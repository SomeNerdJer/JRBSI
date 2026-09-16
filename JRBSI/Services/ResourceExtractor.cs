using System.IO;
using System.Reflection;

namespace JRBSI.Services;

public static class ResourceExtractor
{
    private static readonly string TempDirectory = Path.Combine(Path.GetTempPath(), "JRBSI");

    public static string ExtractEmbeddedExe(string resourceName, string fileName)
    {
        return ExtractEmbeddedResource(resourceName, fileName);
    }

    public static string ExtractEmbeddedResource(string resourceName, string fileName)
    {
        var runDirectory = Path.Combine(TempDirectory, "runs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runDirectory);

        var destinationPath = Path.Combine(runDirectory, fileName);
        var assembly = Assembly.GetExecutingAssembly();
        var fullResourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

        if (fullResourceName is null)
        {
            throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
        }

        try
        {
            using var resourceStream = assembly.GetManifestResourceStream(fullResourceName)
                ?? throw new FileNotFoundException($"Unable to read embedded resource: {resourceName}");
            using var fileStream = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            resourceStream.CopyTo(fileStream);
        }
        catch (IOException ex)
        {
            throw new IOException(
                $"Could not extract {fileName}. Close any running installers or other JRBSI windows and try again.",
                ex);
        }

        if (!IsValidWindowsExecutable(destinationPath))
        {
            throw new InvalidDataException(
                $"{fileName} is not a valid Windows installer. " +
                "The embedded file may be a Git LFS pointer instead of the actual installer.");
        }

        return destinationPath;
    }

    public static bool IsValidWindowsExecutable(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length < 1024)
        {
            var prefix = File.ReadAllText(path);
            if (prefix.StartsWith("version https://git-lfs.github.com/spec/v1", StringComparison.Ordinal))
            {
                return false;
            }
        }

        Span<byte> header = stackalloc byte[2];
        using var stream = File.OpenRead(path);
        return stream.Read(header) == 2 && header[0] == 0x4D && header[1] == 0x5A;
    }

    public static string LogFilePath => Path.Combine(TempDirectory, "install.log");

    public static void AppendLog(string message)
    {
        Directory.CreateDirectory(TempDirectory);
        File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
