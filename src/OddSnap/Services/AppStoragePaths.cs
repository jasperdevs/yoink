using System.IO;

namespace OddSnap.Services;

internal static class AppStoragePaths
{
    private static readonly string RoamingOddSnapDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OddSnap");

    public static string SettingsPath => Path.Combine(GetStorageDirectory(), "settings.json");
    public static string LogDirectory => Path.Combine(GetStorageDirectory(), "logs");

    public static string ResolveSettingsPath(string? explicitSettingsPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitSettingsPath))
            return Path.GetFullPath(explicitSettingsPath);

        return Path.GetFullPath(SettingsPath);
    }

    internal static string ResolveStorageDirectory(string? runningDirectory, bool isInstalled)
    {
        if (isInstalled || string.IsNullOrWhiteSpace(runningDirectory))
            return RoamingOddSnapDirectory;

        return Path.Combine(Path.GetFullPath(runningDirectory), "OddSnap");
    }

    private static string GetStorageDirectory() =>
        ResolveStorageDirectory(InstallService.GetRunningAppDirectory(), InstallService.IsInstalled());
}
