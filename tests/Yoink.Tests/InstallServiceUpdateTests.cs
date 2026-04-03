using System.IO.Compression;
using Xunit;
using Yoink.Services;

namespace Yoink.Tests;

public sealed class InstallServiceUpdateTests
{
    [Fact]
    public void ApplyUpdateFromZip_CopiesFilesIntoTargetDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "yoink-tests", Guid.NewGuid().ToString("N"));
        var packageDir = Path.Combine(root, "package");
        var targetDir = Path.Combine(root, "target");
        var zipPath = Path.Combine(root, "update.zip");

        try
        {
            Directory.CreateDirectory(packageDir);
            Directory.CreateDirectory(targetDir);

            File.WriteAllText(Path.Combine(packageDir, "Yoink.exe"), "new executable");
            File.WriteAllText(Path.Combine(packageDir, "portable.txt"), "portable");
            Directory.CreateDirectory(Path.Combine(packageDir, "nested"));
            File.WriteAllText(Path.Combine(packageDir, "nested", "data.txt"), "payload");

            File.WriteAllText(Path.Combine(targetDir, "Yoink.exe"), "old executable");

            ZipFile.CreateFromDirectory(packageDir, zipPath);

            InstallService.ApplyUpdateFromZip(zipPath, targetDir, launchAfter: false);

            Assert.Equal("new executable", File.ReadAllText(Path.Combine(targetDir, "Yoink.exe")));
            Assert.Equal("portable", File.ReadAllText(Path.Combine(targetDir, "portable.txt")));
            Assert.Equal("payload", File.ReadAllText(Path.Combine(targetDir, "nested", "data.txt")));
            Assert.False(File.Exists(zipPath));
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch { }
        }
    }
}
