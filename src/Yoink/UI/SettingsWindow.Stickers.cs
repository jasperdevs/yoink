using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Yoink.Helpers;
using Yoink.Models;
using Yoink.Services;

namespace Yoink.UI;

public partial class SettingsWindow
{
    private void StickerProviderCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveStickerSettings.Provider = (StickerProvider)StickerProviderCombo.SelectedIndex;
        UpdateStickerProviderVisibility();
        UpdateLocalEngineUi();
        _settingsService.Save();
    }

    private void StickerLocalCpuEngineCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateLocalEngineUi();
    }

    private void StickerLocalGpuEngineCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateLocalEngineUi();
    }

    private void StickerLocalExecutionCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateStickerExecutionUi();
    }

    private LocalStickerEngine GetSelectedLocalStickerEngine()
    {
        var executionProvider = (StickerExecutionProvider)StickerLocalExecutionCombo.SelectedIndex;
        return executionProvider == StickerExecutionProvider.Gpu
            ? GetSelectedStickerEngine(StickerLocalGpuEngineCombo)
            : GetSelectedStickerEngine(StickerLocalCpuEngineCombo);
    }

    private async void StickerInstallDriversBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var executionProvider = (StickerExecutionProvider)StickerLocalExecutionCombo.SelectedIndex;
            StickerInstallDriversBtn.IsEnabled = false;
            await RembgRuntimeService.EnsureInstalledAsync(executionProvider);
            ToastWindow.Show("rembg", $"{RembgRuntimeService.GetSetupTargetName(executionProvider)} complete.");
        }
        catch (Exception ex)
        {
            ToastWindow.ShowError("rembg setup failed", ex.Message);
        }
        finally
        {
            StickerInstallDriversBtn.IsEnabled = true;
        }
    }

    private void StickerShadowCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveStickerSettings.AddShadow = StickerShadowCheck.IsChecked == true;
        _settingsService.Save();
    }

    private void StickerStrokeCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveStickerSettings.AddStroke = StickerStrokeCheck.IsChecked == true;
        _settingsService.Save();
    }

    private void StickerRemoveBgKeyBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveStickerSettings.RemoveBgApiKey = StickerRemoveBgKeyBox.Text;
        _settingsService.Save();
    }

    private void StickerPhotoroomKeyBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveStickerSettings.PhotoroomApiKey = StickerPhotoroomKeyBox.Text;
        _settingsService.Save();
    }

    private async void StickerDownloadRembgBtn_Click(object sender, RoutedEventArgs e)
    {
        var engine = GetSelectedLocalStickerEngine();

        if (LocalStickerEngineService.IsModelDownloaded(engine))
        {
            bool removed = LocalStickerEngineService.RemoveDownloadedModel(engine);
            SetStickerDownloadUi(false, null, removed ? "Model removed." : "Couldn't remove the model.");
            if (removed)
                ToastWindow.Show("Sticker engine", "Removed the local sticker model.");
            else
                ToastWindow.ShowError("Sticker engine error", "Couldn't remove the local sticker model.");
            UpdateLocalEngineUi();
            return;
        }

        SetStickerDownloadUi(true, 0, "Preparing download...");
        try
        {
            var progress = new Progress<LocalStickerEngineDownloadProgress>(p =>
            {
                SetStickerDownloadUi(true, p.TotalBytes is > 0 ? p.Percent : null, p.StatusMessage);
            });

            var result = await LocalStickerEngineService.DownloadModelAsync(engine, progress);
            if (!result.Success || string.IsNullOrWhiteSpace(result.ModelPath))
            {
                SetStickerDownloadUi(false, null, result.Message);
                ToastWindow.ShowError("Sticker engine error", result.Message);
                return;
            }

            SetStickerDownloadUi(false, 100, "Download complete. The model is ready to use.");
            ToastWindow.Show("Sticker engine", $"Downloaded {LocalStickerEngineService.GetEngineLabel(engine)}. Sticker captures will now use it automatically.");
            UpdateLocalEngineUi();
        }
        catch (Exception ex)
        {
            SetStickerDownloadUi(false, null, ex.Message);
            ToastWindow.ShowError("Sticker engine error", ex.Message);
        }
    }

    private void StickerOpenLocalEngineRepoBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var engine = GetSelectedLocalStickerEngine();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = LocalStickerEngineService.GetProjectUrl(engine),
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void StickerRemoveAllModelsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Remove all downloaded local sticker models?\n\nThey will be downloaded again the next time you use them.",
                "Remove Models",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        bool removed = RembgRuntimeService.RemoveAllCachedModels();
        if (removed)
        {
            ToastWindow.Show("Sticker engine", "Removed all downloaded local sticker models.");
            UpdateLocalEngineUi();
        }
        else
        {
            ToastWindow.ShowError("Sticker engine error", "Couldn't remove the downloaded models.");
        }
    }
}
