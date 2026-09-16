using System.Diagnostics;
using System.IO;
using System.Text;

namespace JRBSI.Services;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public static class ProcessRunner
{
    public const int WingetAlreadyInstalledExitCode = unchecked((int)0x8B15000B);

    public static void RefreshPathEnvironment()
    {
        var machine = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? string.Empty;
        var user = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
        Environment.SetEnvironmentVariable("PATH", $"{machine};{user}");
    }

    public static string ResolveWingetExecutable()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var aliasPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe");
        if (File.Exists(aliasPath))
        {
            return aliasPath;
        }

        return "winget";
    }

    public static ProcessResult RunProcess(
        string fileName,
        string arguments,
        TimeSpan timeout,
        IProgress<string>? activityProgress = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                outputBuilder.AppendLine(args.Data);
                ReportActivityLine(activityProgress, args.Data);
            }
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                errorBuilder.AppendLine(args.Data);
                ReportActivityLine(activityProgress, args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort cleanup for hung processes.
            }

            throw new TimeoutException($"Process timed out: {fileName} {arguments}");
        }

        var output = outputBuilder.ToString().Trim();
        var error = errorBuilder.ToString().Trim();
        ResourceExtractor.AppendLog($"Command: {fileName} {arguments}");
        ResourceExtractor.AppendLog($"Exit code: {process.ExitCode}");
        if (!string.IsNullOrWhiteSpace(output))
        {
            ResourceExtractor.AppendLog($"Output: {output}");
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            ResourceExtractor.AppendLog($"Error: {error}");
        }

        return new ProcessResult(process.ExitCode, output, error);
    }

    public static ProcessResult RunEmbeddedInstaller(
        string exePath,
        string arguments,
        TimeSpan timeout,
        IProgress<string>? activityProgress = null)
    {
        return RunProcess(exePath, arguments, timeout, activityProgress);
    }

    public static ProcessResult RunWingetInstall(
        string packageName,
        TimeSpan timeout,
        IProgress<string>? activityProgress = null)
    {
        var arguments =
            $"install \"{packageName}\" --silent --accept-source-agreements --accept-package-agreements --disable-interactivity";
        return RunProcess(ResolveWingetExecutable(), arguments, timeout, activityProgress);
    }

    private static void ReportActivityLine(IProgress<string>? activityProgress, string line)
    {
        if (activityProgress is null)
        {
            return;
        }

        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        activityProgress.Report(trimmed.Length <= 120 ? trimmed : $"{trimmed[..117]}...");
    }
}
