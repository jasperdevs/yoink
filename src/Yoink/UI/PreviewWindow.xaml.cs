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

    // Drag state
    private System.Windows.Point _mouseDownPos;
    private bool _mouseIsDown;
    private bool _isDragging;
    private readonly DispatcherTimer _dragTimer;
    private double _dragTargetX, _dragTargetY;
    private double _dragCurrentX, _dragCurrentY;
    private string? _dragTempFile;
    private double _originalLeft, _originalTop;

    private static PreviewWindow? _current;

    public PreviewWindow(Bitmap screenshot)
    {
        _current?.ForceClose();
        _current = this;

        _screenshot = screenshot;
        ClipboardService.CopyToClipboard(screenshot);

        InitializeComponent();
        ApplyTheme();
        SetThumbnail();

        _fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _fadeTimer.Tick += (_, _) => { _fadeTimer.Stop(); if (!_isHovered) StartFade(); };

        // 60fps drag tracking timer
        _dragTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _dragTimer.Tick += DragTick;

        Loaded += OnLoaded;
    }

    private void ApplyTheme()
    {
        Theme.Refresh();
        RootBorder.Background = Theme.Brush(Theme.BgElevated);
        RootBorder.BorderBrush = Theme.Brush(Theme.BorderSubtle);
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

        BeginAnimation(TopProperty, new DoubleAnimation
        {
            From = targetTop + 50, To = targetTop,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(180)
        });
        _fadeTimer.Start();
    }

    // ─── Fade ──────────────────────────────────────────────────────

    private void StartFade()
    {
        if (_isFading || _isDragging) return;
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

    // ─── Hover ─────────────────────────────────────────────────────

    private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isHovered = true;
        _fadeTimer.Stop();
        if (_isFading) CancelFade();
        AnimateButtons(1);
    }

    private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isHovered = false;
        _mouseIsDown = false;
        AnimateButtons(0);
        if (!_isDragging)
        {
            _fadeTimer.Interval = TimeSpan.FromSeconds(3);
            _fadeTimer.Start();
        }
    }

    private void AnimateButtons(double to)
    {
        var dur = TimeSpan.FromMilliseconds(120);
        CloseBtn.BeginAnimation(OpacityProperty, new DoubleAnimation { To = to, Duration = dur });
        SaveBtn.BeginAnimation(OpacityProperty, new DoubleAnimation { To = to, Duration = dur });
    }

    // ─── Animated Drag ─────────────────────────────────────────────

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // Ignore if clicking on buttons
        if (e.OriginalSource is FrameworkElement fe &&
            (fe.Name == "CloseBtn" || fe.Name == "SaveBtn" ||
             IsChildOf(fe, CloseBtn) || IsChildOf(fe, SaveBtn)))
        {
            base.OnMouseLeftButtonDown(e);
            return;
        }

        _mouseDownPos = PointToScreen(e.GetPosition(this));
        _mouseIsDown = true;
        _isDragging = false;
        CaptureMouse();
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        if (!_mouseIsDown || e.LeftButton != MouseButtonState.Pressed)
        {
            base.OnMouseMove(e);
            return;
        }

        if (!_isDragging)
        {
            var screenPos = PointToScreen(e.GetPosition(this));
            var diff = screenPos - _mouseDownPos;
            if (Math.Abs(diff.X) > 6 || Math.Abs(diff.Y) > 6)
                EnterDragMode();
        }

        base.OnMouseMove(e);
    }

    private void EnterDragMode()
    {
        _isDragging = true;
        _fadeTimer.Stop();
        if (_isFading) CancelFade();

        // Cancel any position/opacity animations
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;

        _originalLeft = Left;
        _originalTop = Top;
        _dragCurrentX = Left;
        _dragCurrentY = Top;

        // Prepare temp file for drop
        _dragTempFile = Path.Combine(Path.GetTempPath(), $"yoink_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        _screenshot.Save(_dragTempFile, ImageFormat.Png);

        // Visual: scale down, boost shadow, slight rotation
        var dur = TimeSpan.FromMilliseconds(150);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        DragScale.CenterX = ActualWidth / 2;
        DragScale.CenterY = ActualHeight / 2;
        DragRotate.CenterX = ActualWidth / 2;
        DragRotate.CenterY = ActualHeight / 2;

        DragScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation { To = 0.88, Duration = dur, EasingFunction = ease });
        DragScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation { To = 0.88, Duration = dur, EasingFunction = ease });
        RootShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty,
            new DoubleAnimation { To = 30, Duration = dur });
        RootShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ShadowDepthProperty,
            new DoubleAnimation { To = 10, Duration = dur });

        // Hide buttons during drag
        AnimateButtons(0);

        _dragTimer.Start();
    }

    private void DragTick(object? sender, EventArgs e)
    {
        if (!_isDragging) return;

        // Get cursor position
        Native.User32.GetCursorPos(out var pt);
        _dragTargetX = pt.X - ActualWidth / 2;
        _dragTargetY = pt.Y - ActualHeight / 2;

        // Elastic lerp toward cursor
        const double spring = 0.25;
        _dragCurrentX += (_dragTargetX - _dragCurrentX) * spring;
        _dragCurrentY += (_dragTargetY - _dragCurrentY) * spring;

        Left = _dragCurrentX;
        Top = _dragCurrentY;

        // Subtle rotation based on horizontal velocity
        double vx = _dragTargetX - _dragCurrentX;
        double targetAngle = Math.Clamp(vx * 0.15, -4, 4);
        double currentAngle = DragRotate.Angle;
        DragRotate.Angle = currentAngle + (targetAngle - currentAngle) * 0.2;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        _mouseIsDown = false;

        if (_isDragging)
        {
            _isDragging = false;
            _dragTimer.Stop();

            // Do the OLE drop with the temp file
            if (_dragTempFile != null && File.Exists(_dragTempFile))
            {
                try
                {
                    var data = new DataObject();
                    data.SetFileDropList(new System.Collections.Specialized.StringCollection { _dragTempFile });
                    DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);
                }
                catch { }
            }

            // Dismiss with animation
            AnimateDismiss();
        }

        base.OnMouseLeftButtonUp(e);
    }

    private void AnimateDismiss()
    {
        var dur = TimeSpan.FromMilliseconds(200);
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

        DragScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation { To = 0.3, Duration = dur, EasingFunction = ease });
        DragScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation { To = 0.3, Duration = dur, EasingFunction = ease });

        var fadeOut = new DoubleAnimation { To = 0, Duration = dur, EasingFunction = ease };
        fadeOut.Completed += (_, _) => ForceClose();
        BeginAnimation(OpacityProperty, fadeOut);
    }

    private static bool IsChildOf(DependencyObject child, DependencyObject parent)
    {
        var current = child;
        while (current != null)
        {
            if (current == parent) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    // ─── Buttons ───────────────────────────────────────────────────

    private void CloseClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ForceClose();
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
        _dragTimer.Stop();
        if (_current == this) _current = null;
        try { Close(); } catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        _fadeTimer.Stop();
        _dragTimer.Stop();
        if (_current == this) _current = null;
        base.OnClosed(e);
    }
}
