using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.IO;
using Yoink.Helpers;
using Color = System.Windows.Media.Color;

namespace Yoink.UI;

public partial class ToastWindow
{
    public static void SetPosition(Yoink.Models.ToastPosition position) => _position = position;
    public static void SetDuration(double seconds) => _durationSeconds = Math.Clamp(seconds, 1, 10);
    public static double GetDuration() => _durationSeconds;

    public static void Show(string title, string body = "", string? filePath = null)
    {
        Services.SoundService.PlayCaptureSound();
        ReplaceCurrentToast();
        var toast = new ToastWindow(title, body, null);
        if (filePath != null)
        {
            toast._savedFilePath = filePath;
            toast.Cursor = System.Windows.Input.Cursors.Hand;
            toast.MouseLeftButtonDown += (_, _) => OpenFileLocation(filePath);
        }
        _current = toast;
        toast.Show();
    }

    public static void ShowSticker(Bitmap sticker)
    {
        ReplaceCurrentToast();
        var toast = new StickerToastWindow(sticker, _position);
        _currentSticker = toast;
        toast.Closed += (_, _) => { if (_currentSticker == toast) _currentSticker = null; };
        toast.Show();
    }

    public static void ShowWithColor(string title, string body, Color color)
    {
        Services.SoundService.PlayCaptureSound();
        ReplaceCurrentToast();
        var toast = new ToastWindow(title, body, color);
        _current = toast;
        toast.Show();
    }

    public static void ShowError(string title, string body = "", string? filePath = null)
    {
        Services.SoundService.PlayErrorSound();
        ReplaceCurrentToast();
        var toast = new ToastWindow(title, body, null);

        // Red-tinted error styling — clearly different from normal toasts
        var red = System.Windows.Media.Color.FromRgb(239, 68, 68);
        toast.Root.Background = Theme.Brush(Theme.IsDark
            ? System.Windows.Media.Color.FromRgb(60, 28, 28)
            : System.Windows.Media.Color.FromRgb(255, 240, 240));
        toast.Root.BorderBrush = Theme.Brush(System.Windows.Media.Color.FromArgb(100, red.R, red.G, red.B));
        toast.Root.BorderThickness = new Thickness(1.5);
        toast.ProgressBar.Background = Theme.Brush(System.Windows.Media.Color.FromArgb(180, red.R, red.G, red.B));
        toast.TitleText.Foreground = Theme.Brush(red);

        if (filePath != null)
        {
            toast._savedFilePath = filePath;
            toast.Cursor = System.Windows.Input.Cursors.Hand;
            toast.MouseLeftButtonDown += (_, _) => OpenFileLocation(filePath);
        }
        _current = toast;
        toast.Show();
    }

