using System.Diagnostics;
using System.Text;

namespace JRBSI.Services;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public static class ProcessRunner
{
    public const int WingetAlreadyInstalledExitCode = unchecked((int)0x8B15000B);

    public static ProcessResult RunProcess(string fileName, string arguments, TimeSpan timeout)
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
            }
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                errorBuilder.AppendLine(args.Data);
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

    public static ProcessResult RunEmbeddedInstaller(string exePath, string arguments, TimeSpan timeout)
    {
        return RunProcess(exePath, arguments, timeout);
    }

    public static ProcessResult RunWingetInstall(string packageName, TimeSpan timeout)
    {
        var arguments =
            $"install \"{packageName}\" --silent --accept-source-agreements --accept-package-agreements --disable-interactivity";
        return RunProcess("winget", arguments, timeout);
    }
}
