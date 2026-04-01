using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Drawing;
using System.IO;
using Color = System.Windows.Media.Color;

namespace Yoink.UI;

public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _timer;
    private bool _isDismissing;
    private bool _isHovered;

    private static ToastWindow? _current;
    private static StickerToastWindow? _currentSticker;
    private static Yoink.Models.ToastPosition _position = Yoink.Models.ToastPosition.Right;
    private static double _durationSeconds = 2.5;

    private bool _isPinned;
    private bool _hasImagePreview;
    private string? _savedFilePath;
    private Bitmap? _previewBitmap;

    private ToastWindow(string title, string body, Color? swatchColor)
    {
        InitializeComponent();
        Opacity = 0;
        Theme.Refresh();

        Root.Background = Theme.Brush(Theme.ToastBg);
        Root.BorderBrush = Theme.Brush(Theme.ToastBorder);
        Root.BorderThickness = new Thickness(1);
        TitleText.Foreground = Theme.Brush(Theme.TextPrimary);
        BodyText.Foreground = Theme.Brush(Theme.TextSecondary);
        ProgressBar.Background = Theme.Brush(Theme.IsDark
            ? System.Windows.Media.Color.FromArgb(60, 255, 255, 255)
            : System.Windows.Media.Color.FromArgb(40, 0, 0, 0));

        TitleText.Text = title;
        BodyText.Text = body;
        if (string.IsNullOrEmpty(body)) BodyText.Visibility = Visibility.Collapsed;

        if (swatchColor.HasValue)
        {
            ColorSwatch.Background = Theme.Brush(swatchColor.Value);
            ColorSwatch.Visibility = Visibility.Visible;
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_durationSeconds) };
        _timer.Tick += (_, _) => { _timer.Stop(); if (!_isHovered) SlideAway(); };

        MouseEnter += (_, _) =>
        {
            _isHovered = true;
            _timer.Stop();
            ProgressScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
            ProgressScale.ScaleX = ProgressScale.ScaleX;
        };
        MouseLeave += (_, _) =>
        {
            _isHovered = false;
            var remaining = Math.Max(0.1, ProgressScale.ScaleX * _durationSeconds);
            ProgressScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
                new DoubleAnimation { To = 0, Duration = TimeSpan.FromSeconds(remaining) });
            _timer.Interval = TimeSpan.FromSeconds(remaining);
            _timer.Start();
        };
        MouseLeftButtonDown += (_, _) =>
        {
            if (_hasImagePreview) return; // image toasts handle clicks on the image/text
            if (_savedFilePath != null) { OpenFileLocation(_savedFilePath); return; }
            SlideAway();
        };
        SourceInitialized += (_, _) => PopupWindowHelper.ApplyNoActivateChrome(this);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var wa = SystemParameters.WorkArea;
        var (targetLeft, targetTop, startLeft, startTop, animateLeft) = PopupWindowHelper.GetPlacement(
            _position, ActualWidth, ActualHeight, wa, Edge);
        Left = startLeft;
        Top = startTop;

        // Use Render priority so layout is fully done before animating
        Dispatcher.BeginInvoke(() =>
        {
            Opacity = 1;
            var dur = TimeSpan.FromMilliseconds(250);
            var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };

            if (animateLeft)
            {
                BeginAnimation(LeftProperty, new DoubleAnimation
                {
                    To = targetLeft, Duration = dur, EasingFunction = ease
                });
            }
            else
            {
                BeginAnimation(TopProperty, new DoubleAnimation
                {
                    To = targetTop, Duration = dur, EasingFunction = ease
                });
            }

            if (!_isPinned)
            {
                _timer.Start();
                ProgressScale.ScaleX = 1;
                ProgressScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
                    new DoubleAnimation { To = 0, Duration = TimeSpan.FromSeconds(_durationSeconds) });
            }
        }, DispatcherPriority.Render);
    }

    private void SlideAway()
    {
        if (_isDismissing) return;
        _isDismissing = true;
        _timer.Stop();

        // Cancel any entrance animation
        BeginAnimation(LeftProperty, null);

        var wa = SystemParameters.WorkArea;
        var dur = TimeSpan.FromMilliseconds(220);
        var ease = new QuarticEase { EasingMode = EasingMode.EaseIn };

        var (exitLeft, exitTop, animateLeft) = PopupWindowHelper.GetDismissPlacement(
            _position, ActualWidth, ActualHeight, wa, Edge);
        Timeline slide;
        if (animateLeft)
        {
            slide = new DoubleAnimation
            {
                To = exitLeft,
                Duration = dur,
                EasingFunction = ease
            };
            slide.Completed += (_, _) => ForceClose();
            BeginAnimation(LeftProperty, (DoubleAnimation)slide);
        }
        else
        {
            slide = new DoubleAnimation
            {
                To = exitTop,
                Duration = dur,
                EasingFunction = ease
            };
            slide.Completed += (_, _) => ForceClose();
            BeginAnimation(TopProperty, (DoubleAnimation)slide);
        }

        BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            To = 0, Duration = dur, EasingFunction = ease
        });
    }

    private void ForceClose()
    {
        _timer.Stop();
        if (_current == this) _current = null;
        try { Close(); } catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        if (_current == this) _current = null;
        _previewBitmap?.Dispose();
        _previewBitmap = null;
        PreviewImage.Source = null;
        base.OnClosed(e);
    }

    public static void SetPosition(Yoink.Models.ToastPosition position) => _position = position;
    public static void SetDuration(double seconds) => _durationSeconds = Math.Clamp(seconds, 1, 10);
    public static double GetDuration() => _durationSeconds;

    public static void Show(string title, string body = "", string? filePath = null)
    {
        Services.SoundService.PlayCaptureSound();
        _currentSticker?.ForceClose(); _currentSticker = null;
        _current?.ForceClose();
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
        _current?.ForceClose();
        _currentSticker?.ForceClose();
        var toast = new StickerToastWindow(sticker, _position);
        _currentSticker = toast;
        toast.Closed += (_, _) => { if (_currentSticker == toast) _currentSticker = null; };
        toast.Show();
    }

    public static void ShowWithColor(string title, string body, Color color)
    {
        Services.SoundService.PlayCaptureSound();
        _currentSticker?.ForceClose(); _currentSticker = null;
        _current?.ForceClose();
        var toast = new ToastWindow(title, body, color);
        _current = toast;
        toast.Show();
    }

    public static void ShowError(string title, string body = "", string? filePath = null)
    {
        Services.SoundService.PlayErrorSound();
        _currentSticker?.ForceClose(); _currentSticker = null;
        _current?.ForceClose();
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
        _currentSticker?.ForceClose(); _currentSticker = null;
        _current?.ForceClose();
        var toast = new ToastWindow(filePath != null ? System.IO.Path.GetFileName(filePath) : "Screenshot",
                                     $"{screenshot.Width}x{screenshot.Height}", null);
        toast._previewBitmap = screenshot;
        toast._savedFilePath = filePath;
        toast._hasImagePreview = true;

        // Set image thumbnail — preserve aspect ratio, adapt toast width to image shape
        toast.ImageArea.Visibility = Visibility.Visible;
        double aspect = (double)screenshot.Width / screenshot.Height;
        // Wide images get a wider toast, tall images get a narrower one
        int toastW = (int)Math.Clamp(180 * aspect, 200, 340);
        toast.Root.MaxWidth = toastW;
        toast.Root.MinWidth = Math.Min(200, toastW);
        toast.ImageArea.MaxHeight = (int)Math.Clamp(toastW / aspect, 80, 200);
        var hBmp = screenshot.GetHbitmap();
        try
        {
            var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBmp, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            toast.PreviewImage.Source = src;
        }
        finally { Native.User32.DeleteObject(hBmp); }

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

        // Drag-and-drop: track mouse, start drag on threshold
        System.Windows.Point mouseDownPos = default;
        bool mouseIsDown = false;
        toast.PreviewImage.MouseLeftButtonDown += (_, e) => { mouseDownPos = e.GetPosition(toast); mouseIsDown = true; };
        toast.PreviewImage.MouseMove += (_, e) =>
        {
            if (!mouseIsDown || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
            var diff = e.GetPosition(toast) - mouseDownPos;
            if (Math.Abs(diff.X) < 8 && Math.Abs(diff.Y) < 8) return;
            mouseIsDown = false;
            string? dragFile = filePath;
            if ((dragFile == null || !System.IO.File.Exists(dragFile)) && screenshot != null)
            {
                dragFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"yoink_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                screenshot.Save(dragFile, System.Drawing.Imaging.ImageFormat.Png);
            }
            if (dragFile == null) return;

            // Visual feedback: fade + accent border to show drag active
            toast.Opacity = 0.65;
            var prevBorder = toast.Root.BorderBrush;
            toast.Root.BorderBrush = Theme.Brush(System.Windows.Media.Color.FromArgb(100, 120, 180, 255));
            toast.Root.BorderThickness = new Thickness(2);

            var data = new System.Windows.DataObject();
            data.SetFileDropList(new System.Collections.Specialized.StringCollection { dragFile });
            var result = System.Windows.DragDrop.DoDragDrop(toast, data, System.Windows.DragDropEffects.Copy | System.Windows.DragDropEffects.Move);

            if (result != System.Windows.DragDropEffects.None)
            {
                toast.SlideAway();
            }
            else
            {
                // Cancelled - restore
                toast.Opacity = 1;
                toast.Root.BorderBrush = prevBorder;
                toast.Root.BorderThickness = new Thickness(1);
            }
        };
        toast.PreviewImage.MouseLeftButtonUp += (_, e) =>
        {
            if (!mouseIsDown) return;
            mouseIsDown = false;
            // Click (no drag) — open file location
            OpenFileLocation(filePath);
        };

        // Click on text area also opens file location
        toast.TitleText.Cursor = System.Windows.Input.Cursors.Hand;
        toast.BodyText.Cursor = System.Windows.Input.Cursors.Hand;
        toast.TitleText.MouseLeftButtonDown += (_, _) => OpenFileLocation(filePath);
        toast.BodyText.MouseLeftButtonDown += (_, _) => OpenFileLocation(filePath);

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
        _current?.ForceClose();
        _currentSticker?.ForceClose();
    }

    private const double Edge = 8;

    private static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        using var trimmed = TrimTransparentBounds(bitmap, 18);
        using var ms = new MemoryStream();
        trimmed.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Position = 0;
        var frame = BitmapFrame.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        frame.Freeze();
        return frame;
    }

    private static Bitmap TrimTransparentBounds(Bitmap source, byte alphaThreshold)
    {
        int minX = source.Width;
        int minY = source.Height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                if (source.GetPixel(x, y).A <= alphaThreshold) continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
            return new Bitmap(source);

        var rect = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        return source.Clone(rect, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    }

}