    /// <summary>Show a preview toast with an image thumbnail.</summary>
    public static void ShowImagePreview(Bitmap screenshot, string? filePath, bool autoPin)
    {
        ReplaceCurrentToast();
        var toast = new ToastWindow(filePath != null ? System.IO.Path.GetFileName(filePath) : "Screenshot",
                                     $"{screenshot.Width}x{screenshot.Height}", null);
        toast._previewBitmap = screenshot;
        toast._savedFilePath = filePath;

        // Set image thumbnail — preserve aspect ratio, adapt toast width to image shape
        toast.ImageArea.Visibility = Visibility.Visible;
        double aspect = (double)screenshot.Width / screenshot.Height;
        // Wide images get a wider toast, tall images get a narrower one
        int toastW = (int)Math.Clamp(180 * aspect, 200, 340);
        toast.Root.MaxWidth = toastW;
        toast.Root.MinWidth = Math.Min(200, toastW);
        toast.ImageArea.MaxHeight = (int)Math.Clamp(toastW / aspect, 80, 200);
        toast.PreviewImage.Source = ToBitmapSource(screenshot);

        // Clip Root to rounded corners after layout (WPF Border.ClipToBounds doesn't clip to CornerRadius)
        toast.Root.SizeChanged += (s, _) =>
        {
            var b = (System.Windows.Controls.Border)s!;
            b.Clip = new System.Windows.Media.RectangleGeometry(
                new Rect(0, 0, b.ActualWidth, b.ActualHeight), 10, 10);
        };

        // Pin support
        if (autoPin)
        {
            toast._isPinned = true;
            toast.ProgressBar.Visibility = Visibility.Collapsed;
            toast.PinBtn.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(180, 255, 255, 255));
            toast.PinIcon.Fill = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(20, 20, 20));
            toast.PinBtn.Opacity = 1;
        }

        // Overlay button hover show/hide
        var btnDur = TimeSpan.FromMilliseconds(120);
        toast.MouseEnter += (_, _) =>
        {
            toast.CloseBtn.BeginAnimation(OpacityProperty, new DoubleAnimation { To = 1, Duration = btnDur });
            toast.PinBtn.BeginAnimation(OpacityProperty, new DoubleAnimation { To = toast._isPinned ? 1 : 1, Duration = btnDur });
            toast.SaveBtn.BeginAnimation(OpacityProperty, new DoubleAnimation { To = 1, Duration = btnDur });
        };
        toast.MouseLeave += (_, _) =>
        {
            toast.CloseBtn.BeginAnimation(OpacityProperty, new DoubleAnimation { To = 0, Duration = btnDur });
            toast.PinBtn.BeginAnimation(OpacityProperty, new DoubleAnimation { To = toast._isPinned ? 0.7 : 0, Duration = btnDur });
            toast.SaveBtn.BeginAnimation(OpacityProperty, new DoubleAnimation { To = 0, Duration = btnDur });
        };

        // Close button
        toast.CloseBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; toast.SlideAway(); };

        // Pin button
        toast.PinBtn.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            toast._isPinned = !toast._isPinned;
            if (toast._isPinned)
            {
                toast._timer.Stop();
                toast.ProgressScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
                toast.ProgressBar.Visibility = Visibility.Collapsed;
                toast.PinBtn.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(180, 255, 255, 255));
                toast.PinIcon.Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(20, 20, 20));
            }
            else
            {
                toast.ProgressBar.Visibility = Visibility.Visible;
                toast.ProgressScale.ScaleX = 1;
                toast.ProgressScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
                    new DoubleAnimation { To = 0, Duration = TimeSpan.FromSeconds(_durationSeconds) });
                toast._timer.Interval = TimeSpan.FromSeconds(_durationSeconds);
                toast._timer.Start();
                toast.PinBtn.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(144, 0, 0, 0));
                toast.PinIcon.Fill = System.Windows.Media.Brushes.White;
            }
        };

        // Save button (Save As dialog)
        toast.SaveBtn.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            // Pin the toast so it doesn't auto-dismiss while the dialog is open
            toast._timer.Stop();
            toast._isPinned = true;
            toast.ProgressBar.Visibility = Visibility.Collapsed;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = filePath != null ? System.IO.Path.GetFileName(filePath) : "screenshot.png",
                Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp"
            };
            if (dlg.ShowDialog(toast) == true && screenshot != null)
            {
                var fmt = dlg.FilterIndex switch { 2 => System.Drawing.Imaging.ImageFormat.Jpeg, 3 => System.Drawing.Imaging.ImageFormat.Bmp, _ => System.Drawing.Imaging.ImageFormat.Png };
                screenshot.Save(dlg.FileName, fmt);
                ToastWindow.Show("Saved", System.IO.Path.GetFileName(dlg.FileName));
            }
        };

        // Click on text/image opens file location if there is one, otherwise it dismisses.
        toast.TitleText.Cursor = System.Windows.Input.Cursors.Hand;
        toast.BodyText.Cursor = System.Windows.Input.Cursors.Hand;
        if (filePath != null)
            toast.ToolTip = "Drag to move the file or click to open its location";

        _current = toast;
        toast.Show();
    }

    private static void OpenFileLocation(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) return;
        try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\""); }
        catch { }
    }

    public static void DismissCurrent()
    {
        _current?.TryForceClose();
        _currentSticker?.ForceClose();
    }

    private static void ReplaceCurrentToast()
    {
        _current?.TryForceClose(force: true);
        _currentSticker?.ForceClose();
        _currentSticker = null;
    }

    private const double Edge = 8;

    private static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        return BitmapPerf.ToBitmapSource(bitmap);
    }
}
