namespace Sparkling.Backend.Configuration;

public class DockerImageSettings
{
    // These local images are built before running the program using the build script in sparkling/docker-images/
    public string Spark { get; set; } = "spark-cuda";
    public string Jupyter { get; set; } = "jupyter-custom";

    public string SparkImageName => Spark.Contains(':') ? Spark.Split(':')[0] : Spark;
    public string SparkImageTag => Spark.Contains(':') ? Spark.Split(':')[1] : "latest";
}
