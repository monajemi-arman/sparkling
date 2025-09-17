using Docker.DotNet;
using Docker.DotNet.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Sparkling.Backend.Exceptions;
using Sparkling.Backend.Models;
using Microsoft.Extensions.Logging; // Add this using directive

namespace Sparkling.Backend.Requests.Handlers;

public class StopJupyerContainerRequestHandler(IMediator mediator, SparklingDbContext sparklingDbContext, ILogger<StopJupyerContainerRequestHandler> logger): INotificationHandler<StopJupyterContainerRequest>
{
    private static async Task<ContainerListResponse> GetContainerById(IDockerClient client, Guid jupyterContainerId, CancellationToken cancellationToken, ILogger<StopJupyerContainerRequestHandler> logger)
    {
        logger.LogDebug("Searching for Docker container with label 'sparkling_jupyter_container_id={JupyterContainerId}'.", jupyterContainerId);

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
            logger.LogWarning("Docker container with label 'sparkling_jupyter_container_id={JupyterContainerId}' not found.", jupyterContainerId);
            throw new NonRetryableException($"Docker container with label 'sparkling_jupyter_container_id={jupyterContainerId}' not found.");
        }

        logger.LogDebug("Found Docker container with ID: {DockerContainerId} for JupyterContainerId: {JupyterContainerId}.", container.ID, jupyterContainerId);
        return container;
    }
    
    public async Task Handle(StopJupyterContainerRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling StopJupyterContainerRequest for WorkSessionId: {WorkSessionId}", request.WorkSessionId);

        Node node;
        try
        {
            logger.LogDebug("Attempting to retrieve active local node.");
            node =
                await sparklingDbContext
                    .Nodes
                    .Where(n => n.IsActive && n.IsLocal)
                    .SingleAsync(cancellationToken: cancellationToken);
            logger.LogDebug("Found active local node: {NodeName} (ID: {NodeId}).", node.Name, node.Id);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogError(ex, "No active local node found for stopping container for WorkSessionId: {WorkSessionId}.", request.WorkSessionId);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while retrieving the active local node for WorkSessionId: {WorkSessionId}.", request.WorkSessionId);
            throw;
        }
        
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = e => ValueTask.FromResult(e.Outcome.Exception is not null and not NonRetryableException),
                OnRetry = arguments =>
                {
                    logger.LogWarning("Retrying StopJupyterContainerRequest for WorkSessionId {WorkSessionId} due to: {ExceptionMessage}", request.WorkSessionId, arguments.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                },
                MaxRetryAttempts = 3
            })
            .Build(); // Builds the resilience pipeline

        await pipeline.ExecuteAsync(async token =>
        {
            (IDockerClient client, Action cleanup) dockerClientInfo = (null, () => { }); // Initialize with default values
            try
            {
                logger.LogDebug("Obtaining Docker client for node {NodeName} (ID: {NodeId}).", node.Name, node.Id);
                dockerClientInfo = await mediator.Send(new GetDockerClientRequest() { Node = node }, token);
                var (client, cleanup) = dockerClientInfo;
                logger.LogDebug("Docker client obtained. Attempting to retrieve work session {WorkSessionId}.", request.WorkSessionId);

                var workSession = 
                    await sparklingDbContext
                        .WorkSessions
                        .Include(w => w. JupyterContainer)
                        .SingleAsync(w => w.Id == request.WorkSessionId, cancellationToken: token);
                
                if (workSession.JupyterContainer is null)
                {
                    logger.LogError("Work session container with ID {WorkSessionId} not found in database.", request.WorkSessionId);
                    throw new NonRetryableException($"Work session container with ID {request.WorkSessionId} not found.");
                }

                var dbContainer = workSession.JupyterContainer;
                logger.LogDebug("Work session {WorkSessionId} found. Associated JupyterContainerId: {JupyterContainerId}.", workSession.Id, dbContainer.Id);
                
                logger.LogDebug("Attempting to find Docker container with ID {JupyterContainerId} on node {NodeName}.", dbContainer.Id, node.Name);
                var container = await GetContainerById(client, dbContainer.Id, token, logger);
                
                logger.LogDebug("Found Docker container with ID: {DockerContainerId}. Attempting to remove it.", container.ID);
                await client.Containers.RemoveContainerAsync(container.ID,
                    new ContainerRemoveParameters() { Force = true }, token);
                logger.LogInformation("Successfully removed Docker container ID: {DockerContainerId} for WorkSessionId: {WorkSessionId}.", container.ID, request.WorkSessionId);

                
                workSession.JupyterContainer = null;
                workSession.JupyterContainerId = null;

                sparklingDbContext.Entry(dbContainer).State = EntityState.Deleted;
                sparklingDbContext.Entry(workSession).State = EntityState.Modified;
                logger.LogDebug("Updating database for WorkSessionId: {WorkSessionId}. Marking container {JupyterContainerId} as deleted.", request.WorkSessionId, dbContainer.Id);
                
                await sparklingDbContext.SaveChangesAsync(token);
                logger.LogInformation("Database updated for WorkSessionId: {WorkSessionId}. Container {JupyterContainerId} removed from DB.", request.WorkSessionId, dbContainer.Id);
            }
            catch (NonRetryableException ex)
            {
                logger.LogError(ex, "Non-retryable exception occurred while stopping Jupyter container for WorkSessionId: {WorkSessionId}.", request.WorkSessionId);
                throw; // Re-throw non-retryable exceptions
            }
            catch (DockerApiException daEx)
            {
                logger.LogError(daEx, "Docker API Exception occurred while stopping Jupyter container for WorkSessionId: {WorkSessionId}. Status: {StatusCode}, Response: {Response}",
                    request.WorkSessionId, daEx.StatusCode, daEx.ResponseBody);
                throw; // Re-throw to be handled by resilience pipeline
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unexpected error occurred while stopping Jupyter container for WorkSessionId: {WorkSessionId}.", request.WorkSessionId);
                throw; // Re-throw to be handled by resilience pipeline
            }
            finally
            {
                logger.LogDebug("Invoking Docker client cleanup for node {NodeName} (ID: {NodeId}).", node.Name, node.Id);
                dockerClientInfo.cleanup();
            }
            
        }, cancellationToken);
        
    }
}