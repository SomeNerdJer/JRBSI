using JRBSI.Models;

namespace JRBSI.Services;

public sealed class InstallOrchestrator
{
    public const string WpilibUrl =
        "https://docs.wpilib.org/en/stable/docs/zero-to-robot/step-2/wpilib-setup.html";

    public const string WaitingForWingetMessage = "Will be installed after winget is available";

    public IReadOnlyList<InstallItem> CreateInstallItems()
    {
        return
        [
            new InstallItem
            {
                Name = "Winget",
                Method = InstallMethod.EmbeddedWinget
            },
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

    public void ScanInstalledPackages(
        IEnumerable<InstallItem> items,
        Action<Action>? uiInvoker = null,
        IProgress<string>? scanProgress = null)
    {
        scanProgress?.Report("Checking winget...");
        var wingetAvailable = PackageDetector.IsWingetAvailable();
        var registryCache = PackageDetector.RegistryDisplayNameCache.Load();

        foreach (var item in items)
        {
            if (item.Status != InstallStatus.Pending)
            {
                continue;
            }

            scanProgress?.Report($"Checking {item.Name}...");

            var alreadyInstalled = item.Name switch
            {
                "Winget" => wingetAvailable,
                "NI Package Manager" => PackageDetector.IsNiPackageManagerInstalled(),
                "Cursor" => PackageDetector.IsCursorInstalled(registryCache),
                "Google Chrome" => PackageDetector.IsGoogleChromeInstalled(registryCache),
                "Phoenix Tuner X" => PackageDetector.IsPhoenixTunerInstalled(registryCache),
                _ => false
            };

            if (alreadyInstalled)
            {
                RunOnUi(uiInvoker, () =>
                {
                    item.Status = InstallStatus.AlreadyInstalled;
                    item.DetailMessage = "Detected existing installation.";
                });
                continue;
            }

            if (item.Method == InstallMethod.Winget && !wingetAvailable)
            {
                RunOnUi(uiInvoker, () =>
                {
                    item.DetailMessage = WaitingForWingetMessage;
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
                    item.DetailMessage = "Starting...";
                });

                var activity = CreateItemActivity(item, uiInvoker);

                try
                {
                    var result = item.Method switch
                    {
                        InstallMethod.EmbeddedExe => InstallEmbeddedPackage(item, activity),
                        InstallMethod.EmbeddedWinget => InstallWingetBootstrap(activity),
                        InstallMethod.Winget => InstallWingetPackage(item, activity),
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

    private static IProgress<string> CreateItemActivity(InstallItem item, Action<Action>? uiInvoker)
    {
        return new Progress<string>(message =>
            RunOnUi(uiInvoker, () => item.DetailMessage = message));
    }

    private sealed record InstallResult(InstallStatus Status, string DetailMessage);

    private static InstallResult InstallEmbeddedPackage(InstallItem item, IProgress<string> activity)
    {
        if (string.IsNullOrWhiteSpace(item.EmbeddedResourceName) ||
            string.IsNullOrWhiteSpace(item.ExeFileName))
        {
            return new InstallResult(InstallStatus.Failed, "Missing embedded installer configuration.");
        }

        activity.Report("Extracting installer...");
        var exePath = ResourceExtractor.ExtractEmbeddedExe(item.EmbeddedResourceName, item.ExeFileName);
        activity.Report($"Running {item.ExeFileName} (this may take several minutes)...");

        var result = ProcessRunner.RunEmbeddedInstaller(
            exePath,
            item.Arguments ?? string.Empty,
            TimeSpan.FromHours(2),
            activity);

        if (result.ExitCode == 0)
        {
            return new InstallResult(InstallStatus.Installed, "Installation finished successfully.");
        }

        var detail = string.IsNullOrWhiteSpace(result.StandardError)
            ? $"Installer exited with code {result.ExitCode}."
            : result.StandardError;
        return new InstallResult(InstallStatus.Failed, detail);
    }

    private static InstallResult InstallWingetBootstrap(IProgress<string> activity)
    {
        if (PackageDetector.IsWingetAvailable())
        {
            return new InstallResult(InstallStatus.AlreadyInstalled, "Detected existing installation.");
        }

        var result = WingetBootstrapper.InstallEmbeddedWinget(TimeSpan.FromMinutes(20), activity);
        if (result.ExitCode == 0 && PackageDetector.IsWingetAvailable())
        {
            return new InstallResult(InstallStatus.Installed, "Installation finished successfully.");
        }

        if (PackageDetector.IsWingetAvailable())
        {
            return new InstallResult(InstallStatus.Installed, "winget is available.");
        }

        var detail = string.IsNullOrWhiteSpace(result.StandardError)
            ? $"Winget bootstrap exited with code {result.ExitCode}."
            : result.StandardError;
        return new InstallResult(InstallStatus.Failed, detail);
    }

    private static InstallResult InstallWingetPackage(InstallItem item, IProgress<string> activity)
    {
        if (string.IsNullOrWhiteSpace(item.WingetPackageName))
        {
            return new InstallResult(InstallStatus.Failed, "Missing winget package name.");
        }

        if (!PackageDetector.IsWingetAvailable())
        {
            return new InstallResult(InstallStatus.Failed, "winget is not available on this system.");
        }

        activity.Report($"Running winget install \"{item.WingetPackageName}\"...");

        var result = ProcessRunner.RunWingetInstall(item.WingetPackageName, TimeSpan.FromMinutes(30), activity);

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
