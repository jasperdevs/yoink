using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using Yoink.Models;

namespace Yoink.Services;

public enum HistoryKind
{
    Image,
    Gif,
    Sticker
}

public sealed class HistoryEntry
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public DateTime CapturedAt { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long FileSizeBytes { get; set; }
    public HistoryKind Kind { get; set; } = HistoryKind.Image;
    public string? UploadUrl { get; set; }
    public string? UploadProvider { get; set; }
}

public sealed class OcrHistoryEntry
{
    public string Text { get; set; } = "";
    public DateTime CapturedAt { get; set; }
}

public sealed class ColorHistoryEntry
{
    public string Hex { get; set; } = "";
    public DateTime CapturedAt { get; set; }
}

public sealed partial class HistoryService
{
    public static readonly string HistoryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Yoink History");

    public static readonly string StickerDir = Path.Combine(HistoryDir, "stickers");

    private static readonly string LegacyHistoryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Yoink", "history");

    private static readonly string LegacyStickerDir = Path.Combine(LegacyHistoryDir, "stickers");

    private static readonly string IndexPath = Path.Combine(HistoryDir, "index.json");
    private static readonly string OcrIndexPath = Path.Combine(HistoryDir, "ocr_index.json");
    private static readonly string ColorIndexPath = Path.Combine(HistoryDir, "color_index.json");

