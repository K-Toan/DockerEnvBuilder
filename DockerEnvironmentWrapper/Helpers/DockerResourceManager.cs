using System.Text;
using Logger;
using Docker.DotNet;
using Docker.DotNet.Models;
using FileCompressor;

namespace DockerEnvironmentWrapper.Helpers;

public class DockerResourceManager(DockerClient client, ILogger logger)
{
    #region Resource

    public void SetLimit()
    {
        // not implemented
        return;
    }

    #endregion

    #region Container

    /// <summary>
    /// Execute a command on a specific container
    /// </summary>
    /// <param name="containerId">ID of the container</param>
    /// <param name="commands">Commands to be executed</param>
    /// <param name="user">Docker user's privilege</param>
    public async Task ExecCommandsAsync(string containerId, string[] commands, string user = "root")
    {
        try
        {
            // create exec instance
            var execCreateResponse = await client.Exec.ExecCreateContainerAsync(containerId,
                new ContainerExecCreateParameters
                {
                    AttachStderr = true,
                    AttachStdout = true,
                    Cmd = commands,
                    Tty = false,
                    User = user
                });

            using var stream = await client.Exec.StartAndAttachContainerExecAsync(
                execCreateResponse.ID,
                false); // tty = false to distinguish stdout and stderr

            // read output streams
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            var buffer = new byte[4096];
            while (true)
            {
                var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, CancellationToken.None);
                if (result.Count == 0)
                    break;

                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                if (result.Target == MultiplexedStream.TargetStream.StandardOut)
                {
                    outputBuilder.Append(text);
                    logger.Log($"Output: {text}");
                }
                else if (result.Target == MultiplexedStream.TargetStream.StandardError)
                {
                    errorBuilder.Append(text);
                    logger.LogError($"Error: {text}");
                }
            }

            // get execution result
            var execInspectResponse = await client.Exec.InspectContainerExecAsync(execCreateResponse.ID);
        
            if (execInspectResponse.ExitCode != 0)
            {
                throw new Exception($@"Command execution failed with exit code {execInspectResponse.ExitCode}.
                Stdout: {outputBuilder}
                Stderr: {errorBuilder}
                Commands: {string.Join(" ", commands)}");
            }

            logger.Log($"Command executed successfully. Output: {outputBuilder}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message, ex);
            throw;
        }
    }

    /// <summary>
    /// Create a container and return its id.
    /// </summary>
    /// <param name="containerName">Name of the container</param>
    /// <param name="imageName">Name of the image</param>
    /// <param name="containerPort">Container port</param>
    /// <param name="hostPort">Host (TCP) port</param>
    /// <param name="envParams">Environment parameters</param>
    /// <param name="volumeName">Container volume name</param>
    /// <param name="networkName">Container network used</param>
    /// <returns>Return container id or throw if error</returns>
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

            // ensure network is available if container needed
            if (!string.IsNullOrEmpty(networkName))
            {
                await EnsureNetworkExistsAsync(networkName);
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

    /// <summary>
    /// Start a container.
    /// </summary>
    /// <param name="containerId">ID of the container</param>
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

    /// <summary>
    /// Stop a running container.
    /// </summary>
    /// <param name="containerId">ID of the container</param>
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

    /// <summary>
    /// Remove a container and its volume
    /// </summary>
    /// <param name="containerId">ID of the container</param>
    /// <param name="removeVolumes">Remove volumes</param>
    /// <param name="force">Force to remove</param>
    public async Task RemoveContainerAsync(string containerId, bool removeVolumes = false, bool force = false)
    {
        try
        {
            await client.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters()
                {
                    Force = force,
                    RemoveVolumes = removeVolumes
                });
            logger.Log($"Container {containerId} has been removed.");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error while removing container {containerId}: {ex.Message}");
        }
    }

    #endregion

    #region Network

    /// <summary>
    /// Ensure a network is existed. If not, create a new one
    /// </summary>
    /// <param name="networkName">Name of the network</param>
    /// <returns>Return network ID or throw if error</returns>
    public async Task<string> EnsureNetworkExistsAsync(string networkName)
    {
        try
        {
            var networks = await client.Networks.ListNetworksAsync(new NetworksListParameters());
            var existingNetwork = networks.FirstOrDefault(n => n.Name == networkName);

            if (existingNetwork != null)
            {
                logger.Log($"Network {networkName} already exists.");
                return existingNetwork.ID;
            }

            logger.Log($"Network {networkName} is not exists. Try creating new network.");
            var network = await client.Networks.CreateNetworkAsync(new NetworksCreateParameters
            {
                Name = networkName
            });

            logger.Log($"Network {networkName} has been created.");
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

    /// <summary>
    /// Copy a file into container volume and return its path.
    /// </summary>
    /// <param name="containerId">ID of the container</param>
    /// <param name="sourceFilePath">Path to source file</param>
    /// <param name="destinationFilePath">Path to destination file/folder</param>
    /// <param name="overwrite">Overwrite existing destination file/folder</param>
    /// <returns>Path to copied file</returns>
    public async Task<string> CopyToContainerAsync(string containerId, string sourceFilePath,
        string destinationFilePath,
        bool overwrite = true)
    {
        if (string.IsNullOrEmpty(sourceFilePath) || string.IsNullOrEmpty(destinationFilePath))
            throw new ArgumentNullException(nameof(sourceFilePath));

        // create tar file
        string tarFilePath = TarCompressor.CreateTarFile(sourceFilePath);
        if (!File.Exists(tarFilePath))
        {
            logger.Log($"Cannot create tar file {tarFilePath}.");
            return null;
        }

        logger.Log($"Tar file {tarFilePath} has been created.");

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
            // delete temporary tar file
            File.Delete(tarFilePath);
            logger.Log($"Tar file {tarFilePath} has been deleted.");
        }

        // return copied file path
        string copiedFilePath = Path.Combine(Path.GetDirectoryName(destinationFilePath), Path.GetFileName(sourceFilePath)).Replace("\\", "/");
        logger.Log($"Copied file path: {copiedFilePath}");
        return copiedFilePath;
    }

    /// <summary>
    /// Ensure a volume is existed.
    /// </summary>
    /// <param name="volumeName">Name of the volume</param>
    /// <returns>Return volume name of throw if error</returns>
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

    #endregion
}