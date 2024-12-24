using Logger;
using Docker.DotNet;

namespace DockerEnvironmentWrapper.Deprecated;

public class DockerVolumeManager
{
    private readonly DockerClient _client;
    private readonly ILogger _logger;

    public DockerVolumeManager(DockerClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

}
