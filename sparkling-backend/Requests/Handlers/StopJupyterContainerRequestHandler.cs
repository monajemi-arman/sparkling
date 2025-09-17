using Docker.DotNet;
using Docker.DotNet.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Sparkling.Backend.Exceptions;
using Sparkling.Backend.Models;

namespace Sparkling.Backend.Requests.Handlers;

public class StopJupyerContainerRequestHandler(IMediator mediator, SparklingDbContext sparklingDbContext): INotificationHandler<StopJupyterContainerRequest>
{
    private static async Task<ContainerListResponse> GetContainerById(IDockerClient client, Guid jupyterContainerId, CancellationToken cancellationToken)
    {
        var containers = await client.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    { "label", new Dictionary<string, bool> { { $"sparkling_jupyter_container_id={jupyterContainerId}", true } } }
                }
            },
            cancellationToken
        );

        var container = containers.FirstOrDefault();

        if (container == null)
        {
            throw new NonRetryableException($"Docker container with label 'sparkling_jupyter_container_id={jupyterContainerId}' not found.");
        }

        return container;
    }
    
    public async Task Handle(StopJupyterContainerRequest request, CancellationToken cancellationToken)
    {
        var node =
            await sparklingDbContext
                .Nodes
                .Where(n => n.IsActive && n.IsLocal)
                .SingleAsync(cancellationToken: cancellationToken);
        
        if (node is null)
            throw new KeyNotFoundException("No active local node found.");
        
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
                await mediator.Send(new GetDockerClientRequest() { Node = node }, token);

            try
            {
                var workSession = 
                    await sparklingDbContext
                        .WorkSessions
                        .Include(w => w. JupyterContainer)
                        .SingleAsync(w => w.Id == request.WorkSessionId, cancellationToken: token);
                
                if (workSession.JupyterContainer is null)
                    throw new NonRetryableException($"Work session container with ID {request.WorkSessionId} not found.");

                var dbContainer = workSession.JupyterContainer;
                
                // Pass the JupyterContainer's Id to the updated GetContainerById method
                var container = await GetContainerById(client, workSession.JupyterContainer.Id, token);
                
                await client.Containers.RemoveContainerAsync(container.ID,
                    new ContainerRemoveParameters() { Force = true }, token);

                
                workSession.JupyterContainer = null;
                workSession.JupyterContainerId = null;

                sparklingDbContext.Entry(dbContainer).State = EntityState.Deleted;
                sparklingDbContext.Entry(workSession).State = EntityState.Modified;
                
                await sparklingDbContext.SaveChangesAsync(token);
            }
            finally
            {
                cleanup();
            }
            
        }, cancellationToken);
        
    }
}