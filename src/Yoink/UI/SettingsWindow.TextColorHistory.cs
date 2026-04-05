using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yoink.Models;
using Yoink.Services;
using Button = System.Windows.Controls.Button;

namespace Yoink.UI;

public partial class SettingsWindow
{
    private string _ocrSearchQuery = "";
    private Grid? _ocrSearchGrid;

    private void LoadOcrHistory()
    {
        // Keep the search box if it already exists, only rebuild entries
        if (_ocrSearchGrid != null && OcrStack.Children.Count > 0 && OcrStack.Children[0] == _ocrSearchGrid)
        {
            // Remove everything except the search box
            while (OcrStack.Children.Count > 1)
                OcrStack.Children.RemoveAt(OcrStack.Children.Count - 1);
        }
        else
        {
            OcrStack.Children.Clear();

            _ocrSearchGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            var searchBox = new System.Windows.Controls.TextBox
            {
                FontSize = 12,
                Padding = new Thickness(8, 6, 8, 6),
                Text = _ocrSearchQuery,
                Foreground = Theme.Brush(Theme.TextPrimary),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(12, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 255, 255, 255)),
                BorderThickness = new Thickness(1),
            };
            var searchPlaceholder = new TextBlock
            {
                Text = "Search text captures...",
                FontSize = 12,
                Opacity = 0.28,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                IsHitTestVisible = false,
                Visibility = string.IsNullOrWhiteSpace(_ocrSearchQuery) ? Visibility.Visible : Visibility.Collapsed
            };
            _ocrSearchGrid.Children.Add(searchBox);
            _ocrSearchGrid.Children.Add(searchPlaceholder);
            OcrStack.Children.Add(_ocrSearchGrid);

            searchBox.TextChanged += (_, _) =>
            {
                _ocrSearchQuery = searchBox.Text ?? "";
                searchPlaceholder.Visibility = string.IsNullOrWhiteSpace(_ocrSearchQuery) ? Visibility.Visible : Visibility.Collapsed;
                LoadOcrHistory();
            };
            searchBox.GotKeyboardFocus += (_, _) => searchPlaceholder.Visibility = Visibility.Collapsed;
            searchBox.LostKeyboardFocus += (_, _) =>
                searchPlaceholder.Visibility = string.IsNullOrWhiteSpace(searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        var allEntries = _historyService.OcrEntries;

        // Filter entries by search query
        var query = _ocrSearchQuery.Trim();
        var entries = string.IsNullOrWhiteSpace(query)
            ? allEntries
            : allEntries.Where(e => e.Text.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        HistoryEmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryEmptyLabel.Text = allEntries.Count == 0 ? "No text captures yet"
            : entries.Count == 0 ? "No text captures match your search" : "";

        if (string.IsNullOrWhiteSpace(query))
            HistoryCountText.Text = $"{entries.Count} text capture{(entries.Count == 1 ? "" : "s")}";
        else
            HistoryCountText.Text = $"{entries.Count} of {allEntries.Count} text capture{(allEntries.Count == 1 ? "" : "s")}";

        DeleteSelectedBtn.Visibility = _selectMode && HistoryCategoryCombo.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;

        var groups = entries.GroupBy(e => e.CapturedAt.Date).OrderByDescending(g => g.Key);
        foreach (var group in groups)
        {
            string label = group.Key == DateTime.Today ? "Today"
                : group.Key == DateTime.Today.AddDays(-1) ? "Yesterday"
                : group.Key.ToString("MMMM d, yyyy");

            if (OcrStack.Children.Count > 0)
            {
                OcrStack.Children.Add(new Border
                {
                    Height = 1,
                    Background = Theme.Brush(Theme.BorderSubtle),
                    Margin = new Thickness(6, 14, 6, 0)
                });
            }

            OcrStack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
                Foreground = Theme.Brush(Theme.TextPrimary),
                Opacity = 0.45,
                Margin = new Thickness(6, 12, 0, 6)
            });

            foreach (var entry in group)
            {
                var card = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 0, 0, 4),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(12, 255, 255, 255)),
                };

