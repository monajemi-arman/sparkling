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

            try
            {
                // Connect first, then add/start forwarded port
                await client.ConnectAsync(cancellationToken);
                _logger.LogInformation("SSH client connected successfully to {NodeAddress}", request.Node.Address);

                ForwardedPortLocal forwardedPort = new ForwardedPortLocal("127.0.0.1", randomPort, "127.0.0.1", 5763);
                client.AddForwardedPort(forwardedPort);

                try
                {
                    forwardedPort.Start();
                    _logger.LogInformation("SSH port forwarding started: localhost:{LocalPort} -> {RemoteHost}:{RemotePort}", randomPort, "127.0.0.1", 5763);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start forwarded port after SSH connect. Disposing SSH client.");
                    // Ensure cleanup when start fails
                    try { forwardedPort.Stop(); } catch { /* ignore */ }
                    client.Dispose();
                    throw;
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
                    // Stop forwarded port and dispose client before rethrowing
                    try { forwardedPort.Stop(); } catch { /* ignore */ }
                    client.Dispose();
                    throw;
                }

                return (dockerClient, () =>
                {
                    _logger.LogInformation("Stopping SSH port forwarding and disposing SSH client for remote node.");
                    try
                    {
                        forwardedPort.Stop();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error while stopping forwarded port.");
                    }
                    client.Dispose();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect SSH client to {NodeAddress}. Ensure SSH server is running and credentials are correct.", request.Node.Address);
                throw; // Re-throw to propagate the error
            }
        }
        else
        {
            IDockerClient dockerClient = null;
            Uri dockerUri = null;
            const string unixSocketPath = "/var/run/docker.sock";
            bool clientCreated = false;

            // Attempt 1: Unix socket
            if (File.Exists(unixSocketPath))
            {
                dockerUri = new Uri($"unix://{unixSocketPath}");
                _logger.LogInformation("Node is local. Attempting Docker client creation using Unix socket URI: {DockerUri}", dockerUri);
                try
                {
                    dockerClient = new DockerClientConfiguration(dockerUri).CreateClient();
                    _logger.LogInformation("Docker client created successfully for local node using Unix socket.");
                    clientCreated = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create Docker client for local node using Unix socket URI: {DockerUri}. Falling back to TCP attempts.", dockerUri);
                }
            }

            // Attempt 2: 127.0.0.1 TCP
            if (!clientCreated)
            {
                dockerUri = new Uri("http://127.0.0.1:5763");
                _logger.LogInformation("Node is local. Attempting Docker client creation using TCP URI: {DockerUri}", dockerUri);
                try
                {
                    dockerClient = new DockerClientConfiguration(dockerUri).CreateClient();
                    _logger.LogInformation("Docker client created successfully for local node using 127.0.0.1.");
                    clientCreated = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create Docker client for local node using URI: {DockerUri}. Attempting host.docker.internal.", dockerUri);
                }
            }

            // Attempt 3: host.docker.internal TCP
            if (!clientCreated)
            {
                dockerUri = new Uri("http://host.docker.internal:5763");
                _logger.LogInformation("Node is local. Attempting Docker client creation using TCP URI: {DockerUri}", dockerUri);
                try
                {
                    dockerClient = new DockerClientConfiguration(dockerUri).CreateClient();
                    _logger.LogInformation("Docker client created successfully for local node using host.docker.internal.");
                    clientCreated = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create Docker client for local node using URI: {DockerUri}. All local connection attempts failed.", dockerUri);
                    throw; // Re-throw if all attempts fail
                }
            }

            if (!clientCreated)
            {
                throw new InvalidOperationException("Failed to create Docker client for local node after all attempts.");
            }

            return (dockerClient, () => { /* No cleanup needed for local Docker client */ });
        }
    }
}