using System.IO;
using Yoink.Models;
using Yoink.Services;
using Xunit;

namespace Yoink.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void Save_BuffersDiskWritesButUpdatesProcessCache()
    {
        var root = CreateTempRoot();
        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            using var service = new SettingsService(settingsPath, TimeSpan.FromMinutes(1));
            service.Settings.StartWithWindows = true;

            service.Save();

            Assert.False(File.Exists(settingsPath));

            var cached = SettingsService.LoadStatic(settingsPath);
            Assert.NotNull(cached);
            Assert.True(cached!.StartWithWindows);

            service.FlushPendingWrites();

            Assert.True(File.Exists(settingsPath));
            Assert.Contains("\"StartWithWindows\": true", File.ReadAllText(settingsPath));
        }
        finally
        {
            TryDeleteRoot(root);
        }
    }

    [Fact]
    public void Dispose_FlushesPendingWrites()
    {
        var root = CreateTempRoot();
        try
        {
            var settingsPath = Path.Combine(root, "settings.json");
            var service = new SettingsService(settingsPath, TimeSpan.FromMinutes(1));
            service.Settings.MuteSounds = true;

            service.Save();
            service.Dispose();

            Assert.True(File.Exists(settingsPath));

            var reloaded = SettingsService.LoadStatic(settingsPath);
            Assert.NotNull(reloaded);
            Assert.True(reloaded!.MuteSounds);
        }
        finally
        {
            TryDeleteRoot(root);
        }
    }

    [Fact]
    public void TryDeserialize_AppliesSettingsMigrations()
    {
        const string json = """
        {
          "CompressHistory": true,
          "CaptureImageFormat": 0,
          "ImageUploadDestination": 8,
          "ImageUploadSettings": {
            "AiChatUploadDestination": 8
          },
          "EnabledTools": ["rect"],
          "StickerUploadSettings": {
            "Provider": 0,
            "LocalEngine": 3
          }
        }
        """;

        var ok = SettingsService.TryDeserialize(json, out var settings);

        Assert.True(ok);
        Assert.Equal(CaptureImageFormat.Jpeg, settings.CaptureImageFormat);
        Assert.NotNull(settings.EnabledTools);
        Assert.Contains("rect", settings.EnabledTools!);
        Assert.Contains(ToolDef.DefaultEnabledIds().First(), settings.EnabledTools!);
        Assert.Equal(StickerProvider.LocalCpu, settings.StickerUploadSettings.Provider);
        Assert.Equal(LocalStickerEngine.BiRefNetLite, settings.StickerUploadSettings.LocalEngine);
        Assert.Equal(LocalStickerEngine.U2Netp, settings.StickerUploadSettings.LocalCpuEngine);
        Assert.Equal(UploadDestination.TempHosts, settings.ImageUploadDestination);
        Assert.Equal(UploadDestination.Catbox, settings.ImageUploadSettings.AiChatUploadDestination);
    }

    [Fact]
    public void LoadStatic_ReturnsIsolatedCachedSettingsInstances()
    {
        var root = CreateTempRoot();
        try
        {
            var settingsPath = Path.Combine(root, "settings.json");

            var first = SettingsService.LoadStatic(settingsPath);
            Assert.NotNull(first);

            first!.MuteSounds = true;

            var second = SettingsService.LoadStatic(settingsPath);
            Assert.NotNull(second);
            Assert.NotSame(first, second);
            Assert.False(second!.MuteSounds);
        }
        finally
        {
            TryDeleteRoot(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "yoink-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDeleteRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
        }
    }
}
