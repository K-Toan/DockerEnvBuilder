using Logger;
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

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // create container configs
        string mssqlContainerId = await docker.CreateContainerAsync(
            containerName: "test-mssql-container",
            imageName: "mcr.microsoft.com/mssql/server:2019-latest",
            containerPort: 1433,
            hostPort: 1433,
            envParams: ["ACCEPT_EULA=Y", "SA_PASSWORD=Auto@anhlh"],
            volumeName: "test-mssql-volume:/var/opt/mssql",
            networkName: "test-network"
        );
        
        string tomcatContainerId = await docker.CreateContainerAsync(
            containerName: "test-tomcat-container",
            imageName:"tomcat:latest",
            containerPort: 8080,
            hostPort: 8080,
            envParams: [],
            volumeName: "test-tomcat-volume:/usr/local/tomcat/webapps/",
            networkName:"test-network"
        );

        // run container
        await docker.StartContainerAsync(mssqlContainerId);
        await docker.StartContainerAsync(tomcatContainerId);
        
        // copy files to volume
        string copiedSqlFile = await docker.CopyToContainerAsync(
            mssqlContainerId,
            @"D:/Temps/cp/Solution/Meta/SamplePE.sql",
            @"/var/opt/mssql/"
        );
        
        string copiedWarFile = await docker.CopyToContainerAsync(
            tomcatContainerId,
            @"D:/Temps/cp/Solution/StudentProjects/he171478/Q3_HE171478.war",
            @"/usr/local/tomcat/webapps/"
        );

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        logger.Log($"Waiting 10s for .war file to be extracted, then copy ConnectDB.properties into it");
        await Task.Delay(10000);
        
        string copiedConnectionFile = await docker.CopyToContainerAsync(
            tomcatContainerId,
            @"D:/Temps/cp/Solution/Meta/ConnectDB.properties",
            @$"{(Path.GetDirectoryName(copiedWarFile) + "/" + Path.GetFileNameWithoutExtension(copiedWarFile))}/WEB-INF/".Replace("\\", "/")
        ); 
        
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        logger.Log($"Waiting 10s, then exec {copiedSqlFile} on mssql");
        await Task.Delay(10000);
        
        await docker.ExecCommandsAsync(
            containerId: mssqlContainerId,
            commands: [
                "/opt/mssql-tools18/bin/sqlcmd",
                "-C",
                "-S", "localhost",
                "-U", "sa",
                "-P", "Auto@anhlh",
                "-i", copiedSqlFile
            ],
            user: "root"
        );

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        logger.Log($"Press any key to remove container and its volumes...");
        Console.ReadKey();
        
        // stop container
        await docker.StopContainerAsync(mssqlContainerId);
        await docker.StopContainerAsync(tomcatContainerId);

        // remove 
        await docker.RemoveContainerAsync(mssqlContainerId, true, true);
        await docker.RemoveContainerAsync(tomcatContainerId, true, true);

    }
}