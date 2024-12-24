// using Docker.DotNet;
// using Docker.DotNet.Models;
//
// public class DockerEnvironmentExecutor
// {
//     private string _networkName;
//     private readonly DockerClient _client;
//
//     public DockerEnvironmentExecutor()
//     {
//         _client = new DockerClientConfiguration()
//             .CreateClient();
//     }
//
//     public DockerEnvironmentExecutor EnableDockerNetworkAsync(string networkName)
//     {
//         _networkName = networkName;
//         return this;
//     }
//
//     public async Task<string> RunContainerAsync(string containerName, string imageName, string imageTag, string[] envParams, int containerPort, int hostPort, string volumeName)
//     {
//         try
//         {
//             // ensure docker already have image with tag
//             await EnsureImageExistsAsync(imageName, imageTag);
//
//             // config container params
//             var containerParams = new CreateContainerParameters()
//             {
//                 Image = imageName + ":" + imageTag,
//                 Name = containerName,
//                 Env = envParams.ToList(),
//                 ExposedPorts = new Dictionary<string, EmptyStruct>
//                 {
//                      { $"{containerPort}/tcp", new EmptyStruct() }
//                 },
//                 HostConfig = new HostConfig
//                 {
//                     PortBindings = new Dictionary<string, IList<PortBinding>>
//                     {
//                         { $"{containerPort}/tcp", new List<PortBinding> { new PortBinding { HostPort = hostPort.ToString() } } }
//                     },
//                     Binds = new List<string>
//                     {
//                         $"{containerName}:/var/opt/mssql"
//                     }
//                 }
//             };
//
//             // create container with configs
//             var response = await _client.Containers.CreateContainerAsync(containerParams);
//             Logger.Log($"Container {containerName} created with ID: {response.ID}");
//
//             // run container
//             var started = await _client.Containers.StartContainerAsync(response.ID, null);
//             if (!started)
//             {
//                 Logger.Log($"Failed to start container {containerName}.");
//                 return null;
//             }
//             Logger.Log($"Container {containerName} started successfully.");
//
//             // if network enabled, connect to it
//             if (!string.IsNullOrEmpty(_networkName))
//             {
//                 await ConnectNetworkAsync(containerName, _networkName);
//             }
//
//             return response.ID;
//         }
//         catch (Exception e)
//         {
//             Logger.Log(e.Message);
//             return null;
//         }
//     }
//
//     public async Task ConnectNetworkAsync(string containerName, string networkName)
//     {
//         if (string.IsNullOrEmpty(_networkName))
//         {
//             Logger.Log("Network not enabled");
//             return;
//         }
//
//         try
//         {
//             // try get network id, create new if not exist
//             string networkID = await EnsureNetworkExistsAsync(_networkName);
//
//             await _client.Networks.ConnectNetworkAsync(
//                 networkID,
//                 new NetworkConnectParameters()
//                 {
//                     Container = containerName
//                 }
//             );
//
//             Logger.Log($"{containerName} successfully connected to {networkName}");
//         }
//         catch (Exception ex)
//         {
//             Logger.Log($"{containerName} cannot connected to {networkName}: {ex.Message}");
//         }
//     }
//
//     public async Task StopContainerAsync(string containerID)
//     {
//         try
//         {
//             var stopped = await _client.Containers.StartContainerAsync(
//                 containerID,
//                 new ContainerStartParameters()
//             );
//             Logger.Log($"Container with id {containerID} has stopped");
//         }
//         catch (Exception ex ) 
//         {
//             Logger.Log("Cannot stop container: " + ex.Message);
//         }
//     }
//
//     public async Task<string> EnsureNetworkExistsAsync(string networkName)
//     {
//         try
//         {
//             // check if network existed
//             var networks = await _client.Networks.ListNetworksAsync(new NetworksListParameters());
//             var existingNetwork = networks.FirstOrDefault(n => n.Name == networkName);
//             if (existingNetwork != null)
//             {
//                 Logger.Log($"Network '{networkName}' existed.");
//                 return existingNetwork.ID; // return network ID
//             }
//
//             // create new network if not exist
//             var network = await _client.Networks.CreateNetworkAsync(
//                 new NetworksCreateParameters
//                 {
//                     Name = networkName
//                 }
//             );
//             Logger.Log($"Created network '{networkName}' with ID: {network.ID}");
//             return network.ID;
//         }
//         catch (Exception ex)
//         {
//             Logger.Log($"Error checking network existence: {ex.Message}");
//             return null;
//         }
//     }
//
//     public async Task EnsureImageExistsAsync(string imageName, string imageTag)
//     {
//         try
//         {
//             string fullImageName = $"{imageName}:{imageTag}";
//
//             var images = await _client.Images.ListImagesAsync(new ImagesListParameters { All = true });
//
//             bool imageExists = images.Any(image =>
//                 image.RepoTags != null &&
//                 image.RepoTags.Contains(fullImageName)
//             );
//
//             // if image existed, return
//             if (imageExists)
//             {
//                 Logger.Log($"Image '{fullImageName}' existed.");
//                 return;
//             }
//
//             // if not, pull new image
//             Logger.Log($"Image '{fullImageName}' not found. pulling new image...");
//             await _client.Images.CreateImageAsync(
//                 new ImagesCreateParameters
//                 {
//                     FromImage = imageName,
//                     Tag = imageTag
//                 },
//                 null,
//                 new Progress<JSONMessage>(message =>
//                 {
//                     if (!string.IsNullOrEmpty(message.Status))
//                     {
//                         Logger.Log(message.Status);
//                     }
//                 })
//             );
//
//             Logger.Log($"Image '{fullImageName}' pulled successfully");
//         }
//         catch (Exception ex)
//         {
//             Logger.Log($"Error while checking/pulling image: {ex.Message}");
//         }
//     }
//
// }
