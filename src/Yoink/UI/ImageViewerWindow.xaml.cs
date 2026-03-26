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
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

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

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
