using System.Windows;
using System.Windows.Threading;
using Yoink.Services;
using Yoink.UI;

namespace Yoink;

public partial class App : Application
{
    private static Mutex? _mutex;
    private HotkeyService? _hotkeyService;
    private SettingsService? _settingsService;
    private HistoryService? _historyService;
    private ImageSearchIndexService? _imageSearchIndexService;
    private readonly object _historyGate = new();
    private TrayIcon? _trayIcon;
    private SettingsWindow? _settingsWindow;
    private DispatcherTimer? _idleTrimTimer;
    private int _activeUploadCount;
    private int _isCapturing;
    private bool _historyRecovered;
    private bool _historyChangedHooked;
    private bool _historyMaintenanceScheduled;
    private int _settingsWindowOpening;
}
