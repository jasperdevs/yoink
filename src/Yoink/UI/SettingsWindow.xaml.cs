using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yoink.Models;
using Yoink.Services;
using RadioButton = System.Windows.Controls.RadioButton;
using Button = System.Windows.Controls.Button;

namespace Yoink.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly HistoryService _historyService;
    private bool _isRecordingHotkey;

    public event Action? HotkeyChanged;

    public SettingsWindow(SettingsService settingsService, HistoryService historyService)
    {
        _settingsService = settingsService;
        _historyService = historyService;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = _settingsService.Settings;
        HotkeyBox.Text = FormatHotkey(s.HotkeyModifiers, s.HotkeyKey);
        AfterCaptureCombo.SelectedIndex = (int)s.AfterCapture;
        SaveToFileCheck.IsChecked = s.SaveToFile;
        SaveDirBox.Text = s.SaveDirectory;
        SaveDirPanel.Visibility = s.SaveToFile ? Visibility.Visible : Visibility.Collapsed;
        StartWithWindowsCheck.IsChecked = s.StartWithWindows;
        SaveHistoryCheck.IsChecked = s.SaveHistory;
    }

    // ─── Tabs ──────────────────────────────────────────────────────

    private void TabChanged(object sender, RoutedEventArgs e)
    {
        if (SettingsTab?.IsChecked == true)
        {
            SettingsPanel.Visibility = Visibility.Visible;
            HistoryPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            SettingsPanel.Visibility = Visibility.Collapsed;
            HistoryPanel.Visibility = Visibility.Visible;
            LoadHistory();
        }
    }

    // ─── Hotkey ────────────────────────────────────────────────────

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _isRecordingHotkey = true;
        HotkeyBox.Text = "Press keys...";
        HotkeyHint.Visibility = Visibility.Visible;
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _isRecordingHotkey = false;
        HotkeyHint.Visibility = Visibility.Collapsed;
        HotkeyBox.Text = FormatHotkey(
            _settingsService.Settings.HotkeyModifiers,
            _settingsService.Settings.HotkeyKey);
    }

    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_isRecordingHotkey) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            or Key.Escape) return;

        // Escape cancels recording
        if (e.Key == Key.Escape)
        {
            _isRecordingHotkey = false;
            HotkeyHint.Visibility = Visibility.Collapsed;
            HotkeyBox.Text = FormatHotkey(
                _settingsService.Settings.HotkeyModifiers,
                _settingsService.Settings.HotkeyKey);
            Keyboard.ClearFocus();
            return;
        }

        uint modifiers = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= Native.User32.MOD_ALT;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= Native.User32.MOD_CONTROL;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= Native.User32.MOD_SHIFT;
        if (modifiers == 0) return; // require at least one modifier

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        _settingsService.Settings.HotkeyModifiers = modifiers;
        _settingsService.Settings.HotkeyKey = vk;
        _settingsService.Save();

        HotkeyBox.Text = FormatHotkey(modifiers, vk);
        _isRecordingHotkey = false;
        HotkeyHint.Visibility = Visibility.Collapsed;
        FocusManager.SetFocusedElement(this, this);
        Keyboard.ClearFocus();
        HotkeyChanged?.Invoke();
    }

    // ─── Settings controls ─────────────────────────────────────────

    private void AfterCaptureCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _settingsService.Settings.AfterCapture = (AfterCaptureAction)AfterCaptureCombo.SelectedIndex;
        _settingsService.Save();
    }

    private void SaveToFileCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        bool on = SaveToFileCheck.IsChecked == true;
        _settingsService.Settings.SaveToFile = on;
        SaveDirPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        _settingsService.Save();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Choose where to save screenshots",
            SelectedPath = _settingsService.Settings.SaveDirectory,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _settingsService.Settings.SaveDirectory = dialog.SelectedPath;
            SaveDirBox.Text = dialog.SelectedPath;
            _settingsService.Save();
        }
    }

    private void StartWithWindowsCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        bool on = StartWithWindowsCheck.IsChecked == true;
        _settingsService.Settings.StartWithWindows = on;
        _settingsService.Save();
        SetStartWithWindows(on);
    }

    private void SaveHistoryCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _settingsService.Settings.SaveHistory = SaveHistoryCheck.IsChecked == true;
        _settingsService.Save();
    }

    private static void SetStartWithWindows(bool enable)
    {
        const string keyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyName, true);
        if (key is null) return;
        if (enable)
        {
            var exe = Environment.ProcessPath;
            if (exe is not null) key.SetValue("Yoink", $"\"{exe}\"");
        }
        else key.DeleteValue("Yoink", false);
    }

    // ─── History ───────────────────────────────────────────────────

    private void LoadHistory()
    {
        var items = _historyService.Entries.Select(e => new HistoryItemVM
        {
            Entry = e,
            ThumbPath = e.FilePath,
            Dimensions = $"{e.Width} x {e.Height}",
            TimeAgo = FormatTimeAgo(e.CapturedAt)
        }).ToList();

        HistoryItems.ItemsSource = items;
        HistoryCountText.Text = $"{items.Count} capture{(items.Count == 1 ? "" : "s")}";
        HistoryEmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ClearHistoryClick(object sender, RoutedEventArgs e)
    {
        _historyService.ClearAll();
        LoadHistory();
    }

    private void HistoryItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is HistoryItemVM vm)
        {
            if (!File.Exists(vm.Entry.FilePath)) return;

            // Open fullscreen viewer
            var viewer = new ImageViewerWindow(vm.Entry.FilePath, _historyService, vm.Entry);
            viewer.Owner = this;
            viewer.ShowDialog();
            LoadHistory(); // refresh in case it was deleted
        }
    }

    private static string FormatTimeAgo(DateTime dt)
    {
        var diff = DateTime.Now - dt;
        if (diff.TotalSeconds < 60) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return dt.ToString("MMM d, yyyy");
    }

    private static string FormatHotkey(uint modifiers, uint vk)
    {
        var parts = new List<string>();
        if ((modifiers & Native.User32.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & Native.User32.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & Native.User32.MOD_SHIFT) != 0) parts.Add("Shift");
        var key = KeyInterop.KeyFromVirtualKey((int)vk);
        parts.Add(key switch { Key.Oem3 => "`", _ => key.ToString() });
        return string.Join(" + ", parts);
    }
}

internal sealed class HistoryItemVM
{
    public HistoryEntry Entry { get; set; } = null!;
    public string ThumbPath { get; set; } = "";
    public string Dimensions { get; set; } = "";
    public string TimeAgo { get; set; } = "";
}
