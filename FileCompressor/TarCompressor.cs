using ICSharpCode.SharpZipLib.Tar;

namespace FileCompressor;

public class TarCompressor
{
    public static string CreateTarFile(string filePath)
    {
        try
        {
            string outputTarPath = Path.ChangeExtension(filePath, ".tar");

            // create output directory if not exists
            string outputDir = Path.GetDirectoryName(outputTarPath);
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // create tar file
            using (var tarOutputStream = new TarOutputStream(File.Create(outputTarPath)))
            {
                AddFileToTar(tarOutputStream, filePath, Path.GetFileName(filePath));
            }

            return outputTarPath;
        }
        catch (Exception ex)
        {
            throw new IOException("Could not create tar file.", ex);
        }
    }

    public static string CreateTarFile(string[] filePaths)
    {
        try
        {
            if (filePaths.Length == 0)
            {
                throw new ArgumentException("At least one file must be specified.");
            }

            string outputTarPath = Path.ChangeExtension(filePaths[0], ".tar");

            // create output directory if not exists
            string outputDir = Path.GetDirectoryName(outputTarPath);
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // create tar file
            using (var tarOutputStream = new TarOutputStream(File.Create(outputTarPath)))
            {
                foreach (var filePath in filePaths)
                {
                    AddFileToTar(tarOutputStream, filePath, Path.GetFileName(filePath));
                }
            }

            return outputTarPath;
        }
        catch (Exception ex)
        {
            throw new IOException("Could not create tar file.", ex);
        }
    }

    private static void AddFileToTar(TarOutputStream tarOutputStream, string filePath, string entryName)
    {
        using (var fileStream = File.OpenRead(filePath))
        {
            var tarEntry = TarEntry.CreateTarEntry(entryName); // file name in tar
            tarEntry.Size = fileStream.Length; // file size

            tarOutputStream.PutNextEntry(tarEntry);
            fileStream.CopyTo(tarOutputStream); // write data into tar
            tarOutputStream.CloseEntry();
        }
    }
}