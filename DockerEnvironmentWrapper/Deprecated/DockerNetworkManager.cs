using Logger;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace DockerEnvironmentWrapper.Deprecated;

public class DockerNetworkManager
{
    private readonly DockerClient _client;
    private readonly ILogger _logger;

    public DockerNetworkManager(DockerClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }


}
