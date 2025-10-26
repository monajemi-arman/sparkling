using Docker.DotNet;
using Docker.DotNet.Models;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Renci.SshNet;
using Sparkling.Backend.Dtos.Nodes;
using Sparkling.Backend.Models;
using Sparkling.Backend.Requests;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Sparkling.Backend.Services;
using Microsoft.Extensions.Logging; // Add this using directive

namespace Sparkling.Backend.Controllers;

[ApiController]
[Route("api/v0/[controller]")]
public class NodesController(
    SparklingDbContext sparklingDbContext,
    IValidator<NodeDto> nodeValidator,
    IMediator mediator,
    ILogService logService,
    ILogger<NodesController> logger) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<IEnumerable<NodeDto>> GetNodes()
    {
        return
            await sparklingDbContext
                .Nodes
                .AsNoTracking()
                .Select(node => node.ToDto(User.IsInRole("Admin")))
                .ToListAsync();
    }

    [Authorize(Roles = "Admin")]
    [HttpPut]
    public async Task<ActionResult<NodeDto>> CreateNode([FromBody] NodeDto nodeDto)
    {
        if (!(await nodeValidator.ValidateAsync(nodeDto)).IsValid)
            return BadRequest();

        switch (nodeDto.IsLocal)
        {
            case true when await sparklingDbContext.Nodes.AnyAsync(node => node.IsLocal):
                return BadRequest("Only one local node is allowed.");
            case false when !await sparklingDbContext.Nodes.AnyAsync(node => node.IsLocal && node.IsActive):
                return BadRequest("At least one active local node is required.");
        }

        var keygen = new SshKeyGenerator.SshKeyGenerator(2048);
        var privateKey = keygen.ToPrivateKey();
        var publicSshKey = keygen.ToRfcPublicKey();

        var node = new Node
        {
            Id = Guid.NewGuid(),
            Name = nodeDto.Name,
            Description = nodeDto.Description,
            Address = nodeDto.Address,
            SshPublicKey = publicSshKey,
            SshPrivateKey = privateKey,
            IsActive = false,
            IsLocal = nodeDto.IsLocal
        };

        sparklingDbContext.Nodes.Add(node);
        await sparklingDbContext.SaveChangesAsync();

        return node.ToDto(User.IsInRole("Admin"));
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NodeDto>> GetNode(Guid id)
    {
        var node = await sparklingDbContext.Nodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id);

        if (node == null)
        {
            return NotFound();
        }

        return node.ToDto(User.IsInRole("Admin"));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/script")]
    public async Task<string> GetInitializationScript(Guid id)
    {
        var node = await sparklingDbContext.Nodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id);

        if (node == null)
        {
            return string.Empty;
        }

        return """
               #!/bin/bash
               set -euo pipefail
               
               """ + $"""
               NODE_ID="{node.Id}"
               NODE_NAME="{node.Name}"
               NODE_ADDRESS="{node.Address}"
               PUBLIC_KEY="{node.SshPublicKey}"
               
               """ + """
               echo "Initializing node $NODE_NAME (ID: $NODE_ID)..."

               # Determine OS and package manager
               if [ -f /etc/os-release ]; then
                   . /etc/os-release
                   OS_ID="${ID}"
               else
                   echo "Cannot determine OS. Exiting."
                   exit 1
               fi

               # Package install function
               install_packages() {
                   case "$OS_ID" in
                       ubuntu|debian|kali)
                           sudo apt-get update
                           sudo apt-get install -y docker.io openssh-server
                           ;;
                       centos|rhel)
                           sudo yum install -y docker openssh-server
                           ;;
                       fedora)
                           sudo dnf install -y docker openssh-server
                           ;;
                       arch)
                           sudo pacman -Sy --noconfirm docker openssh
                           ;;
                       opensuse*|suse)
                           sudo zypper install -y docker openssh
                           ;;
                       *)
                           echo "Unsupported OS: $OS_ID"
                           exit 1
                           ;;
                   esac
               }

               # Install required packages
               install_packages

               # Enable and start Docker
               sudo systemctl enable --now docker

               # Enable and start SSH server
               sudo systemctl enable --now sshd || sudo systemctl enable --now ssh

               # Create dedicated user for Sparkling
               if ! id -u "sparkling" > /dev/null 2>&1; then
                   sudo useradd -m -s /bin/bash sparkling
               fi

               # Configure Docker to listen on both Unix socket and TCP port 5763
               sudo mkdir -p /etc/systemd/system/docker.service.d

               # Determine the correct path for dockerd
               DOCKERD_PATH=$(which dockerd)
               if [ -z "$DOCKERD_PATH" ]; then
                   echo "Error: dockerd executable not found. Exiting."
                   exit 1
               fi

               cat <<EOF | sudo tee /etc/systemd/system/docker.service.d/tcp-port.conf
               [Service]
               ExecStart=
               ExecStart=$DOCKERD_PATH -H fd:// -H tcp://127.0.0.1:5763
               EOF

               sudo systemctl daemon-reexec
               sudo systemctl daemon-reload
               sudo systemctl restart docker

               # Setup SSH access for the 'sparkling' user
               sudo mkdir -p /home/sparkling/.ssh
               echo "$PUBLIC_KEY" | sudo tee -a /home/sparkling/.ssh/authorized_keys > /dev/null
               sudo chmod 600 /home/sparkling/.ssh/authorized_keys
               sudo chown -R sparkling:sparkling /home/sparkling/.ssh

               # Add 'sparkling' user to docker group
               sudo usermod -aG docker sparkling

               # Create configuration directory
               sudo mkdir -p /etc/sparkling
               sudo chown -R sparkling:sparkling /etc/sparkling

               # Write node configuration
               cat << EOF | sudo tee -a /etc/sparkling/config.json
               {
                 "id": "${NODE_ID}",
                 "name": "${NODE_NAME}",
                 "address": "${NODE_ADDRESS}"
               }
               EOF

               echo "Node initialization complete."
               """;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult> ActivateNode(Guid id)
    {
        var node = await sparklingDbContext.Nodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id);

        if (node is null)
        {
            return NotFound("Node not found.");
        }

        logger.LogInformation("Received activation request for node {NodeId}.", id);
        bool success = await mediator.Send(new NodeActivationRequest() { NodeId = id });

        if (success)
        {
            logger.LogInformation("Node {NodeId} activated successfully.", id);
            return Ok();
        }
        else
        {
            logger.LogError("Node {NodeId} activation failed.", id);
            // The handler already updated the node's IsActive status to false
            return StatusCode(StatusCodes.Status500InternalServerError, "Node activation failed. Check logs for details.");
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("logs")]
    public async Task GetLogs()
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        try
        {
            logService.AddClient(Response);
            await Task.FromCanceled(HttpContext.RequestAborted);
        }
        finally
        {
            logService.RemoveClient(Response);
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNode(Guid id)
    {
        logger.LogInformation("Attempting to delete node with ID: {NodeId}", id);

        var node = await sparklingDbContext.Nodes
            .Include(n => n.Containers)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (node == null)
        {
            logger.LogWarning("Node with ID: {NodeId} not found for deletion.", id);
            return NotFound();
        }

        // Remove containers associated with the node from Docker host
        try
        {
            logger.LogInformation("Getting Docker client for node {NodeId} ({NodeAddress}) to remove associated containers.", node.Id, node.Address);
            // Get Docker client for this node
            (IDockerClient dockerClient, Action cleanup) = await mediator.Send(
                new GetDockerClientRequest { Node = node }, HttpContext.RequestAborted
            );
            using (dockerClient as IDisposable)
            {
                logger.LogInformation("Listing containers with label 'sparkling_node_id={NodeId}' on node {NodeAddress}.", node.Id, node.Address);
                // Find containers with label "sparkling_node_id" == node.Id
                var containers = await dockerClient.Containers.ListContainersAsync(
                    new Docker.DotNet.Models.ContainersListParameters
                    {
                        All = true,
                        Filters = new Dictionary<string, IDictionary<string, bool>>
                        {
                            { "label", new Dictionary<string, bool> { { $"sparkling_node_id={node.Id}", true } } }
                        }
                    }
                );
                logger.LogInformation("Found {ContainerCount} containers to remove for node {NodeId}.", containers.Count, node.Id);

                foreach (var container in containers)
                {
                    try
                    {
                        logger.LogInformation("Attempting to remove container ID: {ContainerId}, Name: {ContainerName}", container.ID, container.Names?.FirstOrDefault());
                        await dockerClient.Containers.RemoveContainerAsync(
                            container.ID,
                            new Docker.DotNet.Models.ContainerRemoveParameters { Force = true }
                        );
                        logger.LogInformation("Successfully removed container ID: {ContainerId}", container.ID);
                    }
                    catch (DockerApiException daEx)
                    {
                        logger.LogError(daEx, "Docker API Exception when removing container ID: {ContainerId}. Status: {StatusCode}, Response: {Response}",
                            container.ID, daEx.StatusCode, daEx.ResponseBody);
                        // Ignore errors for individual containers, continue with others
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An unexpected error occurred when removing container ID: {ContainerId}.", container.ID);
                        // Ignore errors for individual containers, continue with others
                    }
                }
            }
            cleanup?.Invoke();
            logger.LogInformation("Docker cleanup for node {NodeId} completed.", node.Id);
        }
        catch (DockerApiException daEx)
        {
            logger.LogError(daEx, "Docker API Exception during Docker cleanup for node {NodeId}. Status: {StatusCode}, Response: {Response}",
                node.Id, daEx.StatusCode, daEx.ResponseBody);
            // Ignore errors in Docker cleanup, continue with DB removal
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred during Docker cleanup for node {NodeId}.", node.Id);
            // Ignore errors in Docker cleanup, continue with DB removal
        }

        // Optionally: Remove containers associated with the node from DB
        if (node.Containers != null)
        {
            logger.LogInformation("Removing {ContainerCount} associated containers from database for node {NodeId}.", node.Containers.Count, node.Id);
            sparklingDbContext.Containers.RemoveRange(node.Containers);
        }
        sparklingDbContext.Nodes.Remove(node);
        await sparklingDbContext.SaveChangesAsync();
        logger.LogInformation("Node {NodeId} and its associated containers removed from database.", node.Id);

        return NoContent();
    }
}