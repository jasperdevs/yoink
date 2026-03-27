using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Yoink.Services;

namespace Yoink.UI;

public partial class ImageViewerWindow : Window
{
    private readonly string _filePath;
    private readonly HistoryService _historyService;
    private readonly HistoryEntry _entry;

    public ImageViewerWindow(string filePath, HistoryService historyService, HistoryEntry entry)
    {
        _filePath = filePath;
        _historyService = historyService;
        _entry = entry;
        InitializeComponent();

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(filePath);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        ViewerImage.Source = bmp;
        InfoText.Text = $"{_entry.Width} x {_entry.Height}  |  {_entry.CapturedAt:MMM d, yyyy  h:mm tt}";

        // Size window to image, capped at 80% of screen
        var screen = SystemParameters.WorkArea;
        double maxW = screen.Width * 0.8;
        double maxH = screen.Height * 0.8;
        double scale = Math.Min(maxW / bmp.PixelWidth, maxH / bmp.PixelHeight);
        scale = Math.Min(scale, 1.0); // don't upscale
        Width = Math.Max(400, bmp.PixelWidth * scale);
        Height = Math.Max(300, bmp.PixelHeight * scale);
        SizeToContent = SizeToContent.Manual;
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void DragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void CloseClick(object sender, MouseButtonEventArgs e) => Close();

    private void CopyClick(object sender, RoutedEventArgs e)
    {
        if (File.Exists(_filePath))
        {
            using var bmp = new Bitmap(_filePath);
            ClipboardService.CopyToClipboard(bmp);
        }
        Close();
    }

    private void DeleteClick(object sender, RoutedEventArgs e)
    {
        _historyService.DeleteEntry(_entry);
        Close();
    }
}
