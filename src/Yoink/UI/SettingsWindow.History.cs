using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yoink.Helpers;
using Yoink.Models;
using Yoink.Services;
using Image = System.Windows.Controls.Image;
using WpfPoint = System.Windows.Point;

namespace Yoink.UI;

public partial class SettingsWindow
{
    private bool _selectMode;
    private List<HistoryItemVM> _historyItems = new();
    private List<HistoryItemVM> _filteredHistoryItems = new();
    private List<HistoryItemVM> _gifItems = new();
    private List<HistoryItemVM> _stickerItems = new();
    private List<HistoryItemVM> _allHistoryItems = new();
    private Dictionary<string, HistoryItemVM> _allHistoryItemsByPath = new(StringComparer.OrdinalIgnoreCase);
    private List<HistoryItemVM> _allGifItems = new();
    private List<HistoryItemVM> _allStickerItems = new();
    private string _imageSearchQuery = "";
    private bool _suppressImageSearchSourceEvents;
    private CancellationTokenSource? _searchFilterCts;
    private int _searchFilterVersion;
    private int _historyRenderCount;
    private int _gifRenderCount;
    private int _stickerRenderCount;
    private bool _imageSearchRowAutoHidden;
    private const int HistoryPageSize = 60;

    private void LoadHistory()
    {
        HistoryStack.Children.Clear();
        _imageSearchRowAutoHidden = false;

        var entries = _historyService.ImageEntries;
        var selectedPaths = _allHistoryItems
            .Where(i => i.IsSelected)
            .Select(i => i.Entry.FilePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _allHistoryItems = new List<HistoryItemVM>(entries.Count);
        _allHistoryItemsByPath = new Dictionary<string, HistoryItemVM>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            var fileNameSearch = Path.GetFileNameWithoutExtension(e.FileName);
            var searchText = _imageSearchIndexService.BuildSearchText(e.FilePath, e.FileName);

            var vm = new HistoryItemVM
            {
                Entry = e,
                ThumbPath = e.FilePath,
                Dimensions = e.Width > 0 ? $"{e.Width} x {e.Height}" : "",
                TimeAgo = FormatTimeAgo(e.CapturedAt),
                FileNameSearchText = fileNameSearch,
                NormalizedFileNameSearchText = ImageSearchQueryMatcher.Normalize(fileNameSearch),
                SearchText = searchText,
                NormalizedSearchText = ImageSearchQueryMatcher.Normalize(searchText),
                OcrSearchText = "",
                SemanticSearchText = "",
                IsSelected = selectedPaths.Contains(e.FilePath)
            };

            _allHistoryItems.Add(vm);
            _allHistoryItemsByPath[e.FilePath] = vm;
        }

        if (_settingsService.Settings.ShowImageSearchBar)
            RefreshImageSearchTexts();

        if (_settingsService.Settings.AutoIndexImages)
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (IsLoaded && HistoryTab.IsChecked == true && HistoryCategoryCombo.SelectedIndex == 0)
                        _imageSearchIndexService.RequestSync(entries, _settingsService.Settings.OcrLanguageTag);
                }
                catch { }
            }, System.Windows.Threading.DispatcherPriority.Background);
        ApplyImageSearchFilter();
        DeleteSelectedBtn.Visibility = _selectMode ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderHistoryItems()
    {
        HistoryStack.Children.Clear();
        _historyItems = _filteredHistoryItems.Take(_historyRenderCount).ToList();
        var groups = _historyItems.GroupBy(i => i.Entry.CapturedAt.Date).OrderByDescending(g => g.Key);
        foreach (var group in groups)
        {
            string label = group.Key == DateTime.Today ? "Today"
                : group.Key == DateTime.Today.AddDays(-1) ? "Yesterday"
                : group.Key.ToString("MMMM d, yyyy");

            if (HistoryStack.Children.Count > 0)
            {
                HistoryStack.Children.Add(new Border
                {
                    Height = 1,
                    Background = Theme.Brush(Theme.BorderSubtle),
                    Margin = new Thickness(6, 14, 6, 0)
                });
            }

            HistoryStack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new System.Windows.Media.FontFamily(UiChrome.PreferredFamilyName),
                Foreground = Theme.Brush(Theme.TextPrimary),
                Opacity = 0.45,
                Margin = new Thickness(6, 12, 0, 6)
            });

            var wrap = new WrapPanel();
            foreach (var item in group)
                wrap.Children.Add(CreateHistoryCard(item));
            HistoryStack.Children.Add(wrap);
        }
    }

    private Border CreateHistoryCard(HistoryItemVM vm)
    {
        var shell = BuildMediaCardShell(vm, () =>
        {
            if (!string.IsNullOrEmpty(vm.Entry.UploadUrl))
            {
                ClipboardService.CopyTextToClipboard(vm.Entry.UploadUrl);
                ToastWindow.Show("Copied", vm.Entry.UploadUrl);
                return;
            }
            if (!File.Exists(vm.Entry.FilePath)) return;
            using var bmp = new Bitmap(vm.Entry.FilePath);
            Services.ClipboardService.CopyToClipboard(bmp);
            ToastWindow.Show("Copied", $"{vm.Dimensions} screenshot copied");
        });

        if (!string.IsNullOrEmpty(vm.Entry.UploadProvider))
        {
            var badge = CreateProviderBadge(vm.Entry.UploadProvider);
            if (badge != null) shell.ImageContainer.Children.Add(badge);
        }

        shell.InfoPanel.Children.Add(new TextBlock
        {
            Text = vm.Entry.FileName,
            FontSize = 11,
            FontFamily = new System.Windows.Media.FontFamily(UiChrome.PreferredFamilyName),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        var timeAndStatus = string.IsNullOrWhiteSpace(vm.ImageSearchStatusText)
            ? vm.TimeAgo
            : $"{vm.TimeAgo} · {vm.ImageSearchStatusText}";
        shell.InfoPanel.Children.Add(new TextBlock
        {
            Text = timeAndStatus,
            FontSize = 10,
            FontFamily = new System.Windows.Media.FontFamily(UiChrome.PreferredFamilyName),
            Opacity = 0.3,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (_settingsService.Settings.ShowImageSearchDiagnostics)
        {
            if (!string.IsNullOrWhiteSpace(vm.ImageSearchMatchText))
            {
                shell.InfoPanel.Children.Add(new TextBlock
                {
                    Text = vm.ImageSearchMatchText,
                    FontSize = 9.5,
                    FontFamily = new System.Windows.Media.FontFamily(UiChrome.PreferredFamilyName),
                    Opacity = 0.38,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            if (!string.IsNullOrWhiteSpace(vm.ImageSearchDiagnosticsText))
                shell.Card.ToolTip = vm.ImageSearchDiagnosticsText;
        }
        return shell.Card;
    }

    private static void UpdateCardSelection(HistoryItemVM vm)
    {
        if (vm.Card is null)
            return;

        if (vm.SelectionBadge != null)
            vm.SelectionBadge.Visibility = vm.IsSelected ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ToggleSelectMode(object sender, RoutedEventArgs e)
    {
        _selectMode = !_selectMode;
        SelectBtn.Content = _selectMode ? "Done" : "Select";
        DeleteSelectedBtn.Visibility = _selectMode ? Visibility.Visible : Visibility.Collapsed;
        LoadCurrentHistoryTab();
        UpdateImageSearchActionButtons();
    }

    private void DeleteAllClick(object sender, RoutedEventArgs e)
    {
        try
        {
            CancelImageSearchWork();
            string tab = HistoryCategoryCombo.SelectedIndex == 0 ? "images"
                : HistoryCategoryCombo.SelectedIndex == 2 ? "videos/GIFs"
                : HistoryCategoryCombo.SelectedIndex == 1 ? "text history"
                : HistoryCategoryCombo.SelectedIndex == 3 ? "colors" : "stickers";
            if (MessageBox.Show($"Delete all {tab}?", "Confirm 1/3", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (MessageBox.Show($"Really delete all {tab}?", "Confirm 2/3", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            if (MessageBox.Show($"This cannot be undone. Delete all {tab}?", "Confirm 3/3", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            if (HistoryCategoryCombo.SelectedIndex == 0) _historyService.ClearImages();
            else if (HistoryCategoryCombo.SelectedIndex == 2) DeleteMediaItems(_allGifItems);
            else if (HistoryCategoryCombo.SelectedIndex == 1) _historyService.ClearOcr();
            else if (HistoryCategoryCombo.SelectedIndex == 3) _historyService.ClearColors();
            else _historyService.ClearStickers();

            _selectMode = false;
            SelectBtn.Content = "Select";
            DeleteSelectedBtn.Visibility = Visibility.Collapsed;

            LoadCurrentHistoryTab();
            UpdateImageSearchActionButtons();
        }
        catch (Exception ex)
        {
            ToastWindow.ShowError("Delete failed", ex.Message);
        }
    }

    private void DeleteSelectedClick(object sender, RoutedEventArgs e)
    {
        try
        {
            CancelImageSearchWork();
            _selectMode = false;
            SelectBtn.Content = "Select";
            DeleteSelectedBtn.Visibility = Visibility.Collapsed;

            if (HistoryCategoryCombo.SelectedIndex == 0)
            {
                var toDelete = _historyItems.Where(i => i.IsSelected).Select(i => i.Entry).ToList();
                _historyService.DeleteEntries(toDelete);
            }
            else if (HistoryCategoryCombo.SelectedIndex == 2)
            {
                DeleteMediaItems(_gifItems.Where(i => i.IsSelected).ToList());
            }
            else if (HistoryCategoryCombo.SelectedIndex == 1)
            {
                var toDelete = OcrStack.Children.OfType<Border>()
                    .Where(b => b.Tag is true)
                    .ToList();
                // Map selected cards to their OcrHistoryEntry by matching text content
                var allEntries = _historyService.OcrEntries;
                var entriesToDelete = new List<OcrHistoryEntry>();
                foreach (var card in toDelete)
                {
                    if (card.Child is Grid root && root.Children.OfType<StackPanel>().FirstOrDefault() is { } stack)
                    {
                        var textBox = stack.Children.OfType<System.Windows.Controls.TextBox>().FirstOrDefault();
                        if (textBox != null)
                        {
                            var match = allEntries.FirstOrDefault(e =>
                                e.Text == textBox.Text || e.Text.StartsWith(textBox.Text.TrimEnd('.', ' ')));
                            if (match != null) entriesToDelete.Add(match);
                        }
                    }
                }
                _historyService.DeleteOcrEntries(entriesToDelete);
            }
            else if (HistoryCategoryCombo.SelectedIndex == 3)
            {
                var toDelete = ColorStack.Children.OfType<Border>()
                    .Select(s => s.Tag).OfType<ColorHistoryEntry>().ToList();
                _historyService.DeleteColorEntries(toDelete);
            }
            else if (HistoryCategoryCombo.SelectedIndex == 4)
            {
                var toDelete = _stickerItems.Where(i => i.IsSelected).Select(i => i.Entry).ToList();
                _historyService.DeleteEntries(toDelete);
            }

            LoadCurrentHistoryTab();
            UpdateImageSearchActionButtons();
        }
        catch (Exception ex)
        {
            ToastWindow.ShowError("Delete failed", ex.Message);
        }
    }

    private void LoadStickerHistory()
    {
        StickerStack.Children.Clear();

        var entries = _historyService.StickerEntries;
        long totalBytes = 0;
        foreach (var e in entries)
            totalBytes += e.FileSizeBytes > 0 ? e.FileSizeBytes : TryGetFileLength(e.FilePath);
        var sizeStr = FormatStorageSize(totalBytes);
        HistoryCountText.Text = $"{entries.Count} sticker{(entries.Count == 1 ? "" : "s")} · {sizeStr}";
        HistoryEmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryEmptyLabel.Text = "No stickers yet";

        _allStickerItems = entries.Select(e => new HistoryItemVM
        {
            Entry = e,
            ThumbPath = e.FilePath,
            Dimensions = e.Width > 0 ? $"{e.Width} x {e.Height}" : "",
            TimeAgo = FormatTimeAgo(e.CapturedAt)
        }).ToList();

        _stickerRenderCount = Math.Min(HistoryPageSize, _allStickerItems.Count);
        RenderStickerItems();
        DeleteSelectedBtn.Visibility = _selectMode ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RenderStickerItems()
    {
        StickerStack.Children.Clear();
        _stickerItems = _allStickerItems.Take(_stickerRenderCount).ToList();
        var groups = _stickerItems.GroupBy(i => i.Entry.CapturedAt.Date).OrderByDescending(g => g.Key);
        foreach (var group in groups)
        {
            string label = group.Key == DateTime.Today ? "Today"
                : group.Key == DateTime.Today.AddDays(-1) ? "Yesterday"
                : group.Key.ToString("MMMM d, yyyy");

            if (StickerStack.Children.Count > 0)
            {
                StickerStack.Children.Add(new Border
                {
                    Height = 1,
                    Background = Theme.Brush(Theme.BorderSubtle),
                    Margin = new Thickness(6, 14, 6, 0)
                });
            }

            StickerStack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Opacity = 0.45,
                Margin = new Thickness(6, 10, 0, 4)
            });

            var wrap = new WrapPanel();
            foreach (var item in group)
                wrap.Children.Add(CreateHistoryCard(item));
            StickerStack.Children.Add(wrap);
        }
    }

    private void StickerPanel_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalOffset + e.ViewportHeight < e.ExtentHeight - 300) return;
        if (_stickerRenderCount >= _allStickerItems.Count) return;
        _stickerRenderCount = Math.Min(_stickerRenderCount + HistoryPageSize, _allStickerItems.Count);
        RenderStickerItems();
    }

    private static long TryGetFileLength(string filePath)
    {
        try { return new FileInfo(filePath).Length; }
        catch { return 0; }
    }
}
