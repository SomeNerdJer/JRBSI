using System.IO;
using System.Reflection;

namespace JRBSI.Services;

public static class ResourceExtractor
{
    private static readonly string TempDirectory = Path.Combine(Path.GetTempPath(), "JRBSI");

    public static string ExtractEmbeddedExe(string resourceName, string fileName)
    {
        Directory.CreateDirectory(TempDirectory);

        var destinationPath = Path.Combine(TempDirectory, fileName);
        var assembly = Assembly.GetExecutingAssembly();
        var fullResourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

        if (fullResourceName is null)
        {
            throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
        }

        using var resourceStream = assembly.GetManifestResourceStream(fullResourceName)
            ?? throw new FileNotFoundException($"Unable to read embedded resource: {resourceName}");
        using var fileStream = File.Create(destinationPath);
        resourceStream.CopyTo(fileStream);

        return destinationPath;
    }

    public static string LogFilePath => Path.Combine(TempDirectory, "install.log");

    public static void AppendLog(string message)
    {
        Directory.CreateDirectory(TempDirectory);
        File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
