using Docker.DotNet;
using MediatR;
using Sparkling.Backend.Models;

namespace Sparkling.Backend.Requests;

public class StopJupyterContainerRequest : INotification
{
    public Guid WorkSessionId { get; set; }
}