using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sparkling.Backend.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet; // For IDockerClient
using Sparkling.Backend.Requests;

namespace Sparkling.Backend.Requests.Handlers;

public class NodeActivationRequestHandler : IRequestHandler<NodeActivationRequest, bool>
{
    private readonly IMediator _mediator;
    private readonly SparklingDbContext _dbContext;
    private readonly ILogger<NodeActivationRequestHandler> _logger;

    public NodeActivationRequestHandler(IMediator mediator, SparklingDbContext dbContext, ILogger<NodeActivationRequestHandler> logger)
    {
        _mediator = mediator;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> Handle(NodeActivationRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to activate node with ID: {NodeId}", request.NodeId);

        var node = await _dbContext.Nodes
            .Include(n => n.Containers) // Ensure Containers are loaded for logic below
            .FirstOrDefaultAsync(n => n.Id == request.NodeId, cancellationToken);

        if (node == null)
        {
            _logger.LogWarning("Node with ID: {NodeId} not found during activation attempt.", request.NodeId);
            return false;
        }

        IDockerClient? dockerClient = null;
        Action? cleanupAction = null;
        bool activationSuccess = false;

        try
        {
            // Attempt to get a Docker client to verify connectivity
            _logger.LogInformation("Verifying Docker client connectivity for node {NodeId} ({NodeAddress}).", node.Id, node.Address);
            (dockerClient, cleanupAction) = await _mediator.Send(new GetDockerClientRequest { Node = node }, cancellationToken);
            _logger.LogInformation("Docker client connectivity verified for node {NodeId}.", node.Id);

            // If we reached here, connectivity is good. Mark node as active.
            node.IsActive = true;
            _dbContext.Entry(node).State = EntityState.Modified;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Node {NodeId} successfully activated.", node.Id);

            // Ensure a Spark container exists for the node before attempting to start it
            var sparkContainer = node.Containers?.FirstOrDefault(c => c.Type == ContainerType.SparkNode);
            if (sparkContainer == null)
            {
                _logger.LogInformation("No existing Spark container found for node {NodeId}. Creating a new one.", node.Id);
                sparkContainer = await _mediator.Send(new CreateSparkContainerRequest
                {
                    Node = node,
                    Type = node.IsLocal ? CreateSparkContainerRequest.SparkNodeType.Master : CreateSparkContainerRequest.SparkNodeType.Worker,
                    DockerClient = dockerClient // Pass the verified client
                }, cancellationToken);

                node.Containers ??= new List<Container>();
                node.Containers.Add(sparkContainer);
                _dbContext.Entry(node).State = EntityState.Modified; // Mark node as modified to save new container
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("New Spark container {ContainerId} created and added to node {NodeId}.", sparkContainer.Id, node.Id);
            }
            else
            {
                _logger.LogInformation("Existing Spark container {ContainerId} found for node {NodeId}.", sparkContainer.Id, node.Id);
            }

            // Now, attempt to start the Spark master container
            _logger.LogInformation("Attempting to start Spark master container for node {NodeId}.", node.Id);
            await _mediator.Send(new StartSparkContainerRequest { Node = node }, cancellationToken);
            _logger.LogInformation("Spark master container start initiated for node {NodeId}.", node.Id);

            activationSuccess = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate node {NodeId} or start Spark container. Setting IsActive to false. Error: {ErrorMessage}", request.NodeId, ex.Message);
            node.IsActive = false; // Mark as inactive on failure
            _dbContext.Entry(node).State = EntityState.Modified;
            await _dbContext.SaveChangesAsync(cancellationToken);
            activationSuccess = false;
        }
        finally
        {
            // Ensure cleanup is called even if activation fails
            cleanupAction?.Invoke();
            (dockerClient as IDisposable)?.Dispose(); // Dispose the Docker client if it's IDisposable
            _logger.LogInformation("Docker client cleanup performed for node {NodeId}.", request.NodeId);
        }

        return activationSuccess;
    }
}