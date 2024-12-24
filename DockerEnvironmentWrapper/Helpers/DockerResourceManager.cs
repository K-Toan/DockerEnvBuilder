using System.IO.Compression;
using Logger;
using Docker.DotNet;
using Docker.DotNet.Models;

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

    public async Task CopyToContainerAsync(string containerId, string sourcePath, string destinationPath, bool overwrite = true)
    {
        try
        {
            using (var tarStream = CreateTar(sourcePath))
            {
                // send tarball into container
                await client.Containers.ExtractArchiveToContainerAsync(
                    containerId,
                    new ContainerPathStatParameters { Path = destinationPath },
                    tarStream);
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error copying file to container volume: {ex.Message}");
        }
    }

    private Stream CreateTar(string sourcePath)
    {
        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            // if source path leads to a file
            if (File.Exists(sourcePath))
            {
                logger.Log($"Path leads to a file");
                // include to tar if file
                var entry = archive.CreateEntry(Path.GetFileName(sourcePath));
                using (var entryStream = entry.Open())
                using (var fileStream = File.OpenRead(sourcePath))
                {
                    fileStream.CopyTo(entryStream);
                }
            }
            // if source path leads to a directory
            else if (Directory.Exists(sourcePath))
            {
                logger.Log($"Path leads to a directory");
                // include all files if folder
                foreach (var filePath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(sourcePath, filePath).Replace("\\", "/");
                    var entry = archive.CreateEntry(relativePath);
                    using (var entryStream = entry.Open())
                    using (var fileStream = File.OpenRead(filePath))
                    {
                        fileStream.CopyTo(entryStream);
                    }
                }
            }
            else
            {
                throw new FileNotFoundException($"Source path '{sourcePath}' does not exist.");
            }
        }

        memoryStream.Seek(0, SeekOrigin.Begin); // Reset stream in order to read from start
        return memoryStream;
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