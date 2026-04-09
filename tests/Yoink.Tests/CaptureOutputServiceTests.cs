using System.Drawing;
using Yoink.Models;
using Yoink.Services;
using Xunit;

namespace Yoink.Tests;

public sealed class CaptureOutputServiceTests
{
    [Theory]
    [InlineData(CaptureImageFormat.Png, "png")]
    [InlineData(CaptureImageFormat.Jpeg, "jpg")]
    [InlineData(CaptureImageFormat.Bmp, "bmp")]
    public void SaveBitmap_WritesRequestedFormatWithoutLeavingTempFiles(CaptureImageFormat format, string extension)
    {
        var root = CreateTempRoot();
        try
        {
            using var bitmap = new Bitmap(12, 8);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Coral);
            }

            var filePath = Path.Combine(root, "nested", $"capture.{extension}");
            CaptureOutputService.SaveBitmap(bitmap, filePath, format, jpegQuality: 90);

            Assert.True(File.Exists(filePath));
            Assert.NotEmpty(File.ReadAllBytes(filePath));
            Assert.DoesNotContain(Directory.EnumerateFiles(Path.GetDirectoryName(filePath)!), path => path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
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
