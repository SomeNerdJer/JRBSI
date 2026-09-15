using System.Text;

namespace JRBSI.Services;

public static class WingetBootstrapper
{
    private static readonly string[] DependencyResourceNames =
    [
        "Microsoft.VCLibs.140.00_x64.appx",
        "Microsoft.VCLibs.140.00.UWPDesktop_x64.appx",
        "Microsoft.WindowsAppRuntime.1.8_x64.appx"
    ];

    public static ProcessResult InstallEmbeddedWinget(TimeSpan timeout)
    {
        if (PackageDetector.IsWingetAvailable())
        {
            return new ProcessResult(0, "winget is already available.", string.Empty);
        }

        var packagePath = ResourceExtractor.ExtractEmbeddedResource(
            "Microsoft.DesktopAppInstaller.msixbundle",
            "Microsoft.DesktopAppInstaller.msixbundle");
        var licensePath = ResourceExtractor.ExtractEmbeddedResource(
            "winget_License1.xml",
            "winget_License1.xml");

        var dependencyPaths = DependencyResourceNames
            .Select(name => ResourceExtractor.ExtractEmbeddedResource(name, name))
            .ToArray();

        // Install framework dependencies for the current user first. Already-present
        // packages are ignored so re-imaging stays idempotent.
        foreach (var dependencyPath in dependencyPaths)
        {
            var depArgs =
                $"-NoProfile -ExecutionPolicy Bypass -Command \"Add-AppxPackage -Path '{EscapeForPowerShell(dependencyPath)}'\"";
            var depResult = ProcessRunner.RunProcess("powershell.exe", depArgs, TimeSpan.FromMinutes(5));
            ResourceExtractor.AppendLog(
                $"Dependency install '{Path.GetFileName(dependencyPath)}' exit {depResult.ExitCode}");
        }

        var dismArgs = new StringBuilder();
        dismArgs.Append("/Online /Add-ProvisionedAppxPackage ");
        dismArgs.Append($"/PackagePath:\"{packagePath}\" ");
        dismArgs.Append($"/LicensePath:\"{licensePath}\"");
        foreach (var dependencyPath in dependencyPaths)
        {
            dismArgs.Append($" /DependencyPackagePath:\"{dependencyPath}\"");
        }

        var provisionResult = ProcessRunner.RunProcess(
            "dism.exe",
            dismArgs.ToString(),
            timeout);

        // Register App Installer for the elevating user so winget.exe resolves immediately.
        ProcessRunner.RunProcess(
            "powershell.exe",
            "-NoProfile -ExecutionPolicy Bypass -Command \"Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe\"",
            TimeSpan.FromMinutes(5));

        ProcessRunner.RefreshPathEnvironment();

        if (!PackageDetector.IsWingetAvailable())
        {
            // Fallback for client SKUs where provisioning alone does not register the alias yet.
            var addArgs =
                $"-NoProfile -ExecutionPolicy Bypass -Command \"Add-AppxPackage -Path '{EscapeForPowerShell(packagePath)}'\"";
            ProcessRunner.RunProcess("powershell.exe", addArgs, TimeSpan.FromMinutes(10));
            ProcessRunner.RefreshPathEnvironment();
        }

        for (var attempt = 0; attempt < 15; attempt++)
        {
            if (PackageDetector.IsWingetAvailable())
            {
                return new ProcessResult(0, "winget installed and verified.", provisionResult.StandardOutput);
            }

            Thread.Sleep(1000);
            ProcessRunner.RefreshPathEnvironment();
        }

        if (provisionResult.ExitCode == 0)
        {
            return new ProcessResult(
                1,
                provisionResult.StandardOutput,
                "App Installer provisioned but winget is still not available on PATH.");
        }

        var detail = string.IsNullOrWhiteSpace(provisionResult.StandardError)
            ? provisionResult.StandardOutput
            : provisionResult.StandardError;
        return new ProcessResult(provisionResult.ExitCode, provisionResult.StandardOutput, detail);
    }

    private static string EscapeForPowerShell(string path)
    {
        return path.Replace("'", "''", StringComparison.Ordinal);
    }
}
