using System.Runtime.InteropServices;
using Docker.DotNet;
using MediatR;
using Renci.SshNet;
using Microsoft.Extensions.Logging;

namespace Sparkling.Backend.Requests.Handlers;

public class GetDockerClientRequestHandler : IRequestHandler<GetDockerClientRequest, (IDockerClient, Action)>
{
    private readonly ILogger<GetDockerClientRequestHandler> _logger;

    public GetDockerClientRequestHandler(ILogger<GetDockerClientRequestHandler> logger)
    {
        _logger = logger;
    }

    public async Task<(IDockerClient, Action)> Handle(GetDockerClientRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetDockerClientRequest for Node: {NodeAddress}, IsLocal: {IsLocal}",
            request.Node.Address, request.Node.IsLocal);

        if (!request.Node.IsLocal)
        {
            _logger.LogInformation("Node is remote. Attempting SSH connection to {NodeAddress}", request.Node.Address);
            using var keyStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(request.Node.SshPrivateKey));
            var client = new SshClient(request.Node.Address, "sparkling", new PrivateKeyFile(keyStream));
            var randomPort = (uint)Random.Shared.NextInt64(1024, 65535);

            _logger.LogInformation("Setting up SSH port forwarding: Local 127.0.0.1:{RandomPort} -> Remote 127.0.0.1:5763", randomPort);
            client.AddForwardedPort(new ForwardedPortLocal("127.0.0.1", randomPort, "127.0.0.1", 5763));

            try
            {
                await client.ConnectAsync(cancellationToken);
                _logger.LogInformation("SSH client connected successfully to {NodeAddress}", request.Node.Address);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect SSH client to {NodeAddress}. Ensure SSH server is running and credentials are correct.", request.Node.Address);
                throw; // Re-throw to propagate the error
            }

            IDockerClient dockerClient;
            var dockerUri = new Uri($"http://localhost:{randomPort}");
            _logger.LogInformation("Creating Docker client for remote node using URI: {DockerUri}", dockerUri);
            try
            {
                dockerClient = new DockerClientConfiguration(dockerUri).CreateClient();
                _logger.LogInformation("Docker client created successfully for remote node.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Docker client for remote node using URI: {DockerUri}. Check if Docker daemon is accessible via SSH tunnel.", dockerUri);
                throw;
            }

            return (dockerClient, () =>
            {
                _logger.LogInformation("Disposing SSH client for remote node.");
                client.Dispose();
            });
        }
        else
        {
            // Explicitly use the Docker TCP endpoint
            IDockerClient dockerClient;
            var dockerUri = new Uri("http://127.0.0.1:5763");
            _logger.LogInformation("Node is local. Creating Docker client using URI: {DockerUri}", dockerUri);
            try
            {
                dockerClient = new DockerClientConfiguration(dockerUri).CreateClient();
                _logger.LogInformation("Docker client created successfully for local node.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Docker client for local node using URI: {DockerUri}. Ensure Docker daemon is running and listening on this TCP port.", dockerUri);
                throw;
            }

            return (dockerClient, () => { /* No cleanup needed for local Docker client */ });
        }
    }
}