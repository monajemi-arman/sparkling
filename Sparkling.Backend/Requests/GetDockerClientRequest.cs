using Docker.DotNet;
using MediatR;
using Sparkling.Backend.Models;

namespace Sparkling.Backend.Requests;

public class GetDockerClientRequest : IRequest<(IDockerClient, Action)>
{
    public Node Node { get; set; } = null!;
}