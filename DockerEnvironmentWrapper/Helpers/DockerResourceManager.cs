using Logger;
using Docker.DotNet;
using Docker.DotNet.Models;
using FileCompressor;

namespace DockerEnvironmentWrapper.Helpers;

public class DockerResourceManager(DockerClient client, ILogger logger)
{
    #region Resource

    #endregion

    #region Container

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

    #endregion

    #region Network

    public async Task<string> EnsureNetworkExistsAsync(string networkName)
    {
        try
        {
            var networks = await client.Networks.ListNetworksAsync(new NetworksListParameters());
            var existingNetwork = networks.FirstOrDefault(n => n.Name == networkName);

            if (existingNetwork != null)
            {
                return existingNetwork.ID;
            }

            var network = await client.Networks.CreateNetworkAsync(new NetworksCreateParameters
            {
                Name = networkName
            });

            return network.ID;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error ensuring network existence: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Volume

    public async Task CopyToContainerAsync(string containerId, string sourceFilePath, string destinationFilePath,
        bool overwrite = true)
    {
        // create tarfile
        string tarFilePath = TarCompressor.CreateTarFile(sourceFilePath, @"D:/Temps/SamplePE.tar");
        if (File.Exists(tarFilePath))
            logger.Log($"Tar file {tarFilePath} created.");
        
        try
        {
            // Copy tar file into container
            using (FileStream fs = new FileStream(tarFilePath, FileMode.Open, FileAccess.Read))
            {
                var parameters = new ContainerPathStatParameters
                {
                    Path = destinationFilePath,
                    AllowOverwriteDirWithFile = overwrite
                };

                await client.Containers.ExtractArchiveToContainerAsync(containerId, parameters, fs);

                logger.Log($"{tarFilePath} copied to {destinationFilePath} successfully.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error copying file to container volume: {ex.Message}");
        }
        finally
        {
            if (File.Exists(tarFilePath))
            {
                File.Delete(tarFilePath);
            }
        }
    }

    public async Task<string> EnsureVolumeExistsAsync(string volumeName)
    {
        try
        {
            var volumes = await client.Volumes.ListAsync();
            var existingVolume = volumes.Volumes.FirstOrDefault(v => v.Name == volumeName);

            if (existingVolume != null)
            {
                return existingVolume.Name;
            }

            var volume = await client.Volumes.CreateAsync(new VolumesCreateParameters { Name = volumeName });
            return volume.Name;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error ensuring volume existence: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> RemoveVolumeAsync(string volumeName)
    {
        try
        {
            var volumes = await client.Volumes.ListAsync();
            var existingVolume = volumes.Volumes.FirstOrDefault(v => v.Name == volumeName);

            if (existingVolume != null)
            {
                var removed = volumes.Volumes.Remove(new VolumeResponse() { Name = volumeName });
                logger.Log($"Removed volume {volumeName} successfully.");
                return removed;
            }

            logger.Log($"Volumn {volumeName} was not found.");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error ensuring volume existence: {ex.Message}");
            throw;
        }
    }

    #endregion
}