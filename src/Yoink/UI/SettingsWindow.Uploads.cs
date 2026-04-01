using System.Windows;
using System.Windows.Controls;
using Yoink.Models;
using Yoink.Services;

namespace Yoink.UI;

public partial class SettingsWindow
{
    private UploadSettings ActiveUploadSettings => _settingsService.Settings.ImageUploadSettings;
    private StickerSettings ActiveStickerSettings => _settingsService.Settings.StickerUploadSettings;

    private void LoadUploadSettingsIntoUi(UploadSettings s)
    {
        ImgurClientIdBox.Text = s.ImgurClientId;
        ImgurTokenBox.Text = s.ImgurAccessToken;
        ImgBBKeyBox.Text = s.ImgBBApiKey;
        GyazoTokenBox.Text = s.GyazoAccessToken;
        DropboxTokenBox.Text = s.DropboxAccessToken;
        DropboxPathBox.Text = s.DropboxPathPrefix;
        GoogleDriveTokenBox.Text = s.GoogleDriveAccessToken;
        GoogleDriveFolderBox.Text = s.GoogleDriveFolderId;
        OneDriveTokenBox.Text = s.OneDriveAccessToken;
        OneDriveFolderBox.Text = s.OneDriveFolder;
        AzureBlobSasBox.Text = s.AzureBlobSasUrl;
        GitHubTokenBox.Text = s.GitHubToken;
        GitHubRepoBox.Text = s.GitHubRepo;
        GitHubBranchBox.Text = s.GitHubBranch;
        ImmichUrlBox.Text = s.ImmichBaseUrl;
        ImmichApiKeyBox.Text = s.ImmichApiKey;
        FtpUrlBox.Text = s.FtpUrl;
        FtpUsernameBox.Text = s.FtpUsername;
        FtpPasswordBox.Text = s.FtpPassword;
        FtpPublicUrlBox.Text = s.FtpPublicUrl;
        SftpHostBox.Text = s.SftpHost;
        SftpPortBox.Text = s.SftpPort.ToString();
        SftpUsernameBox.Text = s.SftpUsername;
        SftpPasswordBox.Text = s.SftpPassword;
        SftpRemotePathBox.Text = s.SftpRemotePath;
        SftpPublicUrlBox.Text = s.SftpPublicUrl;
        WebDavUrlBox.Text = s.WebDavUrl;
        WebDavUsernameBox.Text = s.WebDavUsername;
        WebDavPasswordBox.Text = s.WebDavPassword;
        WebDavPublicUrlBox.Text = s.WebDavPublicUrl;
        S3EndpointBox.Text = s.S3Endpoint;
        S3BucketBox.Text = s.S3Bucket;
        S3RegionBox.Text = s.S3Region;
        S3AccessKeyBox.Text = s.S3AccessKey;
        S3SecretKeyBox.Text = s.S3SecretKey;
        S3PublicUrlBox.Text = s.S3PublicUrl;
        CustomUrlBox.Text = s.CustomUploadUrl;
        CustomFieldBox.Text = s.CustomFileFormName;
        CustomJsonPathBox.Text = s.CustomResponseUrlPath;
        CustomHeadersBox.Text = s.CustomHeaders;
    }

    private void LoadStickerSettingsIntoUi(StickerSettings s)
    {
        StickerProviderCombo.SelectedIndex = (int)s.Provider;
        StickerLocalEngineCombo.SelectedIndex = (int)s.LocalEngine;
        StickerRemoveBgKeyBox.Text = s.RemoveBgApiKey;
        StickerPhotoroomKeyBox.Text = s.PhotoroomApiKey;
        StickerShadowCheck.IsChecked = s.AddShadow;
        StickerStrokeCheck.IsChecked = s.AddStroke;
        UpdateStickerProviderVisibility();
        UpdateLocalEngineUi();
    }

    private void UpdateUploadTabVisibility()
    {
        ImageUploadsPanel.Visibility = UploadImagesSubTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        StickerUploadsPanel.Visibility = UploadStickersSubTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateStickerProviderVisibility()
    {
        var provider = (StickerProvider)StickerProviderCombo.SelectedIndex;
        StickerRemoveBgPanel.Visibility = provider == StickerProvider.RemoveBg ? Visibility.Visible : Visibility.Collapsed;
        StickerPhotoroomPanel.Visibility = provider == StickerProvider.Photoroom ? Visibility.Visible : Visibility.Collapsed;
        StickerLocalCpuPanel.Visibility = provider == StickerProvider.LocalCpu ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateLocalEngineUi()
    {
        var engine = (LocalStickerEngine)StickerLocalEngineCombo.SelectedIndex;
        bool downloaded = LocalStickerEngineService.IsModelDownloaded(engine);

        StickerLocalEngineStatusText.Text = downloaded
            ? $"{LocalStickerEngineService.GetEngineLabel(engine)} is downloaded and ready to use for sticker captures."
            : LocalStickerEngineService.GetEngineDescription(engine);

        StickerDownloadRembgBtn.Visibility = Visibility.Visible;
        StickerOpenLocalEngineRepoBtn.Content = "Open model page";
        StickerDownloadRembgBtn.Content = downloaded ? "Remove model" : "Download model";

        if (!IsLoaded)
            return;

        ActiveStickerSettings.LocalEngine = engine;
        _settingsService.Save();
    }

    private void SetStickerDownloadUi(bool isBusy, double? percent = null, string? message = null)
    {
        StickerDownloadRembgBtn.IsEnabled = !isBusy;
        StickerOpenLocalEngineRepoBtn.IsEnabled = !isBusy;

        StickerLocalEngineProgress.Visibility = isBusy || percent.HasValue ? Visibility.Visible : Visibility.Collapsed;
        StickerLocalEngineProgressText.Visibility = !string.IsNullOrWhiteSpace(message) || isBusy ? Visibility.Visible : Visibility.Collapsed;

        if (percent.HasValue)
            StickerLocalEngineProgress.Value = Math.Clamp(percent.Value, 0, 100);
        else
            StickerLocalEngineProgress.Value = 0;

        if (!string.IsNullOrWhiteSpace(message))
            StickerLocalEngineProgressText.Text = message;
        else if (!isBusy)
            StickerLocalEngineProgressText.Text = string.Empty;
    }
}
