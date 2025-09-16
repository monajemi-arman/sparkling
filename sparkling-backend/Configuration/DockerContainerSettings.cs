namespace Sparkling.Backend.Configuration;

public class DockerContainerSettings
{
    // Relative to project root (sparkling/)
    public string SharedVolumeHostPath { get; set; } = "shared-volume";
}
