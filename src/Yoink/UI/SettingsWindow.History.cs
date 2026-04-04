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

        RefreshImageSearchTexts();
        if (_settingsService.Settings.AutoIndexImages)
            _imageSearchIndexService.RequestSync(entries, _settingsService.Settings.OcrLanguageTag);
        ApplyImageSearchFilter();
        DeleteSelectedBtn.Visibility = _selectMode ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshImageSearchTexts()
    {
        foreach (var item in _allHistoryItems)
        {
            var searchText = _imageSearchIndexService.BuildSearchText(item.Entry.FilePath, item.Entry.FileName);
            item.SearchText = searchText;
            item.NormalizedSearchText = ImageSearchQueryMatcher.Normalize(searchText);
            var diagnostics = _imageSearchIndexService.GetDiagnostics(
                item.Entry.FilePath,
                item.Entry.FileName,
                _imageSearchQuery,
                _settingsService.Settings.ImageSearchSources,
                _settingsService.Settings.ImageSearchExactMatch);
            item.ImageSearchStatusText = diagnostics.StatusText;
            item.ImageSearchDiagnosticsText = diagnostics.DetailsText;
            item.ImageSearchMatchText = diagnostics.MatchText;
        }

        UpdateImageSearchPlaceholderText();
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

    private void ApplyImageSearchFilter()
    {
        if (HistoryCategoryCombo.SelectedIndex != 0)
            return;

        if (!_settingsService.Settings.ShowImageSearchBar)
        {
            CancelImageSearchWork();
            ApplyImmediateImageFilter("", _settingsService.Settings.ImageSearchSources, _settingsService.Settings.ImageSearchExactMatch);
            return;
        }

        var query = _imageSearchQuery.Trim();
        var sources = _settingsService.Settings.ImageSearchSources;
        CancelImageSearchWork();
        if (string.IsNullOrWhiteSpace(query) || sources == ImageSearchSourceOptions.None)
        {
            ApplyImmediateImageFilter(query, sources, _settingsService.Settings.ImageSearchExactMatch);
            SetImageSearchLoading(false, forceSemantic: true);
            return;
        }

        SetImageSearchLoading(true, forceSemantic: true);
        _searchFilterCts = new CancellationTokenSource();
        _ = ApplySemanticImageSearchAsync(++_searchFilterVersion, query, sources, _searchFilterCts.Token);
    }

    private void ApplyImmediateImageFilter(string query, ImageSearchSourceOptions sources, bool exactMatch)
    {
        var rankedItems = RankLocalImageItems(query, sources, exactMatch);
        _filteredHistoryItems = rankedItems;
        _historyRenderCount = Math.Min(HistoryPageSize, _filteredHistoryItems.Count);

        long visibleBytes = 0;
        foreach (var item in _filteredHistoryItems)
            visibleBytes += item.Entry.FileSizeBytes;

        var searchEnabled = sources != ImageSearchSourceOptions.None;
        var usingSearch = searchEnabled && !string.IsNullOrWhiteSpace(query);
        var sizeStr = FormatStorageSize(visibleBytes);
        HistoryCountText.Text = $"{_filteredHistoryItems.Count} capture{(_filteredHistoryItems.Count == 1 ? "" : "s")} · {sizeStr}";

        HistoryEmptyText.Visibility = _filteredHistoryItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryEmptyLabel.Text = !searchEnabled && !string.IsNullOrWhiteSpace(query)
            ? "Enable at least one search source"
            : usingSearch
                ? "No screenshots match your search"
                : "No captures yet";

        RenderHistoryItems();
        UpdateImageSearchStatus();
        UpdateImageSearchActionButtons();
    }

    private List<HistoryItemVM> RankLocalImageItems(string query, ImageSearchSourceOptions sources, bool exactMatch)
    {
        var normalizedQuery = ImageSearchQueryMatcher.Normalize(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return _allHistoryItems.OrderByDescending(item => item.Entry.CapturedAt).ToList();

        var allowFileName = sources.HasFlag(ImageSearchSourceOptions.FileName);
        var allowOcr = sources.HasFlag(ImageSearchSourceOptions.Ocr);
        if (!allowFileName && !allowOcr)
            return _allHistoryItems.OrderByDescending(item => item.Entry.CapturedAt).ToList();

        return _allHistoryItems
            .Select(item => new
            {
                Item = item,
                Score = ScoreLocalImageItem(normalizedQuery, item, allowFileName, allowOcr, exactMatch)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Item.Entry.CapturedAt)
            .Select(x => x.Item)
            .ToList();
    }

    private static int ScoreLocalImageItem(string normalizedQuery, HistoryItemVM item, bool allowFileName, bool allowOcr, bool exactMatch)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return 1;

        var searchableText = allowOcr ? item.NormalizedSearchText : "";
        var fileName = allowFileName ? item.NormalizedFileNameSearchText : "";
        return ImageSearchQueryMatcher.ScorePreNormalized(normalizedQuery, searchableText, fileName, exactMatch);
    }

    private async Task ApplySemanticImageSearchAsync(int version, string query, ImageSearchSourceOptions sources, CancellationToken cancellationToken)
    {
        bool searchFailed = false;
        try
        {
            var exactMatch = _settingsService.Settings.ImageSearchExactMatch;
            if (string.IsNullOrWhiteSpace(query) || sources == ImageSearchSourceOptions.None)
                return;

            var entries = _historyService.ImageEntries;
            var rankedEntries = await _imageSearchIndexService.SearchAsync(
                entries,
                query,
                sources,
                exactMatch,
                cancellationToken);

            if (!IsLoaded || version != _searchFilterVersion || cancellationToken.IsCancellationRequested)
                return;

            var filtered = new List<HistoryItemVM>(rankedEntries.Count);
            long visibleBytes = 0;
            foreach (var entry in rankedEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_allHistoryItemsByPath.TryGetValue(entry.FilePath, out var vm))
                    continue;

                filtered.Add(vm);
                visibleBytes += entry.FileSizeBytes > 0 ? entry.FileSizeBytes : vm.Entry.FileSizeBytes;
            }

            if (!IsLoaded || version != _searchFilterVersion || cancellationToken.IsCancellationRequested)
                return;

            _filteredHistoryItems = filtered;
            _historyRenderCount = Math.Min(HistoryPageSize, _filteredHistoryItems.Count);

            var sizeStr = FormatStorageSize(visibleBytes);
            HistoryCountText.Text = $"{_filteredHistoryItems.Count} capture{(_filteredHistoryItems.Count == 1 ? "" : "s")} · {sizeStr}";

            HistoryEmptyText.Visibility = _filteredHistoryItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            HistoryEmptyLabel.Text = _filteredHistoryItems.Count == 0
                ? "No screenshots match your search"
                : "";

            RenderHistoryItems();
            UpdateImageSearchStatus();
            SetImageSearchLoading(false, forceSemantic: true);
            UpdateImageSearchActionButtons();
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            searchFailed = true;
        }
        finally
        {
            if (version == _searchFilterVersion)
                SetImageSearchLoading(false, forceSemantic: true);

            if (version == _searchFilterVersion && searchFailed)
                HistorySearchStatusText.Text = "Search failed";
        }
    }

    private bool ApplySemanticSearchIfNeeded()
    {
        return false;
    }

    private void UpdateImageSearchStatus()
    {
        if (HistoryCategoryCombo.SelectedIndex != 0)
        {
            HistorySearchStatusText.Text = "";
            return;
        }

        if (!_settingsService.Settings.ShowImageSearchBar)
        {
            HistorySearchStatusText.Text = "";
            return;
        }

        var status = _imageSearchIndexService.StatusText;
        if (status.StartsWith("Indexing screenshots", StringComparison.OrdinalIgnoreCase))
        {
            HistorySearchStatusText.Text = status;
            return;
        }

        if (_settingsService.Settings.ImageSearchExactMatch)
        {
            HistorySearchStatusText.Text = "";
            return;
        }

        var sources = _settingsService.Settings.ImageSearchSources;
        if (sources == ImageSearchSourceOptions.None)
        {
            HistorySearchStatusText.Text = "Search off";
            return;
        }

        if (string.IsNullOrWhiteSpace(_imageSearchQuery))
        {
            HistorySearchStatusText.Text = "";
            return;
        }

        HistorySearchStatusText.Text = "";
    }

    private void SetImageSearchLoading(bool isLoading, bool forceSemantic = false)
    {
        ImageSearchLoadingBar.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        if (HistoryCategoryCombo.SelectedIndex == 0)
            UpdateImageSearchStatus();
    }

    private void CancelImageSearchWork()
    {
        _imageSearchDebounceTimer.Stop();
        _semanticSearchTimer.Stop();
        _searchFilterCts?.Cancel();
        _searchFilterCts?.Dispose();
        _searchFilterCts = null;
        SetImageSearchLoading(false, forceSemantic: true);
    }

    private void UpdateImageSearchUi()
    {
        var isImages = HistoryCategoryCombo.SelectedIndex == 0;
        var showSearch = isImages && _settingsService.Settings.ShowImageSearchBar && !_imageSearchRowAutoHidden;
        ImageSearchRow.Visibility = showSearch ? Visibility.Visible : Visibility.Collapsed;
        if (showSearch)
        {
            LoadImageSearchSources();
            ImageSearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(ImageSearchBox.Text) && !ImageSearchBox.IsKeyboardFocused
                ? Visibility.Visible
                : Visibility.Collapsed;
            ImageSearchSemanticCheck.IsEnabled = !_settingsService.Settings.ImageSearchExactMatch;
        }
        else
        {
            if (isImages)
                CancelImageSearchWork();
            HistorySearchStatusText.Text = "";
            ImageSearchPlaceholder.Visibility = Visibility.Collapsed;
        }

        UpdateImageSearchActionButtons();
        UpdateImageSearchPlaceholderText();
    }

    private void SetImageSearchRowAutoHidden(bool hidden)
    {
        if (_imageSearchRowAutoHidden == hidden)
            return;

        _imageSearchRowAutoHidden = hidden;
        UpdateImageSearchUi();
    }

    private void UpdateImageSearchActionButtons()
    {
        if (!IsLoaded)
            return;

        var isImages = HistoryCategoryCombo.SelectedIndex == 0;
        var pendingCount = _imageSearchIndexService.CountPendingEntries(_historyService.ImageEntries, _settingsService.Settings.OcrLanguageTag);
        var status = _imageSearchIndexService.StatusText;
        var isIndexing = status.StartsWith("Indexing screenshots", StringComparison.OrdinalIgnoreCase);

        if (isImages && _settingsService.Settings.AutoIndexImages && pendingCount > 0 && !isIndexing)
        {
            _imageSearchIndexService.RequestSync(_historyService.ImageEntries, _settingsService.Settings.OcrLanguageTag);
            status = _imageSearchIndexService.StatusText;
            isIndexing = status.StartsWith("Indexing screenshots", StringComparison.OrdinalIgnoreCase);
        }

        ReindexAllProgressPanel.Visibility = isImages ? Visibility.Visible : Visibility.Collapsed;
        ReindexAllProgressBar.Visibility = isIndexing ? Visibility.Visible : Visibility.Collapsed;
        ReindexAllProgressText.Text = isIndexing
            ? status
            : pendingCount <= 0
                ? "None to index"
                : _settingsService.Settings.AutoIndexImages
                    ? $"{pendingCount} left to index"
                    : $"{pendingCount} waiting";
    }

    private void UpdateImageSearchPlaceholderText()
    {
        if (!IsLoaded)
            return;

        var totalCount = _allHistoryItems.Count;
        var indexedCount = _imageSearchIndexService.CountReadyEntries(_historyService.ImageEntries, _settingsService.Settings.OcrLanguageTag);
        var isIndexing = _imageSearchIndexService.StatusText.StartsWith("Indexing screenshots", StringComparison.OrdinalIgnoreCase);
        ImageSearchPlaceholder.Text = isIndexing
            ? $"Search {indexedCount}/{totalCount} files (indexing...)"
            : $"Search {indexedCount}/{totalCount} files";
    }

    private void UpdateImageSearchSourceSummary()
    {
        var parts = new List<string>(4);
        if (ImageSearchFileNameCheck.IsChecked)
            parts.Add("Name");
        if (ImageSearchOcrCheck.IsChecked)
            parts.Add("OCR");
        if (ImageSearchSemanticCheck.IsChecked)
            parts.Add("Semantic");
        if (ImageSearchExactMatchCheck.IsChecked)
            parts.Add("Exact");

        ImageSearchFiltersSummaryText.Text = parts.Count == 0 ? "None" : string.Join(", ", parts);
    }

    private void LoadImageSearchSources()
    {
        var sources = _settingsService.Settings.ImageSearchSources;
        _suppressImageSearchSourceEvents = true;
        try
        {
            ImageSearchFileNameCheck.IsChecked = (sources & ImageSearchSourceOptions.FileName) != 0;
            ImageSearchOcrCheck.IsChecked = (sources & ImageSearchSourceOptions.Ocr) != 0;
            ImageSearchSemanticCheck.IsChecked = (sources & ImageSearchSourceOptions.Semantic) != 0;
            ImageSearchSemanticCheck.IsEnabled = !_settingsService.Settings.ImageSearchExactMatch;
            ImageSearchExactMatchCheck.IsChecked = _settingsService.Settings.ImageSearchExactMatch;
        }
        finally
        {
            _suppressImageSearchSourceEvents = false;
        }

        UpdateImageSearchSourceSummary();
    }

    private ImageSearchSourceOptions GetImageSearchSourcesFromUi()
    {
        var sources = ImageSearchSourceOptions.None;
        if (ImageSearchFileNameCheck.IsChecked)
            sources |= ImageSearchSourceOptions.FileName;
        if (ImageSearchOcrCheck.IsChecked)
            sources |= ImageSearchSourceOptions.Ocr;
        if (ImageSearchSemanticCheck.IsChecked)
            sources |= ImageSearchSourceOptions.Semantic;
        return sources;
    }

    private void ImageSearchExactMatchCheck_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!IsLoaded)
                return;

            _settingsService.Settings.ImageSearchExactMatch = ImageSearchExactMatchCheck.IsChecked == true;
            _settingsService.Save();

            ImageSearchSemanticCheck.IsEnabled = !_settingsService.Settings.ImageSearchExactMatch;
            UpdateImageSearchSourceSummary();
            CancelImageSearchWork();

            if (HistoryCategoryCombo.SelectedIndex == 0)
                ApplyImageSearchFilter();
        }
        catch (Exception ex)
        {
            HistorySearchStatusText.Text = "Search failed";
            ToastWindow.ShowError("Search failed", ex.Message);
        }
    }

    private void ImageSearchSourcesCheck_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!IsLoaded || _suppressImageSearchSourceEvents)
                return;

            _settingsService.Settings.ImageSearchSources = GetImageSearchSourcesFromUi();
            _settingsService.Save();
            UpdateImageSearchSourceSummary();

            if (HistoryCategoryCombo.SelectedIndex == 0)
                ApplyImageSearchFilter();
        }
        catch (Exception ex)
        {
            HistorySearchStatusText.Text = "Search failed";
            ToastWindow.ShowError("Search failed", ex.Message);
        }
    }

    private void ImageSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            if (!IsLoaded)
                return;

            SetImageSearchRowAutoHidden(false);
            _imageSearchQuery = ImageSearchBox.Text ?? "";
            ImageSearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(_imageSearchQuery) && !ImageSearchBox.IsKeyboardFocused
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (HistoryCategoryCombo.SelectedIndex == 0)
            {
                if (string.IsNullOrWhiteSpace(_imageSearchQuery))
                {
                    CancelImageSearchWork();
                    ApplyImageSearchFilter();
                    return;
                }

                SetImageSearchLoading(true);
                QueueImageSearchRefresh();
            }
        }
        catch (Exception ex)
        {
            HistorySearchStatusText.Text = "Search failed";
            SetImageSearchLoading(false, forceSemantic: true);
            ToastWindow.ShowError("Search failed", ex.Message);
        }
    }

    private void ImageSearchBox_FocusChanged(object sender, RoutedEventArgs e)
    {
        if (ImageSearchBox.IsKeyboardFocused)
            SetImageSearchRowAutoHidden(false);

        ImageSearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(ImageSearchBox.Text) && !ImageSearchBox.IsKeyboardFocused
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ImageSearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape || string.IsNullOrWhiteSpace(ImageSearchBox.Text))
            return;

        try
        {
            CancelImageSearchWork();
            ImageSearchBox.Clear();
            ImageSearchBox.Focus();
            ApplyImageSearchFilter();
            e.Handled = true;
        }
        catch (Exception ex)
        {
            HistorySearchStatusText.Text = "Search failed";
            SetImageSearchLoading(false, forceSemantic: true);
            ToastWindow.ShowError("Search failed", ex.Message);
        }
    }

    private void ImageSearchFiltersBtn_Click(object sender, RoutedEventArgs e)
    {
        ImageSearchFiltersMenu.PlacementTarget = ImageSearchFiltersBtn;
        ImageSearchFiltersMenu.IsOpen = true;
    }

    private void HistoryPanel_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (HistoryCategoryCombo.SelectedIndex == 0 && _settingsService.Settings.ShowImageSearchBar)
        {
            var shouldHideSearch = e.VerticalOffset > 18 && !ImageSearchBox.IsKeyboardFocused;
            SetImageSearchRowAutoHidden(shouldHideSearch);
        }

        if (e.VerticalOffset + e.ViewportHeight < e.ExtentHeight - 300) return;
        if (_historyRenderCount >= _filteredHistoryItems.Count) return;
        _historyRenderCount = Math.Min(_historyRenderCount + HistoryPageSize, _filteredHistoryItems.Count);
        RenderHistoryItems();
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
                var toDelete = OcrStack.Children.OfType<Border>().Where(b => b.Tag as bool? == true).ToList();
                var toDeleteEntries = toDelete
                    .Select(card =>
                    {
                        int idx = OcrStack.Children.IndexOf(card);
                        return idx >= 0 && idx < _historyService.OcrEntries.Count ? _historyService.OcrEntries[idx] : null;
                    })
                    .OfType<OcrHistoryEntry>()
                    .ToList();
                _historyService.DeleteOcrEntries(toDeleteEntries);
            }
            else if (HistoryCategoryCombo.SelectedIndex == 3)
            {
                var toDelete = ColorStack.Children.OfType<StackPanel>().Select(s => s.Tag).OfType<ColorHistoryEntry>().ToList();
                _historyService.DeleteColorEntries(toDelete);
            }
            else if (HistoryCategoryCombo.SelectedIndex == 4)
            {
                var toDelete = _stickerItems.Where(i => i.IsSelected).Select(i => i.Entry).ToList();
                _historyService.DeleteEntries(toDelete);
            }

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
