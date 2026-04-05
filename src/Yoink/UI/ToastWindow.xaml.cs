using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Drawing;
using System.IO;
using Yoink.Helpers;
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
    private string? _savedFilePath;
    private Bitmap? _previewBitmap;
    private bool _isDragging;
    private System.Windows.Point _mouseDownPos;
    private System.Windows.Media.Brush? _dragBorderBrush;
    private Thickness _dragBorderThickness;

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
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            if (ToastPinPolicy.CanAutoDismiss(_isPinned, _isHovered))
                SlideAway();
        };

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
            if (_isPinned)
            {
                _timer.Stop();
                return;
            }
            var remaining = Math.Max(0.1, ProgressScale.ScaleX * _durationSeconds);
            ProgressScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
                new DoubleAnimation { To = 0, Duration = TimeSpan.FromSeconds(remaining) });
            _timer.Interval = TimeSpan.FromSeconds(remaining);
            _timer.Start();
        };
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        Cursor = System.Windows.Input.Cursors.Hand;
        SourceInitialized += (_, _) => PopupWindowHelper.ApplyNoActivateChrome(this);
        Loaded += OnLoaded;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsChildOf(e.OriginalSource as DependencyObject, CloseBtn) ||
            IsChildOf(e.OriginalSource as DependencyObject, PinBtn) ||
            IsChildOf(e.OriginalSource as DependencyObject, SaveBtn))
        {
            return;
        }

        _mouseDownPos = e.GetPosition(this);
        _isDragging = false;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
            return;

        var diff = e.GetPosition(this) - _mouseDownPos;
        if (!_isDragging && Math.Abs(diff.X) < 5 && Math.Abs(diff.Y) < 5)
            return;

        if (!_isDragging)
        {
            _isDragging = true;
            BeginDragFeedback();
        }

        var dragFile = GetDragFilePath();
        if (dragFile is null)
        {
            EndDragFeedback(cancelled: false);
            ReleaseMouseCapture();
            SlideAway();
            return;
        }

        try
        {
            var data = new System.Windows.DataObject();
            data.SetFileDropList(new System.Collections.Specialized.StringCollection { dragFile });
            var result = System.Windows.DragDrop.DoDragDrop(this, data, System.Windows.DragDropEffects.Copy | System.Windows.DragDropEffects.Move);
            if (result != System.Windows.DragDropEffects.None)
                SlideAway();
            else
                EndDragFeedback(cancelled: true);
        }
        finally
        {
            if (_savedFilePath is null && File.Exists(dragFile))
            {
                try { File.Delete(dragFile); } catch { }
            }

            _isDragging = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsMouseCaptured)
            return;

        ReleaseMouseCapture();
        if (_isDragging)
            return;

        if (_savedFilePath != null && File.Exists(_savedFilePath))
        {
            OpenFileLocation(_savedFilePath);
            return;
        }

        SlideAway();
    }

    private void BeginDragFeedback()
    {
        _dragBorderBrush = Root.BorderBrush;
        _dragBorderThickness = Root.BorderThickness;
        Root.BorderBrush = Theme.Brush(System.Windows.Media.Color.FromArgb(120, 120, 180, 255));
        Root.BorderThickness = new Thickness(1.5);
        DragScale.CenterX = ActualWidth / 2;
        DragScale.CenterY = ActualHeight / 2;
        DragScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
            new DoubleAnimation { To = 0.96, Duration = TimeSpan.FromMilliseconds(140), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut } });
        DragScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
            new DoubleAnimation { To = 0.96, Duration = TimeSpan.FromMilliseconds(140), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut } });
        BeginAnimation(OpacityProperty, new DoubleAnimation { To = 0.82, Duration = TimeSpan.FromMilliseconds(140) });
    }

    private void EndDragFeedback(bool cancelled)
    {
        if (_dragBorderBrush is not null)
            Root.BorderBrush = _dragBorderBrush;
        Root.BorderThickness = _dragBorderThickness;
        _dragBorderBrush = null;
        DragScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
            new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(120) });
        DragScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty,
            new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(120) });
        BeginAnimation(OpacityProperty, new DoubleAnimation { To = 1, Duration = TimeSpan.FromMilliseconds(120) });
    }

    private static bool IsChildOf(DependencyObject? child, DependencyObject parent)
    {
        while (child != null)
        {
            if (child == parent) return true;
            child = VisualTreeHelper.GetParent(child);
        }
        return false;
    }

    private string? GetDragFilePath()
    {
        if (_savedFilePath != null && File.Exists(_savedFilePath))
            return _savedFilePath;

        if (_previewBitmap is null)
            return null;

        var temp = Path.Combine(Path.GetTempPath(), $"yoink_toast_{Guid.NewGuid():N}.png");
        _previewBitmap.Save(temp, System.Drawing.Imaging.ImageFormat.Png);
        return temp;
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
            slide.Completed += (_, _) => TryForceClose();
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
            slide.Completed += (_, _) => TryForceClose();
            BeginAnimation(TopProperty, (DoubleAnimation)slide);
        }

        BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            To = 0, Duration = dur, EasingFunction = ease
        });
    }

    private bool TryForceClose(bool force = false)
    {
        _timer.Stop();
        if (_isPinned && !force)
            return false;

        if (_current == this) _current = null;
        try { Close(); } catch { }
        return true;
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

}
