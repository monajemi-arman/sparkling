namespace Sparkling.Backend.Configuration;

public class DockerImageSettings
{
    public string Spark { get; set; } = "spark:latest";
    public string Jupyter { get; set; } = "quay.io/jupyter/all-spark-notebook";

    public string SparkImageName => Spark.Contains(':') ? Spark.Split(':')[0] : Spark;
    public string SparkImageTag => Spark.Contains(':') ? Spark.Split(':')[1] : "latest";
}
