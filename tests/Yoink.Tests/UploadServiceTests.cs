using Xunit;
using Yoink.Services;

namespace Yoink.Tests;

public sealed class UploadServiceTests
{
    [Theory]
    [InlineData(UploadDestination.Imgur, "Imgur")]
    [InlineData(UploadDestination.ImgBB, "ImgBB")]
    [InlineData(UploadDestination.Catbox, "Catbox")]
    [InlineData(UploadDestination.TransferSh, "transfer.sh")]
    [InlineData(UploadDestination.S3Compatible, "S3")]
    public void GetName_ReturnsExpectedLabels(UploadDestination destination, string expected)
    {
        Assert.Equal(expected, UploadService.GetName(destination));
    }

    [Theory]
    [InlineData("imgur", "Assets/imgur_sq.png")]
    [InlineData("Google Drive", "Assets/gdrive_sq.png")]
    [InlineData("S3", "Assets/aws_sq.png")]
    [InlineData(null, "")]
    public void GetHistoryLogoPath_ReturnsExpectedAssets(string? provider, string expected)
    {
        Assert.Equal(expected, UploadService.GetHistoryLogoPath(provider));
    }

    [Theory]
    [InlineData(UploadDestination.Imgur, "Assets/imgur_sq.png")]
    [InlineData(UploadDestination.GoogleDrive, "Assets/gdrive_sq.png")]
    [InlineData(UploadDestination.S3Compatible, "Assets/aws_sq.png")]
    [InlineData(UploadDestination.None, "")]
    public void GetUploadsLogoPath_ReturnsExpectedAssets(UploadDestination destination, string expected)
    {
        Assert.Equal(expected, UploadService.GetUploadsLogoPath(destination));
    }

    [Fact]
    public void HasCredentials_HandlesCredentialedAndCredentiallessTargets()
    {
        var settings = new UploadSettings();

        Assert.False(UploadService.HasCredentials(UploadDestination.None, settings));
        Assert.True(UploadService.HasCredentials(UploadDestination.Catbox, settings));
        Assert.False(UploadService.HasCredentials(UploadDestination.Imgur, settings));

        settings.ImgurClientId = "client-id";
        Assert.True(UploadService.HasCredentials(UploadDestination.Imgur, settings));
    }

    [Theory]
    [InlineData(UploadDestination.Imgur, ".png", 20L * 1024 * 1024)]
    [InlineData(UploadDestination.Imgur, ".gif", 200L * 1024 * 1024)]
    [InlineData(UploadDestination.TransferSh, ".png", 10L * 1024 * 1024 * 1024)]
    public void GetMaxSize_ReflectsDestinationRules(UploadDestination destination, string extension, long expected)
    {
        var path = "sample" + extension;

        Assert.Equal(expected, UploadService.GetMaxSize(destination, path));
    }
}
