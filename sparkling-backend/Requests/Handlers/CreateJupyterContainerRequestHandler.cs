using Docker.DotNet;
using Docker.DotNet.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Sparkling.Backend.Configuration;
using Sparkling.Backend.Exceptions;
using Sparkling.Backend.Models;

namespace Sparkling.Backend.Requests.Handlers;

public class CreateJupyterContainerRequestHandler(
    IMediator mediator,
    SparklingDbContext sparklingDbContext,
    IOptions<DockerImageSettings> dockerImageOptions,
    IOptions<DockerContainerSettings> dockerContainerOptions)
    : INotificationHandler<CreateJupyterContainerRequest>
{
    private readonly DockerImageSettings _dockerImageSettings = dockerImageOptions.Value;
    private readonly DockerContainerSettings _dockerContainerSettings = dockerContainerOptions.Value;
    // Calculate the project root path once
    private readonly string _projectRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    // helper for structured, colored logging
    private static void LogStep(string message)
    {
        var border = "┌" + new string('─', message.Length + 2) + "┐";
        var content = $"│ \u001b[32m{message}\u001b[0m │";
        var bottom = "└" + new string('─', message.Length + 2) + "┘";
        Console.WriteLine(border);
        Console.WriteLine(content);
        Console.WriteLine(bottom);
    }

    private static async Task<ContainerListResponse> GetContainerById(
        IDockerClient client, Guid databaseId, CancellationToken cancellationToken)
    {
        LogStep($"Listing all containers for databaseId={databaseId}");
        var containers = await client.Containers
            .ListContainersAsync(new ContainersListParameters { All = true }, cancellationToken);
        LogStep($"Found {containers.Count} containers");

        LogStep($"Selecting container whose name contains {databaseId}");
        var container = containers.First(c =>
            c.Names.Any(name => name.Contains(databaseId.ToString())));
        LogStep($"Matched container ID in Docker: {container.ID}");
        return container;
    }

    private static string GenerateRandomToken()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static int GenerateRandomPort()
    {
        var random = new Random();
        // Choose a port in the user range (e.g., 20000-30000)
        return random.Next(20000, 30000);
    }

    private async Task<Container> CreateContainer(
        Node node,
        IDockerClient client,
        CancellationToken cancellationToken,
        Guid containerId,
        string jupyterToken,
        int jupyterPort
    )
    {
        var image = _dockerImageSettings.Jupyter.Contains(':') ? _dockerImageSettings.Jupyter.Split(':')[0] : _dockerImageSettings.Jupyter;
        var tag = _dockerImageSettings.Jupyter.Contains(':') ? _dockerImageSettings.Jupyter.Split(':')[1] : "latest";

        // LogStep($"Pulling image {image}:{tag}");
        // await client.Images.CreateImageAsync(
        //     new ImagesCreateParameters { FromImage = image, Tag = tag },
        //     new AuthConfig(),
        //     new Progress<JSONMessage>(),
        //     cancellationToken);
        // LogStep("Image pull complete");

        LogStep($"Creating container entry in Docker with name {containerId}");

        var portStr = $"{jupyterPort}/tcp";
        Dictionary<string, IList<PortBinding>> portBindings = [];
        portBindings.Add(portStr, [new PortBinding { HostPort = jupyterPort.ToString() }]);
        Dictionary<string, EmptyStruct> exposedPorts = [];
        exposedPorts.Add(portStr, new EmptyStruct());

        var env = new List<string>
        {
            $"JUPYTER_TOKEN={jupyterToken}",
            $"JUPYTER_PORT={jupyterPort}"
        };

        // Construct the absolute path for the shared volume
        var absoluteSharedVolumeHostPath = Path.Combine(_projectRootPath, _dockerContainerSettings.SharedVolumeHostPath);

        // Ensure the shared volume host path exists
        if (!Directory.Exists(absoluteSharedVolumeHostPath))
        {
            LogStep($"Creating host directory for shared volume: {absoluteSharedVolumeHostPath}");
            Directory.CreateDirectory(absoluteSharedVolumeHostPath);
        }

        await client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = $"{image}:{tag}",
            Name = containerId.ToString(),
            Tty = true,
            OpenStdin = true,
            Env = env,
            HostConfig = new HostConfig
            {
                PortBindings = portBindings,
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.Always },
                Mounts = [
                    new Mount() { Type = "bind", Source = absoluteSharedVolumeHostPath, Target = "/shared-volume" }
                ]
            },
            ExposedPorts = exposedPorts,
        }, cancellationToken);
        LogStep("Container creation request submitted");

        var dockerContainer = await GetContainerById(client, containerId, cancellationToken);

        LogStep($"Starting container {dockerContainer.ID}");
        var success = await client.Containers.StartContainerAsync(
            dockerContainer.ID,
            new ContainerStartParameters(),
            cancellationToken);

        if (!success)
        {
            LogStep($"Failed to start container {dockerContainer.ID}, removing it");
            await client.Containers.RemoveContainerAsync(
                dockerContainer.ID,
                new ContainerRemoveParameters { Force = true },
                cancellationToken);
            throw new InvalidOperationException(
                $"Failed to start Jupyter container with ID {dockerContainer.ID}");
        }

        LogStep($"Container {dockerContainer.ID} started successfully");

        // Poll the Docker API until the port mapping appears (or timeout)
        LogStep($"Retrieving port mapping for container {dockerContainer.ID}");
        ContainerInspectResponse inspectResponse = null;
        const int maxAttempts = 10;
        const int delayMs = 10000;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            inspectResponse = await client.Containers
                .InspectContainerAsync(dockerContainer.ID, cancellationToken);

            if (inspectResponse.NetworkSettings.Ports.TryGetValue(portStr, out var bindings)
                && bindings != null && bindings.Count > 0)
            {
                LogStep($"Port mapping found: host port {bindings.First().HostPort}");
                break;
            }

            LogStep($"Port mapping not yet available (attempt {attempt}/{maxAttempts}), waiting {delayMs}ms");
            await Task.Delay(delayMs, cancellationToken);
        }

        if (inspectResponse?.NetworkSettings.Ports.TryGetValue(portStr, out var finalBindings) != true
            || finalBindings.Count == 0)
        {
            throw new InvalidOperationException(
                $"Failed to retrieve port mapping for container {dockerContainer.ID}");
        }

        var hostPort = finalBindings.First().HostPort;

        return new Container
        {
            CreationDateTime = DateTime.UtcNow,
            Id = containerId,
            ImageName = image,
            ImageTag = tag,
            Ports = hostPort,
            Type = ContainerType.JupyterNotebook,
            Volumes = "",
            NodeId = node.Id,
            JupyterToken = jupyterToken,   // ← add this property to your Container model
            JupyterPort = jupyterPort     // ← add this property to your Container model
        };
    }

    public async Task Handle(CreateJupyterContainerRequest request, CancellationToken cancellationToken)
    {
        LogStep("Retrieving active local node from database");
        var node = await sparklingDbContext.Nodes
            .Where(n => n.IsActive && n.IsLocal)
            .SingleOrDefaultAsync(cancellationToken);
        if (node is null)
        {
            LogStep("No active local node found");
            throw new KeyNotFoundException("No active local node found.");
        }
        LogStep($"Found node with ID {node.Id}");

        LogStep("Generating container ID, Jupyter token, and port up front");
        var containerGuid = Guid.NewGuid();
        var jupyterToken = GenerateRandomToken();
        var jupyterPort = GenerateRandomPort();

        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = e => ValueTask.FromResult(
                    e.Outcome.Exception is not null and not NonRetryableException),
                MaxRetryAttempts = 3
            })
            .Build();
        LogStep("Resilience pipeline ready");

        await pipeline.ExecuteAsync(async token =>
        {
            LogStep("Requesting Docker client from mediator");
            var (client, cleanup) = await mediator
                .Send(new GetDockerClientRequest { Node = node }, token);
            LogStep("Docker client acquired");

            try
            {
                LogStep($"Looking up work session {request.WorkSessionId}");
                var workSession = await sparklingDbContext.WorkSessions
                    .FindAsync(new object[] { request.WorkSessionId }, token);
                if (workSession is null)
                {
                    LogStep($"Work session {request.WorkSessionId} not found");
                    throw new NonRetryableException(
                        $"Work session with ID {request.WorkSessionId} not found.");
                }

                LogStep("Creating Jupyter container");
                var container = await CreateContainer(node, client, token, containerGuid, jupyterToken, jupyterPort);
                LogStep($"Container created with internal ID {container.Id}");

                LogStep("Updating work session and persisting container");
                workSession.Status = WorkSessionStatus.Running;
                workSession.StartTime = DateTime.UtcNow;
                workSession.JupyterContainerId = container.Id;
                workSession.JupyterToken = container.JupyterToken;
                workSession.JupyterPort = container.JupyterPort;

                sparklingDbContext.Containers.Add(container);
                sparklingDbContext.Entry(workSession).State = EntityState.Modified;

                LogStep("Saving changes to database");
                await sparklingDbContext.SaveChangesAsync(token);
                LogStep("Database changes saved");
            }
            finally
            {
                LogStep("Cleaning up Docker client resources");
                cleanup();
            }
        }, cancellationToken);
        LogStep("Pipeline execution complete");
    }
}