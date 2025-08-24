namespace Sparkling.Backend.Models;

public class WorkSession
{
    public Guid Id { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public virtual Container? JupyterContainer { get; set; } = null;
    public Guid? JupyterContainerId { get; set; }
    
    public virtual User User { get; set; } = null!;
    public string UserId { get; set; }
    
    public string? JupyterToken { get; set; }
    public int? JupyterPort { get; set; }

    public WorkSessionStatus Status { get; set; } = WorkSessionStatus.Running;
}

public enum WorkSessionStatus
{
    Running,
    Ended,
    Failed,
    Stopped,
    Starting
}
