using Docker.DotNet;
using MediatR;
using Sparkling.Backend.Models;

namespace Sparkling.Backend.Requests;

public class CreateJupyterContainerRequest : INotification
{
    public Guid WorkSessionId { get; set; }
}