using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Yoink.UI;

public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _timer;
    private static ToastWindow? _current;

    private ToastWindow(string title, string body)
    {
        InitializeComponent();

        Theme.Refresh();
        Root.Background = Theme.Brush(Theme.BgElevated);
        Root.BorderBrush = Theme.Brush(Theme.BorderSubtle);
        TitleText.Foreground = Theme.Brush(Theme.TextPrimary);
        BodyText.Foreground = Theme.Brush(Theme.TextSecondary);

        TitleText.Text = title;
        BodyText.Text = body;
        if (string.IsNullOrEmpty(body)) BodyText.Visibility = Visibility.Collapsed;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _timer.Tick += (_, _) => { _timer.Stop(); Dismiss(); };

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - ActualWidth - 16;
        Top = wa.Bottom - ActualHeight - 16;

        BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        });

        _timer.Start();
    }

    private void Dismiss()
    {
        var dur = TimeSpan.FromMilliseconds(220);
        var ease = new QuarticEase { EasingMode = EasingMode.EaseIn };

        SlideX.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation { To = 200, Duration = dur, EasingFunction = ease });

        var fade = new DoubleAnimation { To = 0, Duration = dur, EasingFunction = ease };
        fade.Completed += (_, _) => { try { Close(); } catch { } };
        BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>Show a toast notification. Replaces any existing toast.</summary>
    public static void Show(string title, string body = "")
    {
        // Must be called on UI thread
        _current?.Close();
        _current = new ToastWindow(title, body);
        _current.Show();
    }
}
