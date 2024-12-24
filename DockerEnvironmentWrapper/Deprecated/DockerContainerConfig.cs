namespace DockerEnvironmentWrapper.Deprecated;

public class DockerContainerConfig
{
    public string ContainerName { get; set; }
    public string ImageName { get; set; }
    public int ContainerPort { get; set; }
    public int HostPort { get; set; }
    public string[] EnvParams { get; set; }
    public string VolumeName { get; set; }
    public string NetworkName { get; set; }

    public DockerContainerConfig(string containerName, string imageName, int containerPort, int hostPort, string[] envParams, string volumeName, string networkName)
    {
        ContainerName = containerName;
        ImageName = imageName;
        ContainerPort = containerPort;
        HostPort = hostPort;
        EnvParams = envParams;
        VolumeName = volumeName;
        NetworkName = networkName;
    }

    public DockerContainerConfig UseMssqlContainerConfig()
    {
        return this;
    }

    public DockerContainerConfig UseMongoDbContainerConfig()
    {
        return this;
    }

    public DockerContainerConfig UseTomcatContainerConfig()
    {
        return this;
    }

    public DockerContainerConfig UseIisContainerConfig()
    {
        return this;
    }
}