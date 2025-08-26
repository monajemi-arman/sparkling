using System.ComponentModel.DataAnnotations;
using Sparkling.Backend.Dtos.Nodes;

namespace Sparkling.Backend.Models;

public class Node
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    public bool IsLocal { get; set; } = false;

    public string SshPublicKey { get; set; } = string.Empty;
    public string SshPrivateKey { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = false;
    
    public virtual ICollection<Container> Containers { get; set; } = null!;
    
    public NodeDto ToDto(bool noRedaction = false)
    {
        return new NodeDto
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Address = noRedaction ? Address : "****",
            IsActive = IsActive,
            SshPublicKey = noRedaction ? SshPublicKey : "****",
            IsLocal = IsLocal
        };
    }
}