                card.MouseEnter += (_, _) => card.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(24, 255, 255, 255));
                card.MouseLeave += (_, _) => card.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(12, 255, 255, 255));

                var capturedText = entry.Text;
                bool isLong = capturedText.Length > 120;
                bool expanded = false;

                var textBlock = new System.Windows.Controls.TextBox
                {
                    Text = isLong ? capturedText[..120] + "..." : capturedText,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 60,
                    IsReadOnly = true,
                    BorderThickness = new Thickness(0),
                    Background = System.Windows.Media.Brushes.Transparent,
                    Padding = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.IBeam,
                    Foreground = Theme.Brush(Theme.TextPrimary),
                };

                var bottomRow = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };

                var timeLabel = new TextBlock
                {
                    Text = FormatTimeAgo(entry.CapturedAt),
                    FontSize = 10,
                    Opacity = 0.3,
                    VerticalAlignment = VerticalAlignment.Center
                };
                bottomRow.Children.Add(timeLabel);

                var btnPanel = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right
                };
                DockPanel.SetDock(btnPanel, Dock.Right);

                if (isLong)
                {
                    var showMoreBtn = new Button
                    {
                        Content = "Show more",
                        FontSize = 10,
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = System.Windows.Input.Cursors.Hand
                    };
                    showMoreBtn.Click += (_, _) =>
                    {
                        expanded = !expanded;
                        if (expanded)
                        {
                            textBlock.Text = capturedText;
                            textBlock.MaxHeight = double.PositiveInfinity;
                            showMoreBtn.Content = "Show less";
                        }
                        else
                        {
                            textBlock.Text = capturedText[..120] + "...";
                            textBlock.MaxHeight = 60;
                            showMoreBtn.Content = "Show more";
                        }
                    };
                    btnPanel.Children.Add(showMoreBtn);
                }

                var copyBtn = new Button
                {
                    Content = "Copy all",
                    FontSize = 10,
                    Padding = new Thickness(6, 2, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                copyBtn.Click += (_, _) =>
                {
                    ClipboardService.CopyTextToClipboard(capturedText);
                    ToastWindow.Show("Copied", "Text copied");
                };
                btnPanel.Children.Add(copyBtn);

                bottomRow.Children.Insert(0, btnPanel);

                var textStack = new StackPanel();
                textStack.Children.Add(textBlock);
                textStack.Children.Add(bottomRow);

                var badge = CreateSelectionBadge(false);
                var root = new Grid();
                root.Children.Add(textStack);
                root.Children.Add(badge);
                card.Child = root;

                bool selected = false;
                if (_selectMode)
                {
                    card.Cursor = System.Windows.Input.Cursors.Hand;
                    card.BorderThickness = new Thickness(0);
                    card.MouseLeftButtonDown += (_, e) =>
                    {
                        e.Handled = true;
                        selected = !selected;
                        card.Tag = selected;
                        UpdateSelectableCardSelection(card, badge, selected);
                    };
                }

                UpdateSelectableCardSelection(card, badge, selected);
                OcrStack.Children.Add(card);
            }
        }
    }

    private void LoadColorHistory()
    {
        ColorStack.Children.Clear();
        var entries = _historyService.ColorEntries;
        HistoryEmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryEmptyLabel.Text = "No colors yet";
        HistoryCountText.Text = $"{entries.Count} color{(entries.Count == 1 ? "" : "s")}";
        DeleteSelectedBtn.Visibility = _selectMode && HistoryCategoryCombo.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

        var groups = entries.GroupBy(e => e.CapturedAt.Date).OrderByDescending(g => g.Key);
        foreach (var group in groups)
        {
            string label = group.Key == DateTime.Today ? "Today"
                : group.Key == DateTime.Today.AddDays(-1) ? "Yesterday"
                : group.Key.ToString("MMMM d, yyyy");

            if (ColorStack.Children.Count > 0)
            {
                ColorStack.Children.Add(new Border
                {
                    Height = 1,
                    Background = Theme.Brush(Theme.BorderSubtle),
                    Margin = new Thickness(6, 14, 6, 0)
                });
            }

            ColorStack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
                Foreground = Theme.Brush(Theme.TextPrimary),
                Opacity = 0.45,
                Margin = new Thickness(6, 12, 0, 6)
            });

            foreach (var entry in group)
            {
                byte r = 0, g = 0, b = 0;
                try
                {
                    r = Convert.ToByte(entry.Hex[..2], 16);
                    g = Convert.ToByte(entry.Hex[2..4], 16);
                    b = Convert.ToByte(entry.Hex[4..6], 16);
                }
                catch { }

                var swatchColor = System.Windows.Media.Color.FromRgb(r, g, b);

                // Full-width row card
                var card = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 3),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(12, 255, 255, 255)),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                card.MouseEnter += (_, _) => card.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(24, 255, 255, 255));
                card.MouseLeave += (_, _) => card.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(12, 255, 255, 255));

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Color swatch circle
                var swatch = new Border
                {
                    Width = 36,
                    Height = 36,
                    CornerRadius = new CornerRadius(18),
                    Background = new SolidColorBrush(swatchColor),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 255, 255, 255)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 12, 0)
                };
                Grid.SetColumn(swatch, 0);
                grid.Children.Add(swatch);

                // Hex + time info
                var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                infoStack.Children.Add(new TextBlock
                {
                    Text = $"#{entry.Hex}",
                    FontSize = 13,
                    FontWeight = FontWeights.Medium,
                    Foreground = Theme.Brush(Theme.TextPrimary),
                    FontFamily = new System.Windows.Media.FontFamily("Cascadia Code, Consolas, Segoe UI Variable Text"),
                });
                infoStack.Children.Add(new TextBlock
                {
                    Text = $"RGB({r}, {g}, {b}) · {FormatTimeAgo(entry.CapturedAt)}",
                    FontSize = 10,
                    Opacity = 0.35,
                    Margin = new Thickness(0, 1, 0, 0)
                });
                Grid.SetColumn(infoStack, 1);
                grid.Children.Add(infoStack);

                // Copy button
                var copyBtn = new Button
                {
                    Content = "Copy",
                    FontSize = 10,
                    Padding = new Thickness(8, 3, 8, 3),
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                var capturedHex = entry.Hex;
                copyBtn.Click += (_, _) =>
                {
                    ClipboardService.CopyTextToClipboard(capturedHex);
                    ToastWindow.Show("Copied", capturedHex);
                };
                Grid.SetColumn(copyBtn, 2);
                grid.Children.Add(copyBtn);

                var badge = CreateSelectionBadge(false);
                var root = new Grid();
                root.Children.Add(grid);
                root.Children.Add(badge);
                card.Child = root;

                var selected = false;
                if (_selectMode)
                {
                    card.BorderThickness = new Thickness(0);
                    card.MouseLeftButtonDown += (_, e) =>
                    {
                        e.Handled = true;
                        selected = !selected;
                        card.Tag = selected ? entry : null;
                        UpdateSelectableCardSelection(card, badge, selected);
                    };
                }
                else
                {
                    card.MouseLeftButtonDown += (_, e) =>
                    {
                        e.Handled = true;
                        ClipboardService.CopyTextToClipboard(capturedHex);
                        ToastWindow.Show("Copied", capturedHex);
                    };
                }

                UpdateSelectableCardSelection(card, badge, selected);
                ColorStack.Children.Add(card);
            }
        }
    }

    private static void UpdateSelectableCardSelection(Border card, Border badge, bool selected)
    {
        card.BorderThickness = new Thickness(selected ? Theme.StrokeThickness : 0);
        card.BorderBrush = selected ? Theme.StrokeBrush() : System.Windows.Media.Brushes.Transparent;
        badge.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
    }
}
