using Logger;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace DockerEnvironmentWrapper.Deprecated;

public class DockerContainerManager(DockerClient client, ILogger logger)
{
    public async Task<string> CreateContainerAsync(string containerName, string imageName, int containerPort,
        int hostPort, string[] envParams, string volumeName, string networkName)
    {
        try
        {
            // if already existed container name, return that container ID instead
            // check if container name existed
            var containers = await client.Containers.ListContainersAsync(new ContainersListParameters
            {
                All = true // include inactivating containers
            });

            var existingContainer = containers.FirstOrDefault(c => c.Names.Any(n => n.TrimStart('/') == containerName));

            // if existed
            if (existingContainer != null)
            {
                logger.Log($"Container '{containerName}' already exists with ID: {existingContainer.ID}");
                return existingContainer.ID;
            }
            
            // creating new container
            var parameters = new CreateContainerParameters
            {
                Image = imageName,
                Name = containerName,
                Env = envParams.ToList(),
                ExposedPorts = new Dictionary<string, EmptyStruct>
                {
                    { $"{containerPort}/tcp", new EmptyStruct() }
                },
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        {
                            $"{containerPort}/tcp",
                            new List<PortBinding> { new PortBinding { HostPort = hostPort.ToString() } }
                        }
                    },
                    Binds = new List<string>
                    {
                        $"{containerName}:/var/opt/mssql"
                    },
                    NetworkMode = networkName
                }
            };

            var response = await client.Containers.CreateContainerAsync(parameters);
            return response.ID;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error creating container: {ex.Message}");
            throw;
        }
    }

    public async Task StartContainerAsync(string containerId)
    {
        try
        {
            var started = await client.Containers.StartContainerAsync(containerId, null);
            if (started)
                logger.Log($"Container {containerId} started successfully.");
            else
                logger.LogError($"Failed to start container {containerId}.");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error starting container {containerId}: {ex.Message}");
        }
    }

    public async Task StopContainerAsync(string containerId)
    {
        try
        {
            await client.Containers.StopContainerAsync(containerId,
                new ContainerStopParameters { WaitBeforeKillSeconds = 10 });
            logger.Log($"Container {containerId} stopped successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error stopping container {containerId}: {ex.Message}");
        }
    }

    public async Task RemoveContainerAsync(string containerId, bool force = false)
    {
        try
        {
            await client.Containers.RemoveContainerAsync(containerId, 
                new ContainerRemoveParameters() { Force = force });
            logger.Log($"Container {containerId} has been removed.");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error while removing container {containerId}: {ex.Message}");
        }
    }

    private async Task<string> CheckIfContainerExists(string containerName)
    {
        // check if container name existed
        var containers = await client.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true // include inactivating containers
        });

        var existingContainer = containers.FirstOrDefault(c => c.Names.Any(n => n.TrimStart('/') == containerName));

        // if existed
        if (existingContainer != null)
        {
            logger.Log($"Container '{containerName}' already exists with ID: {existingContainer.ID}");

            // restart if container is stopped
            if (existingContainer.State != "running")
            {
                var started = await client.Containers.StartContainerAsync(existingContainer.ID, null);
                if (started)
                {
                    logger.Log($"Container '{containerName}' started successfully.");
                }
                else
                {
                    logger.LogError($"Failed to start container '{containerName}'.");
                }
            }

            return existingContainer.ID;
        }

        return null;
    }
    
}