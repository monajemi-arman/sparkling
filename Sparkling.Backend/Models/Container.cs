using System.ComponentModel.DataAnnotations;

namespace Sparkling.Backend.Models;

public class Container
{
    [Key]
    public Guid Id { get; set; }
    public ContainerType Type { get; set; }
    public string ImageName { get; set; }
    public string ImageTag { get; set; }
    public DateTime CreationDateTime { get; set; }
    
    //Comma-seperated since there can be multiple
    public string Volumes { get; set; }
    public string Ports { get; set; }
    
    public Guid NodeId { get; set; }
    public virtual Node Node { get; set; } = null!;
    public string? JupyterToken { get; set; }
    public int? JupyterPort { get; set; }
}

public enum ContainerType
{
    SparkNode,
    JupyterNotebook,
    Custom
}