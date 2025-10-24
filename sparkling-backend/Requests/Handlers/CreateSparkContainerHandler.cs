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
using Microsoft.Extensions.Logging; // Add this using directive

namespace Sparkling.Backend.Requests.Handlers;

public class CreateSparkContainerRequestHandler(
    SparklingDbContext sparklingDbContext,
    IMediator mediator,
    ILogService logService,
    IOptions<DockerImageSettings> dockerImageOptions,
    IOptions<DockerContainerSettings> dockerContainerOptions,
    ILogger<CreateSparkContainerRequestHandler> logger) // Inject ILogger
    : IRequestHandler<CreateSparkContainerRequest, Container>
{
    private readonly DockerImageSettings _dockerImageSettings = dockerImageOptions.Value;
    private readonly DockerContainerSettings _dockerContainerSettings = dockerContainerOptions.Value;
    // Calculate the project root path once
    private readonly string _projectRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    string composeDir = Environment.GetEnvironmentVariable("COMPOSE_DIR");

    public async Task<Container> Handle(CreateSparkContainerRequest request, CancellationToken cancellationToken)
    {
        var client = request.DockerClient;
        logger.LogInformation("Handling CreateSparkContainerRequest for Node ID: {NodeId}, Type: {ContainerType}", request.Node.Id, request.Type);

        try
        {
            // logService.Broadcast(
            //     new Log(request.Node.Id, "pulling_image", "Pulling Spark image")
            // );
            // logger.LogInformation("Attempting to pull Docker image: {ImageName}:{ImageTag} for Node ID: {NodeId}", _dockerImageSettings.SparkImageName, _dockerImageSettings.SparkImageTag, request.Node.Id);

            // try
            // {
            //     await client.Images.CreateImageAsync(new ImagesCreateParameters()
            //             { FromImage = _dockerImageSettings.SparkImageName, Tag = _dockerImageSettings.SparkImageTag },
            //         new AuthConfig(), new Progress<JSONMessage>(),
            //         cancellationToken);
            //     logger.LogInformation("Successfully pulled Docker image: {ImageName}:{ImageTag} for Node ID: {NodeId}", _dockerImageSettings.SparkImageName, _dockerImageSettings.SparkImageTag, request.Node.Id);
            // }
            // catch (DockerApiException daEx)
            // {
            //     logger.LogError(daEx, "Docker API Exception when pulling image {ImageName}:{ImageTag} for Node ID: {NodeId}. Status: {StatusCode}, Response: {Response}",
            //         _dockerImageSettings.SparkImageName, _dockerImageSettings.SparkImageTag, request.Node.Id, daEx.StatusCode, daEx.ResponseBody);
            //     throw;
            // }
            // catch (Exception ex)
            // {
            //     logger.LogError(ex, "An unexpected error occurred when pulling image {ImageName}:{ImageTag} for Node ID: {NodeId}.", _dockerImageSettings.SparkImageName, _dockerImageSettings.SparkImageTag, request.Node.Id);
            //     throw;
            // }


            logService.Broadcast(
                new Log(request.Node.Id, "creating_container", "Creating Spark container")
            );
            logger.LogInformation("Attempting to create Docker volume for Node ID: {NodeId}", request.Node.Id);

            string volumeName;
            try
            {
                var volumeResponse =
                    await client.Volumes.CreateAsync(
                        new VolumesCreateParameters() { Driver = "local", Name = $"{Guid.NewGuid()}" },
                        cancellationToken
                    );
                volumeName = volumeResponse.Name;
                logger.LogInformation("Successfully created Docker volume: {VolumeName} for Node ID: {NodeId}", volumeName, request.Node.Id);
            }
            catch (DockerApiException daEx)
            {
                logger.LogError(daEx, "Docker API Exception when creating volume for Node ID: {NodeId}. Status: {StatusCode}, Response: {Response}",
                    request.Node.Id, daEx.StatusCode, daEx.ResponseBody);
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unexpected error occurred when creating volume for Node ID: {NodeId}.", request.Node.Id);
                throw;
            }


            Guid containerId;
            if (request.Node.IsLocal)
            {
                logger.LogInformation("Creating Master Spark Node container for Node ID: {NodeId}", request.Node.Id);
                containerId = await CreateMasterNode(client, volumeName, cancellationToken);
            }
            else
            {
                logger.LogInformation("Creating Worker Spark Node container for Node ID: {NodeId}", request.Node.Id);
                containerId = await CreateWorkerNode(client, volumeName, cancellationToken);
            }


            logService.Broadcast(
                new Log(request.Node.Id, "container_created", "Spark container created")
            );
            logger.LogInformation("Spark container with ID {ContainerId} created for Node ID: {NodeId}", containerId, request.Node.Id);

            return new Container()
            {
                CreationDateTime = DateTime.UtcNow,
                Id = containerId,
                ImageName = _dockerImageSettings.SparkImageName,
                //TODO: put the latest SHA tag here
                ImageTag = _dockerImageSettings.SparkImageTag,
                Ports = request.Node.IsLocal ? "7077,8080" : "8081",
                Volumes = volumeName,
                Type = ContainerType.SparkNode,
                NodeId = request.Node.Id
            };
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null)
                msg += " | Inner: " + ex.InnerException.Message;
            logger.LogError(ex, "CreateSparkContainerRequest failed for Node ID: {NodeId}. Error: {ErrorMessage}", request.Node.Id, msg);
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
        logger.LogInformation("Configuring Master Node container parameters for ID: {ContainerId}", containerId);

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
            logger.LogInformation("Creating host directory for shared volume (Master Node): {Path}", absoluteSharedVolumeHostPath);
            Directory.CreateDirectory(absoluteSharedVolumeHostPath);
        }


        string mountSourcePath = absoluteSharedVolumeHostPath;

        if (!string.IsNullOrEmpty(composeDir))
        {
            logger.LogInformation("Oh my! We are inside docker!");
            mountSourcePath = Path.Combine(composeDir, _dockerContainerSettings.SharedVolumeHostPath);
        }

        try
        {
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
                        new Mount() { Type = "bind", Source = mountSourcePath, Target = "/shared-volume" }
                    ],
                    DeviceRequests = new List<DeviceRequest>
                    {
                        new DeviceRequest
                        {
                            Driver = "nvidia",
                            Count = -1, // -1 = all GPUs
                            Capabilities = new List<IList<string>>
                            {
                                new List<string> { "gpu" }
                            }
                        }
                    }
                },
                ExposedPorts = exposedPorts
            }, cancellationToken);
            logger.LogInformation("Master Node container ID {ContainerId} created successfully.", containerId);
        }
        catch (DockerApiException daEx)
        {
            logger.LogError(daEx, "Docker API Exception when creating Master Node container ID: {ContainerId}. Status: {StatusCode}, Response: {Response}",
                containerId, daEx.StatusCode, daEx.ResponseBody);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred when creating Master Node container ID: {ContainerId}.", containerId);
            throw;
        }


        return containerId;
    }

    private async Task<Guid> CreateWorkerNode(IDockerClient client, string volumeName,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Attempting to find local master node for Worker Node creation.");
        var localMasterNode =
            await sparklingDbContext.Nodes.AsNoTracking().FirstOrDefaultAsync(n => n.IsLocal && n.IsActive, cancellationToken);

        if (localMasterNode is null)
        {
            logger.LogError("No local master node found for Worker Node creation.");
            throw new NonRetryableException("No local master node found. Please create a master node first.");
        }
        logger.LogInformation("Local master node found: {MasterNodeAddress}", localMasterNode.Address);

        var containerId = Guid.NewGuid();
        logger.LogInformation("Configuring Worker Node container parameters for ID: {ContainerId}", containerId);

        Dictionary<string, IList<PortBinding>> portBindings = [];
        portBindings.Add("8081/tcp", new List<PortBinding>() { new() { HostPort = "8081" } });

        Dictionary<string, EmptyStruct> exposedPorts = [];
        exposedPorts.Add("8081/tcp", new EmptyStruct());

        // Construct the absolute path for the shared volume
        var absoluteSharedVolumeHostPath = Path.Combine(_projectRootPath, _dockerContainerSettings.SharedVolumeHostPath);

        // Ensure the shared volume host path exists
        if (!Directory.Exists(absoluteSharedVolumeHostPath))
        {
            logger.LogInformation("Creating host directory for shared volume (Worker Node): {Path}", absoluteSharedVolumeHostPath);
            Directory.CreateDirectory(absoluteSharedVolumeHostPath);
        }

        string mountSourcePath = absoluteSharedVolumeHostPath;

        if (!string.IsNullOrEmpty(composeDir))
        {
            mountSourcePath = Path.Combine(composeDir, _dockerContainerSettings.SharedVolumeHostPath);
        }


        try
        {
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
                        new Mount() { Type = "bind", Source = mountSourcePath, Target = "/shared-volume" }
                    ],
                    DeviceRequests = new List<DeviceRequest>
                    {
                        new DeviceRequest
                        {
                            Driver = "nvidia",
                            Count = -1, // -1 = all GPUs
                            Capabilities = new List<IList<string>>
                            {
                                new List<string> { "gpu" }
                            }
                        }
                    }
                },
                ExposedPorts = exposedPorts
            }, cancellationToken);
            logger.LogInformation("Worker Node container ID {ContainerId} created successfully.", containerId);
        }
        catch (DockerApiException daEx)
        {
            logger.LogError(daEx, "Docker API Exception when creating Worker Node container ID: {ContainerId}. Status: {StatusCode}, Response: {Response}",
                containerId, daEx.StatusCode, daEx.ResponseBody);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred when creating Worker Node container ID: {ContainerId}.", containerId);
            throw;
        }


        return containerId;
    }
}