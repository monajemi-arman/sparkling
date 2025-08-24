using Docker.DotNet;
using Docker.DotNet.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sparkling.Backend.Controllers;
using Sparkling.Backend.Models;
using Sparkling.Backend.Services;

namespace Sparkling.Backend.Requests.Handlers;

public class NodeActivationRequestHandler(IMediator mediator, SparklingDbContext sparklingDbContext, ILogService logService) : INotificationHandler<NodeActivationRequest>
{
    public async Task Handle(NodeActivationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var node =
                await sparklingDbContext
                    .Nodes
                    .Include(n => n.Containers)
                    .FirstOrDefaultAsync(n => n.Id == request.NodeId, cancellationToken);

            if (node is null)
                throw new KeyNotFoundException($"Node with ID {request.NodeId} not found.");

            logService.Broadcast(
                new Log(node.Id, "starting", "Starting node activation")
            );

            logService.Broadcast(
                new Log(node.Id, "pulling_spark_image", "Pulling Spark Docker image")
            );
            await mediator.Send(new StartSparkContainerRequest
            {
                Node = node
            }, cancellationToken);

            logService.Broadcast(
                new Log(node.Id, "starting_spark_container", "Starting Spark container")
            );

            node.IsActive = true;
            sparklingDbContext.Entry(node).State = EntityState.Modified;
            await sparklingDbContext.SaveChangesAsync(cancellationToken);

            logService.Broadcast(
                new Log(node.Id, "activated", "Node activation complete")
            );
        }
        catch (Exception ex)
        {
            var nodeId = request.NodeId;
            var msg = ex.Message;
            if (ex.InnerException != null)
                msg += " | Inner: " + ex.InnerException.Message;
            logService.Broadcast(
                new Log(nodeId, "error", $"Activation failed: {msg}")
            );
            throw;
        }
    }
}