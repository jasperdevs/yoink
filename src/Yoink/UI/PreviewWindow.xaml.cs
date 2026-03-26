using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Yoink.Services;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace Yoink.UI;

public partial class PreviewWindow : Window
{
    private readonly Bitmap _screenshot;
    private readonly DispatcherTimer _fadeTimer;
    private bool _isFading;
    private bool _isHovered;
    private System.Windows.Point _mouseDownPos;
    private bool _mouseIsDown;

    private static PreviewWindow? _current;

    public PreviewWindow(Bitmap screenshot)
    {
        _current?.ForceClose();
        _current = this;

        _screenshot = screenshot;

        // Auto-copy to clipboard
        ClipboardService.CopyToClipboard(screenshot);

        InitializeComponent();
        ApplyTheme();
        SetThumbnail();

        _fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _fadeTimer.Tick += (_, _) => { _fadeTimer.Stop(); if (!_isHovered) StartFade(); };

        Loaded += OnLoaded;
    }

    private void ApplyTheme()
    {
        Theme.Refresh();
        RootBorder.Background = Theme.Brush(Theme.BgElevated);
        RootBorder.BorderBrush = Theme.Brush(Theme.Border);

        // Icon color
        foreach (var path in FindChildren<System.Windows.Shapes.Path>(RootBorder))
            path.Fill = Theme.Brush(Theme.TextPrimary);

        // Save button bg
        SaveBtn.Background = Theme.Brush(Theme.AccentSubtle);
    }

    private static IEnumerable<T> FindChildren<T>(DependencyObject o) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
        {
            var c = VisualTreeHelper.GetChild(o, i);
            if (c is T t) yield return t;
            foreach (var s in FindChildren<T>(c)) yield return s;
        }
    }

    private void SetThumbnail()
    {
        using var ms = new MemoryStream();
        _screenshot.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.StreamSource = ms;
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        ThumbnailImage.Source = bmp;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - ActualWidth - 16;
        double targetTop = wa.Bottom - ActualHeight - 16;

        var slide = new DoubleAnimation
        {
            From = targetTop + 50, To = targetTop,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var fade = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(180) };
        BeginAnimation(TopProperty, slide);
        BeginAnimation(OpacityProperty, fade);
        _fadeTimer.Start();
    }

    private void StartFade()
    {
        if (_isFading) return;
        _isFading = true;
        var a = new DoubleAnimation
        {
            To = 0, Duration = TimeSpan.FromMilliseconds(2500),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        a.Completed += (_, _) => { if (!_isHovered) ForceClose(); };
        BeginAnimation(OpacityProperty, a);
    }

    private void CancelFade()
    {
        _isFading = false;
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
    }

    private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isHovered = true; _fadeTimer.Stop();
        if (_isFading) CancelFade();
    }

    private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isHovered = false; _mouseIsDown = false;
        _fadeTimer.Interval = TimeSpan.FromSeconds(3);
        _fadeTimer.Start();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _mouseDownPos = e.GetPosition(this);
        _mouseIsDown = true;
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        if (!_mouseIsDown || e.LeftButton != MouseButtonState.Pressed) { base.OnMouseMove(e); return; }
        var diff = e.GetPosition(this) - _mouseDownPos;
        if (Math.Abs(diff.X) > 4 || Math.Abs(diff.Y) > 4)
        {
            _mouseIsDown = false;
            var tmp = Path.Combine(Path.GetTempPath(), $"yoink_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            _screenshot.Save(tmp, ImageFormat.Png);
            var d = new DataObject();
            d.SetFileDropList(new System.Collections.Specialized.StringCollection { tmp });
            DragDrop.DoDragDrop(this, d, DragDropEffects.Copy);
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _mouseIsDown = false;
        base.OnMouseLeftButtonUp(e);
    }

    private void SaveClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _fadeTimer.Stop();
        var dlg = new SaveFileDialog
        {
            Filter = "PNG|*.png|JPEG|*.jpg",
            FileName = $"yoink_{DateTime.Now:yyyyMMdd_HHmmss}.png",
            DefaultExt = ".png"
        };
        if (dlg.ShowDialog() == true)
        {
            var fmt = dlg.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                ? ImageFormat.Jpeg : ImageFormat.Png;
            _screenshot.Save(dlg.FileName, fmt);
        }
        ForceClose();
    }

    private void ForceClose()
    {
        _fadeTimer.Stop();
        if (_current == this) _current = null;
        try { Close(); } catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        _fadeTimer.Stop();
        if (_current == this) _current = null;
        base.OnClosed(e);
    }
}
