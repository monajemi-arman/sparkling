namespace Sparkling.Backend.Models;

public record Log(Guid NodeId, string Step, string? Message = null);