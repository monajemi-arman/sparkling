using Sparkling.Backend.Models;

public class WorkSessionDto
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public WorkSessionStatus Status { get; set; }
    public Guid? JupyterContainerId { get; set; }
    public string? JupyterToken { get; set; }
    public int? JupyterPort { get; set; }
}