using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Yoink.Capture;
using Yoink.Services;
using Yoink.UI;

namespace Yoink;

public partial class App : Application
{
    private HotkeyService? _hotkeyService;
    private SettingsService? _settingsService;
    private TrayIcon? _trayIcon;
    private HiddenHotkeyWindow? _hotkeyWindow;
    private bool _isCapturing;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsService = new SettingsService();
        _settingsService.Load();

        _trayIcon = new TrayIcon();
        _trayIcon.OnSettings += ShowSettings;
        _trayIcon.OnQuit += () => Shutdown();

        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        // Create a hidden window to own the hotkey message pump.
        // It needs a real size (1x1) and to be shown/hidden to force HWND creation.
        _hotkeyWindow = new HiddenHotkeyWindow();
        _hotkeyWindow.Show();

        var hwnd = new WindowInteropHelper(_hotkeyWindow).EnsureHandle();
        Debug.WriteLine($"[Yoink] Hidden window HWND: {hwnd}");

        _hotkeyWindow.Hide();

        var settings = _settingsService.Settings;
        bool registered = _hotkeyService.Register(hwnd, settings.HotkeyModifiers, settings.HotkeyKey);
        Debug.WriteLine($"[Yoink] RegisterHotKey result: {registered}");

        if (!registered)
        {
            int err = Native.User32.GetLastError();
            Debug.WriteLine($"[Yoink] GetLastError: {err}");
            _trayIcon.ShowBalloon("Yoink", "Failed to register hotkey (Alt+`). Another app may be using it.",
                System.Windows.Forms.ToolTipIcon.Warning);
        }
        else
        {
            _trayIcon.ShowBalloon("Yoink", "Ready! Press Alt+` to yoink.",
                System.Windows.Forms.ToolTipIcon.Info);
        }
    }

    private void OnHotkeyPressed()
    {
        Debug.WriteLine("[Yoink] Hotkey pressed!");

        if (_isCapturing)
            return;

        _isCapturing = true;

        try
        {
            StartCapture();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Yoink] Capture error: {ex}");
            _trayIcon?.ShowBalloon("Yoink Error", ex.Message, System.Windows.Forms.ToolTipIcon.Error);
            _isCapturing = false;
        }
    }

    private void StartCapture()
    {
        // Small delay to let the hotkey keys release visually
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            DoCapture();
        };
        timer.Start();
    }

    private void DoCapture()
    {
        Bitmap? screenshot = null;

        try
        {
            var (bmp, bounds) = ScreenCapture.CaptureAllScreens();
            screenshot = bmp;
            Debug.WriteLine($"[Yoink] Captured {bounds.Width}x{bounds.Height} at ({bounds.X},{bounds.Y})");

            var overlayThread = new Thread(() =>
            {
                System.Windows.Forms.Application.EnableVisualStyles();

                var overlay = new RegionOverlayForm(screenshot, bounds);

                overlay.RegionSelected += selection =>
                {
                    Debug.WriteLine($"[Yoink] Region selected: {selection}");
                    overlay.Hide();

                    using var cropped = ScreenCapture.CropRegion(screenshot, selection);
                    ClipboardService.CopyToClipboard(cropped);
                    Debug.WriteLine("[Yoink] Copied to clipboard");

                    overlay.Close();
                    System.Windows.Forms.Application.ExitThread();
                };

                overlay.SelectionCancelled += () =>
                {
                    Debug.WriteLine("[Yoink] Selection cancelled");
                    overlay.Close();
                    System.Windows.Forms.Application.ExitThread();
                };

                overlay.FormClosed += (_, _) =>
                {
                    screenshot.Dispose();
                    Dispatcher.Invoke(() => _isCapturing = false);
                };

                System.Windows.Forms.Application.Run(overlay);
            });

            overlayThread.SetApartmentState(ApartmentState.STA);
            overlayThread.IsBackground = true;
            overlayThread.Start();
        }
        catch
        {
            screenshot?.Dispose();
            _isCapturing = false;
            throw;
        }
    }

    private void ShowSettings()
    {
        _trayIcon?.ShowBalloon("Yoink", "Settings coming soon!", System.Windows.Forms.ToolTipIcon.Info);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        _hotkeyWindow?.Close();
        base.OnExit(e);
    }
}

/// <summary>
/// Invisible window that exists solely to provide an HWND for receiving WM_HOTKEY messages.
/// Needs a real (small) size so Windows creates a proper native window.
/// </summary>
internal sealed class HiddenHotkeyWindow : Window
{
    public HiddenHotkeyWindow()
    {
        Width = 1;
        Height = 1;
        Left = -9999;
        Top = -9999;
        WindowStyle = WindowStyle.None;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
    }
}
