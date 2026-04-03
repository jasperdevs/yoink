using System.Diagnostics;
using System.IO;
using System.Windows;
using Yoink.Services;

namespace Yoink.UI;

public partial class SettingsWindow
{
    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshUpdateStatusAsync(true);
    }

    private async void DownloadUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_latestUpdate is null)
            return;

        if (!CanInstallUpdate(_latestUpdate))
        {
            OpenExternalUrl(_latestUpdate.ReleaseUrl);
            return;
        }

        await InstallUpdateAsync();
    }

    private async Task RefreshUpdateStatusAsync(bool isManualCheck)
    {
        if (_updateCheckInFlight)
            return;

        _updateCheckInFlight = true;
        CheckUpdatesButton.IsEnabled = false;
        CheckUpdatesButton.Content = "Checking...";
        DownloadUpdateButton.Visibility = Visibility.Collapsed;
        UpdateStatusText.Text = "Checking GitHub releases...";
        UpdateDetailText.Text = "Looking for the newest production build.";

        try
        {
            _latestUpdate = await UpdateService.CheckForUpdatesAsync(forceRefresh: isManualCheck);
            UpdateStatusText.Text = _latestUpdate.StatusMessage;

            if (_latestUpdate.IsUpdateAvailable)
            {
                var published = _latestUpdate.PublishedAt.HasValue
                    ? $"Published {FormatTimeAgo(_latestUpdate.PublishedAt.Value.LocalDateTime)}"
                    : "Published recently";
                UpdateDetailText.Text = $"Current build: {UpdateService.GetCurrentVersionLabel()}. {published}. Yoink will update itself and restart.";
                DownloadUpdateButton.Content = CanInstallUpdate(_latestUpdate)
                    ? "Update now"
                    : "Open release";
                DownloadUpdateButton.Visibility = Visibility.Visible;
            }
            else
            {
                UpdateDetailText.Text = $"Current build: {UpdateService.GetCurrentVersionLabel()}";
                if (isManualCheck)
                    ToastWindow.Show("Yoink is up to date", UpdateService.GetCurrentVersionLabel());
            }
        }
        catch (Exception ex)
        {
            _latestUpdate = null;
            UpdateStatusText.Text = "Update check failed";
            UpdateDetailText.Text = ex.Message;
            if (isManualCheck)
                ToastWindow.ShowError("Update check failed", ex.Message);
        }
        finally
        {
            _updateCheckInFlight = false;
            CheckUpdatesButton.IsEnabled = true;
            CheckUpdatesButton.Content = "Check now";
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (_latestUpdate is null)
            return;

        if (_updateCheckInFlight)
            return;

        _updateCheckInFlight = true;
        CheckUpdatesButton.IsEnabled = false;
        DownloadUpdateButton.IsEnabled = false;
        CheckUpdatesButton.Content = "Checking...";
        DownloadUpdateButton.Content = "Updating...";
        UpdateStatusText.Text = "Downloading update...";
        UpdateDetailText.Text = "Yoink will download the update, apply it, and relaunch automatically.";

        try
        {
            var packagePath = await UpdateService.DownloadUpdatePackageAsync(_latestUpdate);
            var targetDir = InstallService.GetInstalledLocation() ?? InstallService.GetRunningAppDirectory();
            var helperPath = CreateUpdateHelper();
            StartUpdateHelper(helperPath, packagePath, targetDir, _latestUpdate.LatestVersionLabel);
            ToastWindow.Show("Updating Yoink", "Yoink will close, update, and reopen.");
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = "Update install failed";
            UpdateDetailText.Text = ex.Message;
            ToastWindow.ShowError("Update install failed", ex.Message);
        }
        finally
        {
            _updateCheckInFlight = false;
            CheckUpdatesButton.IsEnabled = true;
            DownloadUpdateButton.IsEnabled = true;
            CheckUpdatesButton.Content = "Check now";
        }
    }

    private static void OpenExternalUrl(string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private static bool CanInstallUpdate(UpdateCheckResult update)
    {
        return !string.IsNullOrWhiteSpace(update.DownloadUrl) &&
               !string.IsNullOrWhiteSpace(update.AssetName) &&
               string.Equals(Path.GetExtension(update.AssetName), ".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateUpdateHelper()
    {
        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExe))
            throw new InvalidOperationException("Unable to locate the running Yoink executable.");

        var helperDir = Path.Combine(Path.GetTempPath(), "Yoink", "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(helperDir);
        var helperPath = Path.Combine(helperDir, "Yoink-Updater.exe");
        File.Copy(currentExe, helperPath, true);
        return helperPath;
    }

    private static void StartUpdateHelper(string helperPath, string packagePath, string targetDir, string versionLabel)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = helperPath,
            WorkingDirectory = Path.GetDirectoryName(helperPath) ?? Path.GetTempPath(),
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--apply-update");
        startInfo.ArgumentList.Add(packagePath);
        startInfo.ArgumentList.Add(targetDir);
        startInfo.ArgumentList.Add(versionLabel);

        if (Process.Start(startInfo) is null)
            throw new InvalidOperationException("Failed to launch the update helper.");
    }
}
