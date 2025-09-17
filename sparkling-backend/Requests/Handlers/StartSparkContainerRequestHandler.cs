using Docker.DotNet;
using Docker.DotNet.Models;
using MediatR;
using Polly;
using Polly.Retry;
using Sparkling.Backend.Controllers;
using Sparkling.Backend.Exceptions;
using Sparkling.Backend.Models;
using Sparkling.Backend.Services;
using Microsoft.Extensions.Logging;

namespace Sparkling.Backend.Requests.Handlers;

public class StartSparkContainerRequestHandler(IMediator mediator, ILogService logService, ILogger<StartSparkContainerRequestHandler> logger) : IRequestHandler<StartSparkContainerRequest>
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
        logger.LogInformation("Handling StartSparkContainerRequest for Node ID: {NodeId}, IsLocal: {IsLocal}", request.Node.Id, request.Node.IsLocal);


        try
        {
            // Safely check for existing Spark container, handling potential null Containers collection
            var maybeSparkContainer =
                request.Node.Containers?
                    .FirstOrDefault(c => c.Type == ContainerType.SparkNode);

            var pipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = e => ValueTask.FromResult(e.Outcome.Exception is not null and not NonRetryableException),
                    OnRetry = arguments =>
                    {
                        logger.LogWarning("Retrying StartSparkContainerRequest due to: {ExceptionMessage}", arguments.Outcome.Exception?.Message);
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
                        logger.LogInformation("Existing Spark container found in DB for Node ID: {NodeId}. Attempting to find in Docker.", request.Node.Id);
                        string? containerId = null;
                        try
                        {
                            containerId = await GetContainerIdByDatabaseId(client, maybeSparkContainer.Id, token);
                        }
                        catch (DockerApiException daEx)
                        {
                            logger.LogError(daEx, "Docker API Exception when listing containers to find existing Spark container for Node ID: {NodeId}. Status: {StatusCode}, Response: {Response}",
                                request.Node.Id, daEx.StatusCode, daEx.ResponseBody);
                            throw; // Re-throw to trigger retry or propagate
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "An unexpected error occurred when listing containers to find existing Spark container for Node ID: {NodeId}.", request.Node.Id);
                            throw; // Re-throw to trigger retry or propagate
                        }


                        if (containerId is not null)
                        {
                            logger.LogInformation("Spark container ID {ContainerId} found in Docker. Attempting to restart.", containerId);
                            try
                            {
                                await client.Containers.RestartContainerAsync(containerId,
                                    new ContainerRestartParameters(),
                                    token);
                                logger.LogInformation("Spark container ID {ContainerId} restarted successfully.", containerId);

                                // We had a container before and we just restarted it
                                return;
                            }
                            catch (DockerApiException daEx)
                            {
                                logger.LogError(daEx, "Docker API Exception when restarting container ID: {ContainerId}. Status: {StatusCode}, Response: {Response}",
                                    containerId, daEx.StatusCode, daEx.ResponseBody);
                                // If we failed to restart the container, we will try to remove it
                                try
                                {
                                    logger.LogWarning("Restart failed for container ID: {ContainerId}. Attempting to remove it.", containerId);
                                    await client.Containers.RemoveContainerAsync(containerId,
                                        new ContainerRemoveParameters() { Force = true }, token);
                                    logger.LogInformation("Successfully removed failed container ID: {ContainerId}.", containerId);

                                    try
                                    {
                                        request.Node.Containers?.Remove(maybeSparkContainer);
                                        logger.LogInformation("Removed container {ContainerId} from database after failed restart and removal from Docker.", maybeSparkContainer.Id);
                                    }
                                    catch (Exception exInner)
                                    {
                                        logger.LogWarning(exInner, "Ignored error removing container {ContainerId} from database after failed restart.", maybeSparkContainer.Id);
                                    }
                                }
                                catch (DockerApiException removeDaEx)
                                {
                                    logger.LogError(removeDaEx, "Docker API Exception when removing failed container ID: {ContainerId}. Status: {StatusCode}, Response: {Response}",
                                        containerId, removeDaEx.StatusCode, removeDaEx.ResponseBody);
                                    // Ignored, we will just create a new container
                                }
                                catch (Exception removeEx)
                                {
                                    logger.LogError(removeEx, "An unexpected error occurred when removing failed container ID: {ContainerId}.", containerId);
                                    // Ignored, we will just create a new container
                                }
                            }
                            catch (Exception e)
                            {
                                logger.LogError(e, "An unexpected error occurred when restarting container ID: {ContainerId}.", containerId);
                                // If we failed to restart the container, we will try to remove it
                                try
                                {
                                    logger.LogWarning("Restart failed for container ID: {ContainerId}. Attempting to remove it.", containerId);
                                    await client.Containers.RemoveContainerAsync(containerId,
                                        new ContainerRemoveParameters() { Force = true }, token);
                                    logger.LogInformation("Successfully removed failed container ID: {ContainerId}.", containerId);

                                    try
                                    {
                                        request.Node.Containers?.Remove(maybeSparkContainer);
                                        logger.LogInformation("Removed container {ContainerId} from database after failed restart and removal from Docker.", maybeSparkContainer.Id);
                                    }
                                    catch (Exception exInner)
                                    {
                                        logger.LogWarning(exInner, "Ignored error removing container {ContainerId} from database after failed restart.", maybeSparkContainer.Id);
                                    }
                                }
                                catch (DockerApiException removeDaEx)
                                {
                                    logger.LogError(removeDaEx, "Docker API Exception when removing failed container ID: {ContainerId}. Status: {StatusCode}, Response: {Response}",
                                        containerId, removeDaEx.StatusCode, removeDaEx.ResponseBody);
                                    // Ignored, we will just create a new container
                                }
                                catch (Exception removeEx)
                                {
                                    logger.LogError(removeEx, "An unexpected error occurred when removing failed container ID: {ContainerId}.", containerId);
                                    // Ignored, we will just create a new container
                                }
                            }
                        }
                        else
                        {
                            logger.LogWarning("Container with DB ID {DbContainerId} not found in Docker for Node ID: {NodeId}. Removing from database.", maybeSparkContainer.Id, request.Node.Id);
                            // We had a container before, but it was not found in Docker, remove it from the database
                            try
                            {
                                request.Node.Containers?.Remove(maybeSparkContainer);
                                logger.LogInformation("Removed container {DbContainerId} from database.", maybeSparkContainer.Id);
                            } catch (Exception exInner)
                            {
                                logger.LogWarning(exInner, "Ignored error removing container {DbContainerId} from database.", maybeSparkContainer.Id);
                            }
                        }
                    }

                    logger.LogInformation("Creating new Spark container for Node ID: {NodeId}.", request.Node.Id);
                    // We don't have a container, so we need to create a new one
                    var container = await mediator.Send(new CreateSparkContainerRequest
                    {
                        Node = request.Node,
                        Type = request.Node.IsLocal
                            ? CreateSparkContainerRequest.SparkNodeType.Master
                            : CreateSparkContainerRequest.SparkNodeType.Worker,
                        DockerClient = client
                    }, token);

                    string? dockerContainerId = null;
                    try
                    {
                        dockerContainerId =
                            await GetContainerIdByDatabaseId(client, container.Id, token);
                    }
                    catch (DockerApiException daEx)
                    {
                        logger.LogError(daEx, "Docker API Exception when listing containers to find newly created Spark container for Node ID: {NodeId}. Status: {StatusCode}, Response: {Response}",
                            request.Node.Id, daEx.StatusCode, daEx.ResponseBody);
                        throw new InvalidOperationException("Retry..."); // Re-throw to trigger retry
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An unexpected error occurred when listing containers to find newly created Spark container for Node ID: {NodeId}.", request.Node.Id);
                        throw new InvalidOperationException("Retry..."); // Re-throw to trigger retry
                    }


                    if (dockerContainerId is not null)
                    {
                        logger.LogInformation("Newly created Docker container ID {DockerContainerId} found. Attempting to start.", dockerContainerId);
                        bool success;
                        try
                        {
                            success = await client.Containers.StartContainerAsync(dockerContainerId,
                                new ContainerStartParameters(),
                                token);
                            logger.LogInformation("Spark container ID {DockerContainerId} started successfully.", dockerContainerId);
                        }
                        catch (DockerApiException daEx)
                        {
                            logger.LogError(daEx, "Docker API Exception when starting container ID: {DockerContainerId}. Status: {StatusCode}, Response: {Response}",
                                dockerContainerId, daEx.StatusCode, daEx.ResponseBody);
                            success = false; // Mark as failed
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "An unexpected error occurred when starting container ID: {DockerContainerId}.", dockerContainerId);
                            success = false; // Mark as failed
                        }


                        if (!success)
                        {
                            logger.LogError("Failed to start Spark container with ID {DockerContainerId}. Attempting to remove it.", dockerContainerId);
                            try
                            {
                                await client.Containers.RemoveContainerAsync(dockerContainerId,
                                    new ContainerRemoveParameters() { Force = true }, token);
                                logger.LogInformation("Successfully removed failed container ID: {DockerContainerId}.", dockerContainerId);
                            }
                            catch (DockerApiException removeDaEx)
                            {
                                logger.LogError(removeDaEx, "Docker API Exception when removing failed container ID: {DockerContainerId}. Status: {StatusCode}, Response: {Response}",
                                    dockerContainerId, removeDaEx.StatusCode, removeDaEx.ResponseBody);
                            }
                            catch (Exception removeEx)
                            {
                                logger.LogError(removeEx, "An unexpected error occurred when removing failed container ID: {DockerContainerId}.", dockerContainerId);
                            }

                            throw new InvalidOperationException(
                                $"Failed to start Spark container with ID {dockerContainerId}");
                        }

                        request.Node.Containers ??= new List<Container>();
                        request.Node.Containers.Add(container);
                        logger.LogInformation("Container {ContainerId} added to node's container list.", container.Id);
                    }
                    else
                    {
                        logger.LogError("Newly created container with DB ID {DbContainerId} not found in Docker. Removing from database and retrying.", container.Id);
                        try
                        {
                            request.Node.Containers?.Remove(container);
                        }
                        catch (Exception exInner)
                        {
                            logger.LogWarning(exInner, "Ignored error removing container {DbContainerId} from database after not being found in Docker.", container.Id);
                        }

                        throw new InvalidOperationException("Retry...");
                    }
                }
                finally
                {
                    cleanup();
                    logger.LogInformation("Docker client cleanup performed for Node ID: {NodeId}.", request.Node.Id);
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null)
                msg += " | Inner: " + ex.InnerException.Message;
            logger.LogError(ex, "StartSparkContainerRequest failed for Node ID: {NodeId}. Error: {ErrorMessage}", request.Node.Id, msg);
            logService.Broadcast(
                new Log(request.Node.Id, "error", $"Activation failed: {msg}")
            );
            throw;
        }
    }
}