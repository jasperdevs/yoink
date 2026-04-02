using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using Yoink.Helpers;
using Yoink.Models;
using Yoink.Services;

namespace Yoink.UI;

public partial class SettingsWindow
{
    private void UploadDestCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _settingsService.Settings.ImageUploadDestination = (Services.UploadDestination)UploadDestCombo.SelectedIndex;
        _settingsService.Save();
        UpdateUploadSettingsVisibility();
    }

    private void UpdateUploadSettingsVisibility()
    {
        var dest = (Services.UploadDestination)UploadDestCombo.SelectedIndex;
        ImgurSettings.Visibility = dest == Services.UploadDestination.Imgur ? Visibility.Visible : Visibility.Collapsed;
        ImgBBSettings.Visibility = dest == Services.UploadDestination.ImgBB ? Visibility.Visible : Visibility.Collapsed;
        CatboxSettings.Visibility = dest == Services.UploadDestination.Catbox ? Visibility.Visible : Visibility.Collapsed;
        LitterboxSettings.Visibility = dest == Services.UploadDestination.Litterbox ? Visibility.Visible : Visibility.Collapsed;
        GyazoSettings.Visibility = dest == Services.UploadDestination.Gyazo ? Visibility.Visible : Visibility.Collapsed;
        FileIoSettings.Visibility = dest == Services.UploadDestination.FileIo ? Visibility.Visible : Visibility.Collapsed;
        UguuSettings.Visibility = dest == Services.UploadDestination.Uguu ? Visibility.Visible : Visibility.Collapsed;
        TransferSettings.Visibility = dest == Services.UploadDestination.TransferSh ? Visibility.Visible : Visibility.Collapsed;
        DropboxSettings.Visibility = dest == Services.UploadDestination.Dropbox ? Visibility.Visible : Visibility.Collapsed;
        GoogleDriveSettings.Visibility = dest == Services.UploadDestination.GoogleDrive ? Visibility.Visible : Visibility.Collapsed;
        OneDriveSettings.Visibility = dest == Services.UploadDestination.OneDrive ? Visibility.Visible : Visibility.Collapsed;
        AzureSettings.Visibility = dest == Services.UploadDestination.AzureBlob ? Visibility.Visible : Visibility.Collapsed;
        GitHubSettings.Visibility = dest == Services.UploadDestination.GitHub ? Visibility.Visible : Visibility.Collapsed;
        ImmichSettings.Visibility = dest == Services.UploadDestination.Immich ? Visibility.Visible : Visibility.Collapsed;
        FtpSettings.Visibility = dest == Services.UploadDestination.Ftp ? Visibility.Visible : Visibility.Collapsed;
        SftpSettings.Visibility = dest == Services.UploadDestination.Sftp ? Visibility.Visible : Visibility.Collapsed;
        WebDavSettings.Visibility = dest == Services.UploadDestination.WebDav ? Visibility.Visible : Visibility.Collapsed;
        S3Settings.Visibility = dest == Services.UploadDestination.S3Compatible ? Visibility.Visible : Visibility.Collapsed;
        CustomUploadSettings.Visibility = dest == Services.UploadDestination.CustomHttp ? Visibility.Visible : Visibility.Collapsed;
        TestUploadCard.Visibility = dest != Services.UploadDestination.None ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AutoUploadScreenshotsCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _settingsService.Settings.AutoUploadScreenshots = AutoUploadScreenshotsCheck.IsChecked == true;
        _settingsService.Save();
    }

    private void AutoUploadGifsCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _settingsService.Settings.AutoUploadGifs = AutoUploadGifsCheck.IsChecked == true;
        _settingsService.Save();
    }

    private void AutoUploadVideosCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _settingsService.Settings.AutoUploadVideos = AutoUploadVideosCheck.IsChecked == true;
        _settingsService.Save();
    }

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

    private void ImgurClientIdBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.ImgurClientId = ImgurClientIdBox.Text;
        _settingsService.Save();
    }

    private void ImgurTokenBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.ImgurAccessToken = ImgurTokenBox.Text;
        _settingsService.Save();
    }

    private void ImgBBKeyBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.ImgBBApiKey = ImgBBKeyBox.Text;
        _settingsService.Save();
    }

    private void GyazoTokenBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.GyazoAccessToken = GyazoTokenBox.Text;
        _settingsService.Save();
    }

    private void DropboxTokenBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.DropboxAccessToken = DropboxTokenBox.Text;
        _settingsService.Save();
    }

    private void DropboxPathBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.DropboxPathPrefix = DropboxPathBox.Text;
        _settingsService.Save();
    }

    private void GoogleDriveTokenBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.GoogleDriveAccessToken = GoogleDriveTokenBox.Text;
        _settingsService.Save();
    }

    private void GoogleDriveFolderBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.GoogleDriveFolderId = GoogleDriveFolderBox.Text;
        _settingsService.Save();
    }

    private void OneDriveTokenBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.OneDriveAccessToken = OneDriveTokenBox.Text;
        _settingsService.Save();
    }

    private void OneDriveFolderBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.OneDriveFolder = OneDriveFolderBox.Text;
        _settingsService.Save();
    }

    private void AzureBlobSasBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.AzureBlobSasUrl = AzureBlobSasBox.Text;
        _settingsService.Save();
    }

    private void GitHubTokenBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.GitHubToken = GitHubTokenBox.Text;
        _settingsService.Save();
    }

    private void GitHubRepoBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.GitHubRepo = GitHubRepoBox.Text;
        _settingsService.Save();
    }

    private void GitHubBranchBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.GitHubBranch = GitHubBranchBox.Text;
        _settingsService.Save();
    }

    private void ImmichUrlBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.ImmichBaseUrl = ImmichUrlBox.Text;
        _settingsService.Save();
    }

    private void ImmichApiKeyBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.ImmichApiKey = ImmichApiKeyBox.Text;
        _settingsService.Save();
    }

    private void FtpUrlBox_Changed(object sender, TextChangedEventArgs e) { if (!IsLoaded) return; ActiveUploadSettings.FtpUrl = FtpUrlBox.Text; _settingsService.Save(); }
    private void FtpUsernameBox_Changed(object sender, TextChangedEventArgs e) { if (!IsLoaded) return; ActiveUploadSettings.FtpUsername = FtpUsernameBox.Text; _settingsService.Save(); }
    private void FtpPasswordBox_Changed(object sender, TextChangedEventArgs e) { if (!IsLoaded) return; ActiveUploadSettings.FtpPassword = FtpPasswordBox.Text; _settingsService.Save(); }
    private void FtpPublicUrlBox_Changed(object sender, TextChangedEventArgs e) { if (!IsLoaded) return; ActiveUploadSettings.FtpPublicUrl = FtpPublicUrlBox.Text; _settingsService.Save(); }
    private void SftpHostBox_Changed(object sender, TextChangedEventArgs e) { if (!IsLoaded) return; ActiveUploadSettings.SftpHost = SftpHostBox.Text; _settingsService.Save(); }
    private void SftpPortBox_Changed(object sender, TextChangedEventArgs e) { if (!IsLoaded) return; if (int.TryParse(SftpPortBox.Text, out var port)) ActiveUploadSettings.SftpPort = port; _settingsService.Save(); }
    private void SftpUsernameBox_Changed(object sender, TextChangedEventArgs e) { if (!IsLoaded) return; ActiveUploadSettings.SftpUsername = SftpUsernameBox.Text; _settingsService.Save(); }
    private void SftpPasswordBox_Changed(object sender, TextChangedEventArgs e) { if (!IsLoaded) return; ActiveUploadSettings.SftpPassword = SftpPasswordBox.Text; _settingsService.Save(); }
    private void SftpRemotePathBox_Changed(object sender, TextChangedEventArgs e) { if (!IsLoaded) return; ActiveUploadSettings.SftpRemotePath = SftpRemotePathBox.Text; _settingsService.Save(); }
    private void SftpPublicUrlBox_Changed(object sender, TextChangedEventArgs e) { if (!IsLoaded) return; ActiveUploadSettings.SftpPublicUrl = SftpPublicUrlBox.Text; _settingsService.Save(); }
    private void WebDavUrlBox_Changed(object sender, TextChangedEventArgs e) { if (!IsLoaded) return; ActiveUploadSettings.WebDavUrl = WebDavUrlBox.Text; _settingsService.Save(); }
    private void WebDavUsernameBox_Changed(object sender, TextChangedEventArgs e) { if (!IsLoaded) return; ActiveUploadSettings.WebDavUsername = WebDavUsernameBox.Text; _settingsService.Save(); }
    private void WebDavPasswordBox_Changed(object sender, TextChangedEventArgs e) { if (!IsLoaded) return; ActiveUploadSettings.WebDavPassword = WebDavPasswordBox.Text; _settingsService.Save(); }
    private void WebDavPublicUrlBox_Changed(object sender, TextChangedEventArgs e) { if (!IsLoaded) return; ActiveUploadSettings.WebDavPublicUrl = WebDavPublicUrlBox.Text; _settingsService.Save(); }

    private void S3EndpointBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.S3Endpoint = S3EndpointBox.Text;
        _settingsService.Save();
    }

    private void S3BucketBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.S3Bucket = S3BucketBox.Text;
        _settingsService.Save();
    }

    private void S3RegionBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.S3Region = S3RegionBox.Text;
        _settingsService.Save();
    }

    private void S3AccessKeyBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.S3AccessKey = S3AccessKeyBox.Text;
        _settingsService.Save();
    }

    private void S3SecretKeyBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.S3SecretKey = S3SecretKeyBox.Text;
        _settingsService.Save();
    }

    private void S3PublicUrlBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.S3PublicUrl = S3PublicUrlBox.Text;
        _settingsService.Save();
    }

    private void CustomUrlBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.CustomUploadUrl = CustomUrlBox.Text;
        _settingsService.Save();
    }

    private void CustomFieldBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.CustomFileFormName = CustomFieldBox.Text;
        _settingsService.Save();
    }

    private void CustomJsonPathBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.CustomResponseUrlPath = CustomJsonPathBox.Text;
        _settingsService.Save();
    }

    private void CustomHeadersBox_Changed(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ActiveUploadSettings.CustomHeaders = CustomHeadersBox.Text;
        _settingsService.Save();
    }

    private async void TestUpload_Click(object sender, RoutedEventArgs e)
    {
        TestUploadBtn.Content = "Uploading...";
        TestUploadBtn.IsEnabled = false;

        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yoink_test.png");
        try
        {
            using (var bmp = new Bitmap(1, 1))
                bmp.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);

            var result = await Services.UploadService.UploadAsync(
                tempPath,
                _settingsService.Settings.ImageUploadDestination,
                ActiveUploadSettings);

            if (result.Success)
                ToastWindow.Show("Upload works", result.Url);
            else
                ToastWindow.ShowError("Upload failed", result.Error);
        }
        catch (Exception ex)
        {
            ToastWindow.ShowError("Upload error", ex.Message);
        }
        finally
        {
            try { System.IO.File.Delete(tempPath); } catch { }
            TestUploadBtn.Content = "Test Upload";
            TestUploadBtn.IsEnabled = true;
        }
    }
}
