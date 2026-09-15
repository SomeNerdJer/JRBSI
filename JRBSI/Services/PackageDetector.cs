using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace JRBSI.Services;

public static class PackageDetector
{
    private const string PhoenixTunerPackageId = "9NVV4PWDW27Z";

    public static bool IsNiPackageManagerInstalled()
    {
        if (RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\National Instruments\NI Package Manager") is not null)
        {
            return true;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return File.Exists(Path.Combine(programFiles, "National Instruments", "NI Package Manager", "NIPackageManager.exe"));
    }

    public static bool IsCursorInstalled()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (File.Exists(Path.Combine(localAppData, "Programs", "cursor", "Cursor.exe")))
        {
            return true;
        }

        return RegistryContainsDisplayName("Cursor");
    }

    public static bool IsGoogleChromeInstalled()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (File.Exists(Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe")))
        {
            return true;
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (File.Exists(Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe")))
        {
            return true;
        }

        return RegistryContainsDisplayName("Google Chrome");
    }

    public static bool IsPhoenixTunerInstalled()
    {
        if (RegistryContainsDisplayName("Phoenix Tuner X"))
        {
            return true;
        }

        try
        {
            var result = ProcessRunner.RunProcess(
                "winget",
                $"list --id {PhoenixTunerPackageId}",
                TimeSpan.FromMinutes(2));

            return result.ExitCode == 0 &&
                   result.StandardOutput.Contains(PhoenixTunerPackageId, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsWingetAvailable()
    {
        try
        {
            var result = ProcessRunner.RunProcess("winget", "--version", TimeSpan.FromSeconds(30));
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool RegistryContainsDisplayName(string displayNameFragment)
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
                if (!string.IsNullOrWhiteSpace(displayName) &&
                    displayName.Contains(displayNameFragment, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
