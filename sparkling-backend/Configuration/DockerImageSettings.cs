namespace Sparkling.Backend.Configuration;

public class DockerImageSettings
{
    public string Spark { get; set; } = "spark-cuda";
    public string Jupyter { get; set; } = "jupyter-custom";

    public string SparkImageName => Spark.Contains(':') ? Spark.Split(':')[0] : Spark;
    public string SparkImageTag => Spark.Contains(':') ? Spark.Split(':')[1] : "latest";
}
