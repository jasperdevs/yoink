using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yoink.Helpers;
using Yoink.Models;
using Yoink.Services;

namespace Yoink.UI;

public partial class SettingsWindow
{
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
        var totalCount = _allHistoryItems.Count;
        if (usingSearch)
        {
            HistoryCountText.Text = $"{_filteredHistoryItems.Count} of {totalCount} capture{(totalCount == 1 ? "" : "s")} · {sizeStr}";
        }
        else
        {
            var indexedCount = _imageSearchIndexService.CountReadyEntries(_historyService.ImageEntries, _settingsService.Settings.OcrLanguageTag);
            var indexSuffix = totalCount > 0 ? $" · {indexedCount}/{totalCount} indexed" : "";
            HistoryCountText.Text = $"{_filteredHistoryItems.Count} capture{(_filteredHistoryItems.Count == 1 ? "" : "s")} · {sizeStr}{indexSuffix}";
        }

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
            var totalCount = _allHistoryItems.Count;
            HistoryCountText.Text = $"{_filteredHistoryItems.Count} of {totalCount} capture{(totalCount == 1 ? "" : "s")} · {sizeStr}";

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

}
