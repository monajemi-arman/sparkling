using Docker.DotNet;
using Docker.DotNet.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sparkling.Backend.Configuration;
using Sparkling.Backend.Controllers;
using Sparkling.Backend.Exceptions;
using Sparkling.Backend.Models;
using Sparkling.Backend.Services;

namespace Sparkling.Backend.Requests.Handlers;

public class CreateSparkContainerRequestHandler(
    SparklingDbContext sparklingDbContext,
    IMediator mediator,
    ILogService logService,
    IOptions<DockerImageSettings> dockerImageOptions,
    IOptions<DockerContainerSettings> dockerContainerOptions)
    : IRequestHandler<CreateSparkContainerRequest, Container>
{
    private readonly DockerImageSettings _dockerImageSettings = dockerImageOptions.Value;
    private readonly DockerContainerSettings _dockerContainerSettings = dockerContainerOptions.Value;
    // Calculate the project root path once
    private readonly string _projectRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    public async Task<Container> Handle(CreateSparkContainerRequest request, CancellationToken cancellationToken)
    {
        var client = request.DockerClient;

        try
        {
            logService.Broadcast(
                new Log(request.Node.Id, "pulling_image", "Pulling Spark image")
            );

            await client.Images.CreateImageAsync(new ImagesCreateParameters()
                    { FromImage = _dockerImageSettings.SparkImageName, Tag = _dockerImageSettings.SparkImageTag },
                new AuthConfig(), new Progress<JSONMessage>(),
                cancellationToken);

            logService.Broadcast(
                new Log(request.Node.Id, "creating_container", "Creating Spark container")
            );

            var volumeResponse =
                await client.Volumes.CreateAsync(
                    new VolumesCreateParameters() { Driver = "local", Name = $"{Guid.NewGuid()}" },
                    cancellationToken
                );

            var containerId = request.Node.IsLocal ?
                await CreateMasterNode(client, volumeResponse.Name, cancellationToken):
                await CreateWorkerNode(client, volumeResponse.Name, cancellationToken);

            logService.Broadcast(
                new Log(request.Node.Id, "container_created", "Spark container created")
            );

            return new Container()
            {
                CreationDateTime = DateTime.UtcNow,
                Id = containerId,
                ImageName = _dockerImageSettings.SparkImageName,
                //TODO: put the latest SHA tag here
                ImageTag = _dockerImageSettings.SparkImageTag,
                Ports = request.Node.IsLocal ? "7077,8080" : "8081",
                Volumes = volumeResponse.Name,
                Type = ContainerType.SparkNode,
                NodeId = request.Node.Id
            };
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null)
                msg += " | Inner: " + ex.InnerException.Message;
            logService.Broadcast(
                new Log(request.Node.Id, "error", $"Activation failed: {msg}")
            );
            throw;
        }
    }

    private async Task<Guid> CreateMasterNode(IDockerClient client, string volumeName,
        CancellationToken cancellationToken = default)
    {
        var containerId = Guid.NewGuid();
        
        Dictionary<string, IList<PortBinding>> portBindings = [];
        portBindings.Add("7077/tcp", new List<PortBinding>() { new() { HostPort = "7077" } });
        portBindings.Add("8080/tcp", new List<PortBinding>() { new() { HostPort = "8080" } });

        Dictionary<string, EmptyStruct> exposedPorts = [];
        exposedPorts.Add("7077/tcp", new EmptyStruct());
        exposedPorts.Add("8080/tcp", new EmptyStruct());

        // Construct the absolute path for the shared volume
        var absoluteSharedVolumeHostPath = Path.Combine(_projectRootPath, _dockerContainerSettings.SharedVolumeHostPath);

        // Ensure the shared volume host path exists
        if (!Directory.Exists(absoluteSharedVolumeHostPath))
        {
            Directory.CreateDirectory(absoluteSharedVolumeHostPath);
        }

        await client.Containers.CreateContainerAsync(new CreateContainerParameters()
        {
            Image = _dockerImageSettings.Spark,
            Name = containerId.ToString(),
            Cmd =
            [
                "/bin/sh",
                "-c",
                "/opt/spark/sbin/start-master.sh -p 7077 ; /bin/sh",
            ],
            Tty = true,
            OpenStdin = true,
            HostConfig = new HostConfig()
            {
                PortBindings = portBindings,
                RestartPolicy = new RestartPolicy() { Name = RestartPolicyKind.Always },
                Mounts = [
                    new Mount() { Type = "volume", Source = volumeName, Target = "/opt/spark" },
                    new Mount() { Type = "bind", Source = absoluteSharedVolumeHostPath, Target = "/shared-volume" }
                ]
            },
            ExposedPorts = exposedPorts
        }, cancellationToken);

        return containerId;
    }
    
    private async Task<Guid> CreateWorkerNode(IDockerClient client, string volumeName,
        CancellationToken cancellationToken = default)
    {
        var localMasterNode =
            await sparklingDbContext.Nodes.AsNoTracking().FirstOrDefaultAsync(n => n.IsLocal && n.IsActive, cancellationToken);
        
        if (localMasterNode is null)
            throw new NonRetryableException("No local master node found. Please create a master node first.");
        
        var containerId = Guid.NewGuid();
        
        Dictionary<string, IList<PortBinding>> portBindings = [];
        portBindings.Add("8081/tcp", new List<PortBinding>() { new() { HostPort = "8081" } });

        Dictionary<string, EmptyStruct> exposedPorts = [];
        exposedPorts.Add("8081/tcp", new EmptyStruct());

        // Construct the absolute path for the shared volume
        var absoluteSharedVolumeHostPath = Path.Combine(_projectRootPath, _dockerContainerSettings.SharedVolumeHostPath);

        // Ensure the shared volume host path exists
        if (!Directory.Exists(absoluteSharedVolumeHostPath))
        {
            Console.WriteLine($"Creating host directory for shared volume: {absoluteSharedVolumeHostPath}");
            Directory.CreateDirectory(absoluteSharedVolumeHostPath);
        }

        await client.Containers.CreateContainerAsync(new CreateContainerParameters()
        {
            Image = _dockerImageSettings.Spark,
            Name = containerId.ToString(),
            Cmd =
            [
                "/bin/sh",
                "-c",
                //FIXME: SECURITY: ensure that the localMasterNode.Address is sanitized and safe to use
                $"/opt/spark/sbin/start-worker.sh {localMasterNode.Address}:7077 ; /bin/sh",
            ],
            Tty = true,
            OpenStdin = true,
            HostConfig = new HostConfig()
            {
                PortBindings = portBindings,
                RestartPolicy = new RestartPolicy() { Name = RestartPolicyKind.Always },
                Mounts = [
                    new Mount() { Type = "volume", Source = volumeName, Target = "/opt/spark" },
                    new Mount() { Type = "bind", Source = absoluteSharedVolumeHostPath, Target = "/shared-volume" }
                ]
            },
            ExposedPorts = exposedPorts
        }, cancellationToken);

        return containerId;
    }
}