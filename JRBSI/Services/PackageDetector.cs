using System.IO;
using Microsoft.Win32;

namespace JRBSI.Services;

public static class PackageDetector
{
    public readonly record struct InstallDetection(bool IsInstalled, string? InstallPath);

    public sealed class RegistryDisplayNameCache
    {
        private readonly List<(string DisplayName, string? InstallPath)> _entries = [];

        public static RegistryDisplayNameCache Load()
        {
            var cache = new RegistryDisplayNameCache();
            cache.LoadEntries();
            return cache;
        }

        public bool ContainsDisplayNameFragment(string displayNameFragment)
        {
            return _entries.Any(entry =>
                entry.DisplayName.Contains(displayNameFragment, StringComparison.OrdinalIgnoreCase));
        }

        public string? FindInstallPath(string displayNameFragment)
        {
            foreach (var entry in _entries)
            {
                if (!entry.DisplayName.Contains(displayNameFragment, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.InstallPath))
                {
                    return entry.InstallPath;
                }
            }

            return null;
        }

        private void LoadEntries()
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var uninstallKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstallKey is null)
                {
                    continue;
                }

                foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                {
                    using var subKey = uninstallKey.OpenSubKey(subKeyName);
                    var displayName = subKey?.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        continue;
                    }

                    _entries.Add((displayName, ResolveInstallPath(subKey)));
                }
            }
        }
    }

    public static InstallDetection DetectNiPackageManager()
    {
        var installDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "National Instruments",
            "NI Package Manager");
        var exePath = Path.Combine(installDir, "NIPackageManager.exe");

        if (File.Exists(exePath))
        {
            return new InstallDetection(true, exePath);
        }

        if (RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\National Instruments\NI Package Manager") is not null)
        {
            return new InstallDetection(true, installDir);
        }

        return new InstallDetection(false, null);
    }

    public static InstallDetection DetectCursor(RegistryDisplayNameCache? registryCache = null)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var exePath = Path.Combine(localAppData, "Programs", "cursor", "Cursor.exe");
        if (File.Exists(exePath))
        {
            return new InstallDetection(true, exePath);
        }

        var registryPath = registryCache?.FindInstallPath("Cursor") ??
                           FindInstallPathInRegistry("Cursor");
        if (registryPath is not null ||
            (registryCache?.ContainsDisplayNameFragment("Cursor") ?? ContainsDisplayNameFragment("Cursor")))
        {
            return new InstallDetection(true, registryPath);
        }

        return new InstallDetection(false, null);
    }

    public static InstallDetection DetectGoogleChrome(RegistryDisplayNameCache? registryCache = null)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var exePath = Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe");
        if (File.Exists(exePath))
        {
            return new InstallDetection(true, exePath);
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var exePathX86 = Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe");
        if (File.Exists(exePathX86))
        {
            return new InstallDetection(true, exePathX86);
        }

        var registryPath = registryCache?.FindInstallPath("Google Chrome") ??
                           FindInstallPathInRegistry("Google Chrome");
        if (registryPath is not null ||
            (registryCache?.ContainsDisplayNameFragment("Google Chrome") ??
             ContainsDisplayNameFragment("Google Chrome")))
        {
            return new InstallDetection(true, registryPath);
        }

        return new InstallDetection(false, null);
    }

    public static InstallDetection DetectPhoenixTuner(RegistryDisplayNameCache? registryCache = null)
    {
        var registryPath = registryCache?.FindInstallPath("Phoenix Tuner X") ??
                           FindInstallPathInRegistry("Phoenix Tuner X");
        if (registryPath is not null ||
            (registryCache?.ContainsDisplayNameFragment("Phoenix Tuner X") ??
             ContainsDisplayNameFragment("Phoenix Tuner X")))
        {
            return new InstallDetection(true, registryPath);
        }

        return new InstallDetection(false, null);
    }

    public static InstallDetection DetectWinget()
    {
        if (!IsWingetAvailable())
        {
            return new InstallDetection(false, null);
        }

        var wingetPath = ProcessRunner.ResolveWingetExecutable();
        return new InstallDetection(true, wingetPath);
    }

    public static bool IsNiPackageManagerInstalled() => DetectNiPackageManager().IsInstalled;

    public static bool IsCursorInstalled(RegistryDisplayNameCache? registryCache = null) =>
        DetectCursor(registryCache).IsInstalled;

    public static bool IsGoogleChromeInstalled(RegistryDisplayNameCache? registryCache = null) =>
        DetectGoogleChrome(registryCache).IsInstalled;

    public static bool IsPhoenixTunerInstalled(RegistryDisplayNameCache? registryCache = null) =>
        DetectPhoenixTuner(registryCache).IsInstalled;

    public static bool IsWingetAvailable()
    {
        try
        {
            var result = ProcessRunner.RunProcess(
                ProcessRunner.ResolveWingetExecutable(),
                "--version",
                TimeSpan.FromSeconds(30));
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static string FormatDetectedInstallMessage(string? installPath)
    {
        return string.IsNullOrWhiteSpace(installPath)
            ? "Detected existing installation."
            : $"Installed at: {installPath}";
    }

    private static bool ContainsDisplayNameFragment(string displayNameFragment)
    {
        return RegistryDisplayNameCache.Load().ContainsDisplayNameFragment(displayNameFragment);
    }

    private static string? FindInstallPathInRegistry(string displayNameFragment)
    {
        return RegistryDisplayNameCache.Load().FindInstallPath(displayNameFragment);
    }

    private static string? ResolveInstallPath(RegistryKey subKey)
    {
        var installLocation = subKey.GetValue("InstallLocation") as string;
        if (!string.IsNullOrWhiteSpace(installLocation))
        {
            return installLocation.Trim().TrimEnd('\\', '"');
        }

        var displayIcon = subKey.GetValue("DisplayIcon") as string;
        if (!string.IsNullOrWhiteSpace(displayIcon))
        {
            return ExtractExecutablePath(displayIcon);
        }

        var uninstallString = subKey.GetValue("UninstallString") as string;
        if (!string.IsNullOrWhiteSpace(uninstallString))
        {
            return ExtractExecutablePath(uninstallString);
        }

        return null;
    }

    private static string? ExtractExecutablePath(string value)
    {
        var trimmed = value.Trim().Trim('"');
        if (trimmed.Length == 0)
        {
            return null;
        }

        var commaIndex = trimmed.IndexOf(',', StringComparison.Ordinal);
        if (commaIndex > 0)
        {
            trimmed = trimmed[..commaIndex].Trim().Trim('"');
        }

        if (File.Exists(trimmed))
        {
            return trimmed;
        }

        if (Directory.Exists(trimmed))
        {
            return trimmed;
        }

        return trimmed.Contains('\\') || trimmed.Contains('/')
            ? trimmed
            : null;
    }
}
