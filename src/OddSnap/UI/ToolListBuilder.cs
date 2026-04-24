using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OddSnap.Helpers;
using OddSnap.Models;
using OddSnap.Services;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using Cursors = System.Windows.Input.Cursors;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace OddSnap.UI;

/// <summary>
/// Shared builder that creates the unified tool list (icon + checkbox + hotkey box)
/// used by both SettingsWindow and SetupWizard.
/// </summary>
public static class ToolListBuilder
{
    public static readonly (string id, string label, char icon)[] ExtraTools =
    {
        ("_fullscreen",    "Fullscreen capture",  ToolGlyphs.FullscreenGlyph),
        ("_activeWindow",  "Active window",       ToolGlyphs.ActiveWindowGlyph),
        ("_scrollCapture", "Scroll capture",      ToolGlyphs.ScrollCaptureGlyph),
        ("_record",        "Record",              ToolGlyphs.RecordGlyph),
    };

    private static readonly Dictionary<TextBox, bool> RecordingFlags = new();

    public static void Build(StackPanel panel, SettingsService settingsService, FrameworkElement owner, Action? hotkeyChanged = null)
    {
        panel.Children.Clear();
        var s = settingsService.Settings;
        var enabled = s.EnabledTools ?? ToolDef.DefaultEnabledIds();
        // Icon color for rendering Fluent glyphs to bitmaps
        var iconColor = Theme.IsDark ? System.Drawing.Color.FromArgb(225, 255, 255, 255) : System.Drawing.Color.FromArgb(210, 0, 0, 0);
        var segoe = new System.Windows.Media.FontFamily(UiChrome.PreferredFamilyName);

        void AddHeader(string text)
        {
            panel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                FontFamily = segoe,
                Opacity = 0.92,
                Margin = new Thickness(2, 14, 0, 7),
            });
        }

        void AddToolRow(string toolId, string label, char icon, bool hasToolbarToggle, bool showHotkey)
        {
            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 8),
                MinHeight = 64,
                BorderThickness = new Thickness(1),
            };
            card.SetResourceReference(Border.BackgroundProperty, "ThemeCardBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "ThemeWindowBorderBrush");

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            if (icon != '\0')
            {
                var iconFrame = new Border
                {
                    Width = 32,
                    Height = 32,
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 12, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                iconFrame.SetResourceReference(Border.BackgroundProperty, "ThemeTabActiveBrush");
                iconFrame.SetResourceReference(Border.BorderBrushProperty, "ThemeInputBorderBrush");

                var img = new System.Windows.Controls.Image
                {
                    Source = ToolIcons.RenderToolIconWpf(toolId, icon, iconColor, 16),
                    Width = 16,
                    Height = 16,
                    Opacity = 1,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                };
                System.Windows.Media.RenderOptions.SetBitmapScalingMode(img, System.Windows.Media.BitmapScalingMode.HighQuality);
                iconFrame.Child = img;
                left.Children.Add(iconFrame);
            }

            if (hasToolbarToggle)
            {
                var cb = new CheckBox
                {
                    IsChecked = enabled.Contains(toolId),
                    Tag = toolId,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 7, 0),
                    Cursor = Cursors.Hand,
                };
                cb.Checked += (_, _) => SaveEnabledTools(panel, settingsService);
                cb.Unchecked += (_, _) => SaveEnabledTools(panel, settingsService);
                left.Children.Add(cb);
            }

            var labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 12.5,
                FontFamily = segoe,
                VerticalAlignment = VerticalAlignment.Center,
            };
            labelBlock.SetResourceReference(TextBlock.ForegroundProperty, "ThemeTextPrimaryBrush");
            left.Children.Add(labelBlock);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            if (showHotkey)
            {
                var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                var hkBox = new TextBox();
                hkBox.SetResourceReference(TextBox.StyleProperty, "HotkeyBox");
                WireHotkeyBox(hkBox, toolId, settingsService, owner, hotkeyChanged);

                var clearBtn = new Button { Content = "×" };
                clearBtn.SetResourceReference(Button.StyleProperty, "ClearBtn");
                var capturedBox = hkBox;
                var capturedId = toolId;
                clearBtn.Click += (_, _) =>
                {
                    settingsService.Settings.SetToolHotkey(capturedId, 0, 0);
                    settingsService.Save();
                    capturedBox.Text = HotkeyFormatter.Format(settingsService.Settings.GetToolHotkey(capturedId).mod, settingsService.Settings.GetToolHotkey(capturedId).key);
                    hotkeyChanged?.Invoke();
                };

                right.Children.Add(hkBox);
                right.Children.Add(clearBtn);

                Grid.SetColumn(right, 1);
                grid.Children.Add(right);
            }

            card.Child = grid;
            panel.Children.Add(card);
        }

        AddHeader("Capture tools");
        foreach (var t in ToolDef.AllTools.Where(t => t.Group == 0))
            AddToolRow(t.Id, t.Label, t.Icon, true, true);

        AddHeader("Capture actions");
        foreach (var (id, label, icon) in ExtraTools)
            AddToolRow(id, label, icon, false, true);

        AddHeader("Annotation tools");
        foreach (var t in ToolDef.AllTools.Where(t => t.Group == 1))
            AddToolRow(t.Id, t.Label, t.Icon, true, true);

        LocalizationService.ApplyTo(panel, settingsService.Settings.InterfaceLanguage);
    }

    private static void SaveEnabledTools(StackPanel panel, SettingsService svc)
    {
        var enabledIds = new System.Collections.Generic.List<string>();
        foreach (var card in panel.Children.OfType<Border>())
        {
            if (card.Child is not Grid g) continue;
            foreach (var sp in g.Children.OfType<StackPanel>())
            foreach (var cb in sp.Children.OfType<CheckBox>())
            {
                if (cb.Tag is string id && cb.IsChecked == true)
                    enabledIds.Add(id);
            }
        }
        if (!enabledIds.Any(id => ToolDef.AllTools.Any(t => t.Id == id && t.Group == 0)))
            return; // must keep at least one capture tool
        svc.Settings.EnabledTools = enabledIds;
        svc.Save();
    }

    private sealed record HotkeyConflict(string ToolId, string Label, bool IsAiRedirect);

    private static void WireHotkeyBox(TextBox box, string toolId, SettingsService svc, FrameworkElement owner, Action? hotkeyChanged)
    {
        var (mod0, key0) = svc.Settings.GetToolHotkey(toolId);
        box.Text = HotkeyFormatter.Format(mod0, key0);

        void StartRecording()
        {
            RecordingFlags[box] = true;
            box.Text = LocalizationService.Translate("Press keys...");
        }

        void RestoreHotkeyText()
        {
            var (m, k) = svc.Settings.GetToolHotkey(toolId);
            box.Text = HotkeyFormatter.Format(m, k);
        }

        void StopRecording()
        {
            RecordingFlags[box] = false;
            Keyboard.ClearFocus();
        }

        box.PreviewMouseDown += (_, e) =>
        {
            if (!box.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                box.Focus();
            }

            StartRecording();
        };
        box.GotFocus += (_, _) =>
        {
            StartRecording();
        };
        box.LostFocus += (_, _) =>
        {
            RecordingFlags[box] = false;
            RestoreHotkeyText();
        };
        box.Unloaded += (_, _) => RecordingFlags.Remove(box);
        void AcceptKey(Key rawKey)
        {
            if (!RecordingFlags.GetValueOrDefault(box)) return;
            if (rawKey == Key.Escape)
            {
                RestoreHotkeyText();
                StopRecording();
                return;
            }

            if (IsModifierOnly(rawKey))
                return;

            uint mod = HotkeyFormatter.GetActiveModifiers();
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(rawKey);
            if (vk == 0) return;
            if (IsUnsafeModifierlessHotkey(mod, vk))
            {
                ToastWindow.ShowError(
                    "Hotkey needs a modifier",
                    "Use Ctrl, Alt, Shift, or Win with this key. Print Screen can be used by itself.");
                RestoreHotkeyText();
                StopRecording();
                return;
            }

            var conflict = FindHotkeyConflict(svc.Settings, toolId, mod, vk);
            if (conflict != null)
            {
                var combo = HotkeyFormatter.Format(mod, vk);
                if (!ThemedConfirmDialog.Confirm(
                        Window.GetWindow(owner),
                        "Hotkey conflict",
                        $"{combo} is already used by \"{conflict.Label}\".\n\nReplace it?",
                        "Replace",
                        "Cancel",
                        danger: false))
                {
                    RestoreHotkeyText();
                    StopRecording();
                    return;
                }

                ClearHotkeyConflict(svc.Settings, conflict);
            }

            svc.Settings.SetToolHotkey(toolId, mod, vk);
            svc.Save();
            box.Text = HotkeyFormatter.Format(mod, vk);
            StopRecording();
            hotkeyChanged?.Invoke();
        }

        box.PreviewKeyDown += (_, e) =>
        {
            if (!RecordingFlags.GetValueOrDefault(box)) return;
            e.Handled = true;
            var key = NormalizeHotkeyKey(e);
            AcceptKey(key);
        };
        box.PreviewKeyUp += (_, e) =>
        {
            if (!RecordingFlags.GetValueOrDefault(box)) return;
            var key = NormalizeHotkeyKey(e);
            if (key is Key.Snapshot or Key.Pause or Key.Cancel)
            {
                e.Handled = true;
                AcceptKey(key);
            }
        };
    }

    private static Key NormalizeHotkeyKey(System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.ImeProcessed)
            key = e.ImeProcessedKey;
        if (key == Key.DeadCharProcessed)
            key = e.DeadCharProcessedKey;
        return key;
    }

    private static bool IsModifierOnly(Key key) =>
        key is Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    private static bool IsUnsafeModifierlessHotkey(uint mod, uint vk) =>
        mod == 0 && vk != Native.User32.VK_SNAPSHOT;

    private static HotkeyConflict? FindHotkeyConflict(AppSettings settings, string currentToolId, uint mod, uint key)
    {
        foreach (var tool in ToolDef.AllTools)
        {
            if (string.Equals(tool.Id, currentToolId, StringComparison.OrdinalIgnoreCase))
                continue;

            var (existingMod, existingKey) = settings.GetToolHotkey(tool.Id);
            if (existingMod == mod && existingKey == key)
                return new HotkeyConflict(tool.Id, tool.Label, IsAiRedirect: false);
        }

        foreach (var (id, label, _) in ExtraTools)
        {
            if (string.Equals(id, currentToolId, StringComparison.OrdinalIgnoreCase))
                continue;

            var (existingMod, existingKey) = settings.GetToolHotkey(id);
            if (existingMod == mod && existingKey == key)
                return new HotkeyConflict(id, label, IsAiRedirect: false);
        }

        if (settings.AiRedirectHotkeyModifiers == mod && settings.AiRedirectHotkeyKey == key)
            return new HotkeyConflict("", "AI Redirect", IsAiRedirect: true);

        return null;
    }

    private static void ClearHotkeyConflict(AppSettings settings, HotkeyConflict conflict)
    {
        if (conflict.IsAiRedirect)
        {
            settings.AiRedirectHotkeyModifiers = 0;
            settings.AiRedirectHotkeyKey = 0;
            return;
        }

        settings.SetToolHotkey(conflict.ToolId, 0, 0);
    }
}
