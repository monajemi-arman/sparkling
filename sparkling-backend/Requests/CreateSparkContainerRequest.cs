using Docker.DotNet;
using MediatR;
using Sparkling.Backend.Models;

namespace Sparkling.Backend.Requests;

public class CreateSparkContainerRequest : IRequest<Container>
{
    public Node Node { get; set; } = null!;
    public IDockerClient DockerClient { get; set; } = null!;
    public SparkNodeType Type { get; set; }

    public enum SparkNodeType
    {
        Master,
        Worker
    }
}