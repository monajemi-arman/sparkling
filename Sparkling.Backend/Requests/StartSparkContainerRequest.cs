using MediatR;
using Sparkling.Backend.Models;

namespace Sparkling.Backend.Requests;

public class StartSparkContainerRequest: IRequest
{
    public Node Node { get; set; } = null!;
}