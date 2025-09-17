using MediatR;
using System;

namespace Sparkling.Backend.Requests;

public class NodeActivationRequest : IRequest<bool>
{
    public Guid NodeId { get; set; }
}