using Docker.DotNet;

namespace DockerEnvironmentWrapper.Helpers;

public class DockerClientWrapper
{
    private DockerClient _client;

    public DockerClientWrapper()
    {
        _client = new DockerClientConfiguration().CreateClient();
    }

    public DockerClient GetClient() => _client;
}
