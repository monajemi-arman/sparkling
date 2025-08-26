using System.Runtime.InteropServices;
using Docker.DotNet;
using MediatR;
using Renci.SshNet;

namespace Sparkling.Backend.Requests.Handlers;

public class GetDockerClientRequestHandler : IRequestHandler<GetDockerClientRequest, (IDockerClient, Action)>
{
    public async Task<(IDockerClient, Action)> Handle(GetDockerClientRequest request,
        CancellationToken cancellationToken)
    {
//         if (request.Node.IsLocal)
//         {
//             if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
//             {
//                 return (
//                     new DockerClientConfiguration(new Uri("http://localhost:1234")).CreateClient(),
//                     () =>
//                     {
//                         /* No cleanup needed for local Docker client */
//                     }
//                 );
//             }
//
//             return (
//                 new DockerClientConfiguration().CreateClient(), () => { /* No cleanup needed for local Docker client */ }
//             );
//         }

        if (!request.Node.IsLocal)
        {
            using var keyStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(request.Node.SshPrivateKey));
            var client = new SshClient(request.Node.Address, "sparkling", new PrivateKeyFile(keyStream));
            var randomPort = (uint)Random.Shared.NextInt64(1024, 65535);
            client.AddForwardedPort(new ForwardedPortLocal("127.0.0.1", randomPort, "127.0.0.1", 5763));
            await client.ConnectAsync(cancellationToken);

            var dockerClient =
                new DockerClientConfiguration(new Uri($"http://localhost:{randomPort}"))
                    .CreateClient();

            return (dockerClient, () => client.Dispose());
        }
        else
        {
            // Explicitly use the Docker TCP endpoint
            var dockerClient =
                new DockerClientConfiguration(new Uri("http://127.0.0.1:5763")).CreateClient();

            return (dockerClient, () => { /* No cleanup needed for local Docker client */ });
        }
    }
}