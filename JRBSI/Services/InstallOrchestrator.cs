using JRBSI.Models;

namespace JRBSI.Services;

public sealed class InstallOrchestrator
{
    public const string WpilibUrl =
        "https://docs.wpilib.org/en/stable/docs/zero-to-robot/step-2/wpilib-setup.html";

    public IReadOnlyList<InstallItem> CreateInstallItems()
    {
        return
        [
            new InstallItem
            {
                Name = "NI Package Manager",
                Method = InstallMethod.EmbeddedExe,
                EmbeddedResourceName = "nitools.exe",
                ExeFileName = "nitools.exe",
                Arguments = "--quiet --accept-eulas --prevent-reboot"
            },
            new InstallItem
            {
                Name = "Cursor",
                Method = InstallMethod.EmbeddedExe,
                EmbeddedResourceName = "CursorSetup.exe",
                ExeFileName = "CursorSetup.exe",
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART"
            },
            new InstallItem
            {
                Name = "Google Chrome",
                Method = InstallMethod.EmbeddedExe,
                EmbeddedResourceName = "ChromeSetup.exe",
                ExeFileName = "ChromeSetup.exe",
                Arguments = "/silent /install"
            },
            new InstallItem
            {
                Name = "Phoenix Tuner X",
                Method = InstallMethod.Winget,
                WingetPackageId = "9NVV4PWDW27Z",
                WingetPackageName = "Phoenix Tuner X"
            }
        ];
    }

    public void ScanInstalledPackages(IEnumerable<InstallItem> items, Action<Action>? uiInvoker = null)
    {
        foreach (var item in items)
        {
            if (item.Status != InstallStatus.Pending)
            {
                continue;
            }

            var alreadyInstalled = item.Name switch
            {
                "NI Package Manager" => PackageDetector.IsNiPackageManagerInstalled(),
                "Cursor" => PackageDetector.IsCursorInstalled(),
                "Google Chrome" => PackageDetector.IsGoogleChromeInstalled(),
                "Phoenix Tuner X" => PackageDetector.IsPhoenixTunerInstalled(),
                _ => false
            };

            if (alreadyInstalled)
            {
                RunOnUi(uiInvoker, () =>
                {
                    item.Status = InstallStatus.AlreadyInstalled;
                    item.DetailMessage = "Detected existing installation.";
                });
            }
        }
    }

    public Task RunInstallAsync(
        IReadOnlyList<InstallItem> items,
        IProgress<string>? summaryProgress = null,
        Action<Action>? uiInvoker = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var total = items.Count;
            var current = 0;

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                current++;
                summaryProgress?.Report($"Installing {current} of {total}: {item.Name}...");

                if (item.Status is InstallStatus.AlreadyInstalled or InstallStatus.Installed)
                {
                    continue;
                }

                RunOnUi(uiInvoker, () =>
                {
                    item.Status = InstallStatus.Installing;
                    item.DetailMessage = string.Empty;
                });

                try
                {
                    var result = item.Method switch
                    {
                        InstallMethod.EmbeddedExe => InstallEmbeddedPackage(item),
                        InstallMethod.Winget => InstallWingetPackage(item),
                        _ => new InstallResult(InstallStatus.Failed, "Unknown install method.")
                    };

                    RunOnUi(uiInvoker, () =>
                    {
                        item.Status = result.Status;
                        item.DetailMessage = result.DetailMessage;
                    });
                }
                catch (Exception ex)
                {
                    RunOnUi(uiInvoker, () =>
                    {
                        item.Status = InstallStatus.Failed;
                        item.DetailMessage = ex.Message;
                    });
                    ResourceExtractor.AppendLog($"Exception installing {item.Name}: {ex}");
                }
            }

            summaryProgress?.Report("Complete");
        }, cancellationToken);
    }

    private static void RunOnUi(Action<Action>? uiInvoker, Action action)
    {
        if (uiInvoker is null)
        {
            action();
            return;
        }

        uiInvoker(action);
    }

    private sealed record InstallResult(InstallStatus Status, string DetailMessage);

    private static InstallResult InstallEmbeddedPackage(InstallItem item)
    {
        if (string.IsNullOrWhiteSpace(item.EmbeddedResourceName) ||
            string.IsNullOrWhiteSpace(item.ExeFileName))
        {
            return new InstallResult(InstallStatus.Failed, "Missing embedded installer configuration.");
        }

        var exePath = ResourceExtractor.ExtractEmbeddedExe(item.EmbeddedResourceName, item.ExeFileName);
        var result = ProcessRunner.RunEmbeddedInstaller(
            exePath,
            item.Arguments ?? string.Empty,
            TimeSpan.FromHours(2));

        if (result.ExitCode == 0)
        {
            return new InstallResult(InstallStatus.Installed, "Installation finished successfully.");
        }

        var detail = string.IsNullOrWhiteSpace(result.StandardError)
            ? $"Installer exited with code {result.ExitCode}."
            : result.StandardError;
        return new InstallResult(InstallStatus.Failed, detail);
    }

    private static InstallResult InstallWingetPackage(InstallItem item)
    {
        if (string.IsNullOrWhiteSpace(item.WingetPackageName))
        {
            return new InstallResult(InstallStatus.Failed, "Missing winget package name.");
        }

        if (!PackageDetector.IsWingetAvailable())
        {
            return new InstallResult(InstallStatus.Failed, "winget is not available on this system.");
        }

        var result = ProcessRunner.RunWingetInstall(item.WingetPackageName, TimeSpan.FromMinutes(30));

        if (result.ExitCode == 0)
        {
            return new InstallResult(InstallStatus.Installed, "Installation finished successfully.");
        }

        if (result.ExitCode == ProcessRunner.WingetAlreadyInstalledExitCode ||
            result.StandardOutput.Contains("already installed", StringComparison.OrdinalIgnoreCase) ||
            result.StandardError.Contains("already installed", StringComparison.OrdinalIgnoreCase))
        {
            return new InstallResult(InstallStatus.AlreadyInstalled, "Package is already installed.");
        }

        var detail = string.IsNullOrWhiteSpace(result.StandardError)
            ? $"winget exited with code {result.ExitCode}."
            : result.StandardError;
        return new InstallResult(InstallStatus.Failed, detail);
    }
}
