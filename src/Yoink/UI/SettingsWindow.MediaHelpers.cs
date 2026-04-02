using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Image = System.Windows.Controls.Image;
using FontFamily = System.Windows.Media.FontFamily;
using Yoink.Models;
using Yoink.Helpers;
using Yoink.Services;

namespace Yoink.UI;

public partial class SettingsWindow
{
    private static bool TryGetThumbFromCache(string path, out BitmapImage? image)
    {
        lock (ThumbCache)
        {
            if (!ThumbCache.TryGetValue(path, out var cached))
            {
                image = null;
                return false;
            }

            TouchThumbCache(path);
            image = cached;
            return true;
        }
    }

    private static void StoreThumbInCache(string path, BitmapImage image)
    {
        lock (ThumbCache)
        {
            ThumbCache[path] = image;
            TouchThumbCache(path);

            while (ThumbCacheOrder.Count > MaxThumbCacheEntries)
            {
                var oldest = ThumbCacheOrder.Last;
                if (oldest is null)
                    break;

                ThumbCacheOrder.RemoveLast();
                ThumbCacheNodes.Remove(oldest.Value);
                ThumbCache.Remove(oldest.Value);
            }
        }
    }

    private static void TouchThumbCache(string path)
    {
        if (ThumbCacheNodes.TryGetValue(path, out var existing))
            ThumbCacheOrder.Remove(existing);

        ThumbCacheNodes[path] = ThumbCacheOrder.AddFirst(path);
    }

    internal static void ClearThumbCache()
    {
        lock (ThumbCache)
        {
            ThumbCache.Clear();
            ThumbCacheOrder.Clear();
            ThumbCacheNodes.Clear();
        }
        LogoCache.Clear();
    }

    private static BitmapImage? LoadPackImage(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;

        lock (LogoCache)
        {
            if (LogoCache.TryGetValue(relativePath, out var cached))
                return cached;
        }

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();

            lock (LogoCache) LogoCache[relativePath] = bmp;
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private static FrameworkElement? CreateProviderBadge(string? providerOrPath, bool isPath = false)
    {
        string logoPath = isPath ? (providerOrPath ?? string.Empty) : UploadService.GetHistoryLogoPath(providerOrPath);
        var logoSource = LoadPackImage(logoPath);
        if (logoSource == null)
        {
            if (string.IsNullOrWhiteSpace(providerOrPath)) return null;

            string text = providerOrPath.Trim();
            if (!isPath)
            {
                text = text switch
                {
                    "Remove.bg" => "RBG",
                    "Photoroom" => "PR",
                    "Local" => "LCL",
                    _ => text.Length <= 4 ? text.ToUpperInvariant() : text[..4].ToUpperInvariant()
                };
            }

            return new Border
            {
                MinWidth = 24,
                Height = 24,
                CornerRadius = new CornerRadius(7),
                Background = Theme.Brush(Theme.SectionIconBg),
                BorderBrush = Theme.StrokeBrush(),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(6, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 8.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Theme.Brush(Theme.TextPrimary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 4, 0)
                }
            };
        }

        return new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(7),
            Background = Theme.Brush(Theme.SectionIconBg),
            BorderBrush = Theme.StrokeBrush(),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(6, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new Image
            {
                Source = logoSource,
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private static string FormatStorageSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    private static string FormatTimeAgo(DateTime dt)
    {
        var span = DateTime.Now - dt;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return dt.ToString("MMM d");
    }
}