    private static readonly string LegacyIndexPath = Path.Combine(LegacyHistoryDir, "index.json");
    private static readonly string LegacyOcrIndexPath = Path.Combine(LegacyHistoryDir, "ocr_index.json");
    private static readonly string LegacyColorIndexPath = Path.Combine(LegacyHistoryDir, "color_index.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private List<HistoryEntry> _entries = new();
    private List<OcrHistoryEntry> _ocrEntries = new();
    private List<ColorHistoryEntry> _colorEntries = new();
    private IReadOnlyList<HistoryEntry>? _imageEntries;
    private IReadOnlyList<HistoryEntry>? _gifEntries;
    private IReadOnlyList<HistoryEntry>? _stickerEntries;
    private readonly object _gate = new();
    private readonly System.Threading.Timer _flushTimer;
    private bool _indexDirty;
    private bool _ocrDirty;
    private bool _colorDirty;

    public event Action? Changed;

    public HistoryService()
    {
        _flushTimer = new System.Threading.Timer(_ =>
        {
            try { FlushPendingWrites(); } catch { }
        }, null, Timeout.Infinite, Timeout.Infinite);
    }

    public IReadOnlyList<HistoryEntry> Entries { get { lock (_gate) return _entries.ToList(); } }
    public IReadOnlyList<HistoryEntry> ImageEntries { get { lock (_gate) return _imageEntries ??= _entries.Where(e => e.Kind == HistoryKind.Image).ToList(); } }
    public IReadOnlyList<HistoryEntry> GifEntries { get { lock (_gate) return _gifEntries ??= _entries.Where(e => e.Kind == HistoryKind.Gif).ToList(); } }
    public IReadOnlyList<HistoryEntry> StickerEntries { get { lock (_gate) return _stickerEntries ??= _entries.Where(e => e.Kind == HistoryKind.Sticker).ToList(); } }
    public IReadOnlyList<OcrHistoryEntry> OcrEntries { get { lock (_gate) return _ocrEntries.ToList(); } }
    public IReadOnlyList<ColorHistoryEntry> ColorEntries { get { lock (_gate) return _colorEntries.ToList(); } }

    private void InvalidateFilteredCache() { _imageEntries = null; _gifEntries = null; _stickerEntries = null; }

    private void NotifyChanged() => Changed?.Invoke();

    public string GetDiskFingerprint(string saveDirectory)
    {
        lock (_gate)
        {
            var hash = new HashCode();

            AddDirectorySignature(hash, HistoryDir);
            AddDirectorySignature(hash, StickerDir);
            AddDirectorySignature(hash, saveDirectory);
            AddDirectorySignature(hash, Path.Combine(saveDirectory, "Videos"));

            AddFileSignature(hash, IndexPath);
            AddFileSignature(hash, OcrIndexPath);
            AddFileSignature(hash, ColorIndexPath);

            hash.Add(_entries.Count);
            hash.Add(_ocrEntries.Count);
            hash.Add(_colorEntries.Count);

            return hash.ToHashCode().ToString("X8");
        }
    }

    public void Load()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(HistoryDir);
            Directory.CreateDirectory(StickerDir);

            if (File.Exists(IndexPath))
            {
                bool indexChanged = false;
                try
                {
                    _entries = JsonSerializer.Deserialize<List<HistoryEntry>>(
                        File.ReadAllText(IndexPath), JsonOpts) ?? new();
                    indexChanged |= _entries.RemoveAll(e => !File.Exists(e.FilePath)) > 0;
                    foreach (var entry in _entries)
                    {
                        var desiredKind = entry.FilePath.StartsWith(StickerDir, StringComparison.OrdinalIgnoreCase)
                            ? HistoryKind.Sticker
                            : Path.GetExtension(entry.FilePath).Equals(".gif", StringComparison.OrdinalIgnoreCase)
                                ? HistoryKind.Gif
                                : HistoryKind.Image;

                        if (entry.Kind != desiredKind)
                        {
                            entry.Kind = desiredKind;
                            indexChanged = true;
                        }
                    }
                }
                catch { _entries = new(); }
                InvalidateFilteredCache();
                if (indexChanged)
                    SaveIndex();
            }

            if (File.Exists(OcrIndexPath))
            {
                try
                {
                    _ocrEntries = JsonSerializer.Deserialize<List<OcrHistoryEntry>>(
                        File.ReadAllText(OcrIndexPath), JsonOpts) ?? new();
                }
                catch { _ocrEntries = new(); }
            }

            if (File.Exists(ColorIndexPath))
            {
                try
                {
                    _colorEntries = JsonSerializer.Deserialize<List<ColorHistoryEntry>>(
                        File.ReadAllText(ColorIndexPath), JsonOpts) ?? new();
                }
                catch { _colorEntries = new(); }
            }

            MigrateLegacyStorage();
            PruneByRetention(HistoryRetentionPeriod.Never);
        }
    }

    public bool CompressHistory { get; set; }
    public int JpegQuality { get; set; } = 85;
    public CaptureImageFormat CaptureImageFormat { get; set; } = CaptureImageFormat.Png;
    public HistoryRetentionPeriod RetentionPeriod { get; set; } = HistoryRetentionPeriod.Never;

    public HistoryEntry SaveGifEntry(string gifPath)
    {
        HistoryEntry entry;
        lock (_gate)
        {
            var fi = new FileInfo(gifPath);
            entry = new HistoryEntry
            {
                FileName = fi.Name,
                FilePath = gifPath,
                CapturedAt = DateTime.Now,
                Width = 0,
                Height = 0,
                FileSizeBytes = fi.Length,
                Kind = HistoryKind.Gif
            };
            _entries.Insert(0, entry);
            InvalidateFilteredCache();
            SaveIndex();
        }
        NotifyChanged();
        return entry;
    }

    public HistoryEntry SaveStickerEntry(Bitmap sticker, string? providerName = null)
    {
        HistoryEntry entry;
        lock (_gate)
        {
            Directory.CreateDirectory(StickerDir);
            var now = DateTime.Now;
            var fileName = $"yoink_sticker_{now:yyyyMMdd_HHmmss_fff}.png";
            var filePath = Path.Combine(StickerDir, fileName);

            sticker.Save(filePath, ImageFormat.Png);

            entry = new HistoryEntry
            {
                FileName = fileName,
                FilePath = filePath,
                CapturedAt = now,
                Width = sticker.Width,
                Height = sticker.Height,
                FileSizeBytes = new FileInfo(filePath).Length,
                Kind = HistoryKind.Sticker,
                UploadProvider = providerName
            };
            _entries.Insert(0, entry);
            InvalidateFilteredCache();
            SaveIndex();
        }
        NotifyChanged();
        return entry;
    }

    public HistoryEntry SaveCapture(Bitmap screenshot)
    {
        HistoryEntry entry;
        lock (_gate)
        {
            Directory.CreateDirectory(HistoryDir);
            var now = DateTime.Now;
            string ext = CaptureOutputService.GetExtension(CaptureImageFormat);
            var fileName = $"yoink_{now:yyyyMMdd_HHmmss_fff}.{ext}";
            var filePath = Path.Combine(HistoryDir, fileName);

            CaptureOutputService.SaveBitmap(screenshot, filePath, CaptureImageFormat, JpegQuality);

            entry = new HistoryEntry
            {
                FileName = fileName, FilePath = filePath, CapturedAt = now,
                Width = screenshot.Width, Height = screenshot.Height,
                FileSizeBytes = new FileInfo(filePath).Length,
                Kind = HistoryKind.Image
            };
            _entries.Insert(0, entry);
            InvalidateFilteredCache();
            SaveIndex();
        }
        NotifyChanged();
        return entry;
    }

    public void SaveOcrEntry(string text)
    {
        lock (_gate)
        {
            _ocrEntries.Insert(0, new OcrHistoryEntry { Text = text, CapturedAt = DateTime.Now });
            while (_ocrEntries.Count > 200)
                _ocrEntries.RemoveAt(_ocrEntries.Count - 1);
            SaveOcrIndex();
        }
        NotifyChanged();
    }

    public void DeleteEntry(HistoryEntry entry)
    {
        lock (_gate)
        {
            _entries.Remove(entry);
            InvalidateFilteredCache();
            try { File.Delete(entry.FilePath); } catch { }
            SaveIndex();
        }
        NotifyChanged();
    }

    public void DeleteEntries(IEnumerable<HistoryEntry> entries)
    {
        var list = entries.Distinct().ToList();
        if (list.Count == 0)
            return;

        lock (_gate)
        {
            foreach (var entry in list)
            {
                _entries.Remove(entry);
                try { File.Delete(entry.FilePath); } catch { }
            }
            InvalidateFilteredCache();
            SaveIndex();
        }
        NotifyChanged();
    }

    public void DeleteOcrEntry(OcrHistoryEntry entry)
    {
        lock (_gate)
        {
            _ocrEntries.Remove(entry);
            SaveOcrIndex();
        }
        NotifyChanged();
    }

    public void DeleteOcrEntries(IEnumerable<OcrHistoryEntry> entries)
    {
        var list = entries.Distinct().ToList();
        if (list.Count == 0)
            return;

        lock (_gate)
        {
            foreach (var entry in list)
                _ocrEntries.Remove(entry);
            SaveOcrIndex();
        }
        NotifyChanged();
    }

    public void SaveColorEntry(string hex)
    {
        lock (_gate)
        {
            _colorEntries.Insert(0, new ColorHistoryEntry { Hex = hex, CapturedAt = DateTime.Now });
            while (_colorEntries.Count > 200)
                _colorEntries.RemoveAt(_colorEntries.Count - 1);
            SaveColorIndex();
        }
        NotifyChanged();
    }

    public void DeleteColorEntry(ColorHistoryEntry entry)
    {
        lock (_gate)
        {
            _colorEntries.Remove(entry);
            SaveColorIndex();
        }
        NotifyChanged();
    }

    public void DeleteColorEntries(IEnumerable<ColorHistoryEntry> entries)
    {
        var list = entries.Distinct().ToList();
        if (list.Count == 0)
            return;

        lock (_gate)
        {
            foreach (var entry in list)
                _colorEntries.Remove(entry);
            SaveColorIndex();
        }
        NotifyChanged();
    }

    public void ClearImages()
    {
        lock (_gate)
        {
            var images = _entries.Where(e => e.Kind == HistoryKind.Image).ToList();
            foreach (var e in images)
            {
                try { File.Delete(e.FilePath); } catch { }
                _entries.Remove(e);
            }
            InvalidateFilteredCache();
            SaveIndex();
        }
        NotifyChanged();
    }

    public void ClearGifs()
    {
        lock (_gate)
        {
            var gifs = _entries.Where(e => e.Kind == HistoryKind.Gif).ToList();
            foreach (var e in gifs)
            {
                try { File.Delete(e.FilePath); } catch { }
                _entries.Remove(e);
            }
            InvalidateFilteredCache();
            SaveIndex();
        }
        NotifyChanged();
    }

    public void ClearOcr()
    {
        lock (_gate)
        {
            _ocrEntries.Clear();
            SaveOcrIndex();
        }
        NotifyChanged();
    }

    public void ClearColors()
    {
        lock (_gate)
        {
            _colorEntries.Clear();
            SaveColorIndex();
        }
        NotifyChanged();
    }

    public void ClearAll()
    {
        lock (_gate)
        {
            foreach (var e in _entries)
                try { File.Delete(e.FilePath); } catch { }
            _entries.Clear();
            InvalidateFilteredCache();
            SaveIndex();
        }
        NotifyChanged();
    }

    public void ClearStickers()
    {
        lock (_gate)
        {
            var stickers = _entries.Where(e => e.Kind == HistoryKind.Sticker).ToList();
            foreach (var e in stickers)
            {
                try { File.Delete(e.FilePath); } catch { }
                _entries.Remove(e);
            }
            InvalidateFilteredCache();
            SaveIndex();
        }
        NotifyChanged();
    }

}
