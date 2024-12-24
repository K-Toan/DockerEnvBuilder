﻿using Logger;
using DockerEnvironmentWrapper.Helpers;

namespace DockerEnvironmentWrapper;

class Program
{
    static async Task Main(string[] args)
    {
        var dockerClientWrapper = new DockerClientWrapper();
        var logger = new ConsoleLogger();
        var client = dockerClientWrapper.GetClient();

        var docker = new DockerResourceManager(client, logger);

        string networkId = await docker.EnsureNetworkExistsAsync("test-network");

        // Tạo volume
        // string volumeName = await volumeManager.EnsureVolumeExistsAsync("test-volume:/var/opt/mssql");

        // Tạo container
        string mssqlContainerId = await docker.CreateContainerAsync(
            containerName: "test-mssql-container",
            imageName: "mcr.microsoft.com/mssql/server:2019-latest",
            containerPort: 1433,
            hostPort: 1433,
            envParams: ["ACCEPT_EULA=Y", "MSSQL_SA_PASSWORD=Auto@anhlh"],
            volumeName: "test-mssql-volume:/var/opt/mssql",
            networkName: "test-network"
        );

        // run container
        await docker.StartContainerAsync(mssqlContainerId);
        
        // copy some file to volume
        await docker.CopyToContainerAsync(
            mssqlContainerId,
            @"D:\Configs\tar.gz",
            "/var/opt/mssql"
        );
        
        // stop container
        await Task.Delay(3000);
        await docker.StopContainerAsync(mssqlContainerId);

        // remove
        await Task.Delay(3000);
        await docker.RemoveVolumeAsync("test-mssql-volume");
        await docker.RemoveContainerAsync(mssqlContainerId);

        Console.ReadKey();
    }
}