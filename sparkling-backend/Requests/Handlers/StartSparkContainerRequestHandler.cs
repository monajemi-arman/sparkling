using Docker.DotNet;
using Docker.DotNet.Models;
using MediatR;
using Polly;
using Polly.Retry;
using Sparkling.Backend.Controllers;
using Sparkling.Backend.Exceptions;
using Sparkling.Backend.Models;
using Sparkling.Backend.Services;

namespace Sparkling.Backend.Requests.Handlers;

public class StartSparkContainerRequestHandler(IMediator mediator, ILogService logService) : IRequestHandler<StartSparkContainerRequest>
{
    private static async Task<string?> GetContainerIdByDatabaseId(IDockerClient client, Guid databaseId,
        CancellationToken cancellationToken)
    {
        var containers = await client.Containers.ListContainersAsync(new ContainersListParameters()
        {
            All = true
        }, cancellationToken);

        var spark =
            containers
                .FirstOrDefault(container => container.Names.Any(name => name.Contains(databaseId.ToString())));

        return spark?.ID;
    }

    public async Task Handle(StartSparkContainerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var maybeSparkContainer =
                request.Node
                    .Containers
                    .FirstOrDefault(c => c.Type == ContainerType.SparkNode);

            var pipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = e => ValueTask.FromResult(e.Outcome.Exception is not null and not NonRetryableException),
                    OnRetry = arguments =>
                    {
                        Console.WriteLine("Retrying due to: " + arguments.Outcome.Exception?.Message);
                        return ValueTask.CompletedTask;
                    },
                    MaxRetryAttempts = 3
                })
                .Build(); // Builds the resilience pipeline

            await pipeline.ExecuteAsync(async token =>
            {
                var (client, cleanup) =
                    await mediator.Send(new GetDockerClientRequest() { Node = request.Node }, token);
                try
                {
                    if (maybeSparkContainer is not null)
                    {
                        var containerId =
                            await GetContainerIdByDatabaseId(client, maybeSparkContainer.Id, token);

                        if (containerId is not null)
                        {
                            try
                            {
                                await client.Containers.RestartContainerAsync(containerId,
                                    new ContainerRestartParameters(),
                                    token);

                                // We had a container before and we just restarted it
                                return;
                            }
                            catch (Exception e)
                            {
                                // If we failed to restart the container, we will try to remove it
                                try
                                {
                                    await client.Containers.RemoveContainerAsync(containerId,
                                        new ContainerRemoveParameters() { Force = true }, token);

                                    try
                                    {
                                        request.Node.Containers?.Remove(maybeSparkContainer);
                                    }
                                    catch (Exception)
                                    {
                                        // Ignored
                                    }
                                }
                                catch (Exception)
                                {
                                    // Ignored, we will just create a new container
                                }
                            }
                        }
                        else
                        {
                            // We had a container before, but it was not found in Docker, remove it from the database
                            try
                            {
                                request.Node.Containers?.Remove(maybeSparkContainer);
                            }
                            catch (Exception e)
                            {
                                // Ignored
                            }
                        }
                    }

                    // We don't have a container, so we need to create a new one
                    var container = await mediator.Send(new CreateSparkContainerRequest
                    {
                        Node = request.Node,
                        Type = request.Node.IsLocal
                            ? CreateSparkContainerRequest.SparkNodeType.Master
                            : CreateSparkContainerRequest.SparkNodeType.Worker,
                        DockerClient = client
                    }, token);

                    var dockerContainerId =
                        await GetContainerIdByDatabaseId(client, container.Id, token);

                    if (dockerContainerId is not null)
                    {
                        var success = await client.Containers.StartContainerAsync(dockerContainerId,
                            new ContainerStartParameters(),
                            token);

                        if (!success)
                        {
                            await client.Containers.RemoveContainerAsync(dockerContainerId,
                                new ContainerRemoveParameters() { Force = true }, token);

                            throw new InvalidOperationException(
                                $"Failed to start Spark container with ID {dockerContainerId}");
                        }

                        request.Node.Containers ??= new List<Container>();
                        request.Node.Containers.Add(container);
                    }
                    else
                    {
                        try
                        {
                            request.Node.Containers?.Remove(container);
                        }
                        catch (Exception e)
                        {
                            // Ignored
                        }

                        throw new InvalidOperationException("Retry...");
                    }
                }
                finally
                {
                    cleanup();
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null)
                msg += " | Inner: " + ex.InnerException.Message;
            logService.Broadcast(
                new Log(request.Node.Id, "error", $"Activation failed: {msg}")
            );
            throw;
        }
    }
}