using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Image = System.Windows.Controls.Image;
using FontFamily = System.Windows.Media.FontFamily;
using Cursors = System.Windows.Input.Cursors;
using Yoink.Helpers;
using Yoink.Services;

namespace Yoink.UI;

public partial class SettingsWindow
{
    private void LoadVideoHistory()
    {
        VideoStack.Children.Clear();
        var baseDir = _settingsService.Settings.SaveDirectory;
        var videoDir = Path.Combine(baseDir, "Videos");
        var dirs = new[] { videoDir, baseDir }.Where(Directory.Exists).ToArray();
        if (dirs.Length == 0) { ShowVideoEmpty(); return; }
        var files = dirs.SelectMany(EnumerateVideoFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(File.GetCreationTime)
            .Take(50)
            .ToArray();
        if (files.Length == 0) { ShowVideoEmpty(); return; }

        var wrap = new WrapPanel();
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            string sizeStr = info.Length > 1024 * 1024
                ? $"{info.Length / 1024.0 / 1024.0:F1} MB"
                : $"{info.Length / 1024:N0} KB";
            string label = info.Extension.TrimStart('.').ToUpper();
            string timeAgo = FormatTimeAgo(info.CreationTime);

            var img = new System.Windows.Controls.Image { Stretch = Stretch.UniformToFill, Opacity = 0 };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            var thumbPath = GetVideoThumbnailPath(file);
            img.Loaded += (_, _) =>
            {
                LoadThumbAsync(img, thumbPath, file);
                img.BeginAnimation(OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)));
            };

            var locBtn = CreateFileLocationButton(file);

            var badge = new Border
            {
                Background = Theme.Brush(Theme.SectionIconBg),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 6, 6),
                Child = new TextBlock
                {
                    Text = $"{label} · {sizeStr}",
                    FontSize = 9,
                    Foreground = Theme.Brush(Theme.TextPrimary),
                    FontFamily = new FontFamily(UiChrome.PreferredFamilyName),
                }
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var imgContainer = new Grid();
            imgContainer.Children.Add(img);
            imgContainer.Children.Add(badge);
            imgContainer.Children.Add(locBtn);
            Grid.SetRow(imgContainer, 0);
            grid.Children.Add(imgContainer);

            var infoPanel = new StackPanel { Margin = new Thickness(10, 6, 10, 8) };
            infoPanel.Children.Add(new TextBlock
            {
                Text = info.Name,
                FontSize = 11,
                FontFamily = new FontFamily(UiChrome.PreferredFamilyName),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            infoPanel.Children.Add(new TextBlock
            {
                Text = timeAgo,
                FontSize = 10,
                FontFamily = new FontFamily(UiChrome.PreferredFamilyName),
                Opacity = 0.3
            });
            Grid.SetRow(infoPanel, 1);
            grid.Children.Add(infoPanel);

            var card = new Border
            {
                Width = 168,
                Margin = new Thickness(3),
                CornerRadius = new CornerRadius(8),
                Background = Theme.Brush(Theme.BgCard),
                BorderBrush = Theme.Brush(Theme.BorderSubtle),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Child = grid,
            };
            bool isDraggingFile = false;
            card.SizeChanged += (s, _) =>
            {
                var b = (Border)s!;
                b.Clip = new System.Windows.Media.RectangleGeometry(
                    new System.Windows.Rect(0, 0, b.ActualWidth, b.ActualHeight), 10, 10);
            };
            card.MouseEnter += (_, _) =>
            {
                locBtn.BeginAnimation(OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
            };
            card.MouseLeave += (_, _) =>
            {
                locBtn.BeginAnimation(OpacityProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(120)));
            };
            var filePath = file;
            if (File.Exists(filePath))
                AttachFileDragHandlers(card, card, filePath, () => !_selectMode, v => isDraggingFile = v);

            card.MouseLeftButtonUp += (_, _) =>
            {
                if (_selectMode || isDraggingFile)
                    return;
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = filePath, UseShellExecute = true }); }
                catch { }
            };
            wrap.Children.Add(card);
        }
        VideoStack.Children.Add(wrap);
    }

    private static IEnumerable<string> EnumerateVideoFiles(string dir)
    {
        foreach (var file in Directory.EnumerateFiles(dir))
        {
            var ext = Path.GetExtension(file);
            if (ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".webm", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".mkv", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
    }

    private void ShowVideoEmpty()
    {
        VideoStack.Children.Add(new TextBlock
        {
            Text = "No video recordings yet",
            FontSize = 13,
            Opacity = 0.2,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 40, 0, 0),
        });
    }

    private static string GetVideoThumbnailPath(string videoPath)
    {
        var thumbDir = Path.Combine(Path.GetDirectoryName(videoPath)!, ".thumbs");
        Directory.CreateDirectory(thumbDir);
        return Path.Combine(thumbDir, Path.GetFileNameWithoutExtension(videoPath) + ".jpg");
    }

    private static async Task<string> EnsureVideoThumbnailAsync(string videoPath, string thumbPath)
    {
        if (File.Exists(thumbPath))
            return thumbPath;

        var ffmpeg = Capture.VideoRecorder.FindFfmpeg();
        if (ffmpeg == null)
            return videoPath;

        try
        {
            var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = $"-y -i \"{videoPath}\" -vframes 1 -q:v 4 \"{thumbPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            });

            if (proc == null)
                return videoPath;

            await proc.WaitForExitAsync();
            return File.Exists(thumbPath) ? thumbPath : videoPath;
        }
        catch
        {
            return videoPath;
        }
    }

    private static void LoadThumbAsync(System.Windows.Controls.Image img, string path)
        => LoadThumbAsync(img, path, null);

    private static void LoadThumbAsync(System.Windows.Controls.Image img, string path, string? sourcePath)
    {
        if (img.Source != null) return;

        var cacheKey = sourcePath ?? path;

        if (TryGetThumbFromCache(cacheKey, out var cached))
        {
            img.Source = cached;
            return;
        }

        lock (ThumbInflight)
        {
            if (!ThumbInflight.Add(cacheKey))
                return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await ThumbDecodeGate.WaitAsync();
                try
                {
                    var loadPath = path;
                    if (!File.Exists(loadPath) && sourcePath != null)
                        loadPath = await EnsureVideoThumbnailAsync(sourcePath, path);

                    if (!File.Exists(loadPath))
                        return;

                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(loadPath);
                    bmp.DecodePixelWidth = 240;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();

                    StoreThumbInCache(cacheKey, bmp);
                    _ = img.Dispatcher.BeginInvoke(() =>
                    {
                        if (img.Source == null)
                            img.Source = bmp;
                    });
                }
                finally
                {
                    ThumbDecodeGate.Release();
                }
            }
            catch { }
            finally
            {
                lock (ThumbInflight)
                    ThumbInflight.Remove(cacheKey);
            }
        });
    }
}
