using MediatR;

namespace Sparkling.Backend.Requests;

public class NodeActivationRequest: INotification
{
    public Guid NodeId { get; set; }
}