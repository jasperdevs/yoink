using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;

namespace Yoink.Services;

public sealed class HistoryEntry
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public DateTime CapturedAt { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class HistoryService
{
    private static readonly string HistoryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Yoink", "history");

    private static readonly string IndexPath = Path.Combine(HistoryDir, "index.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private List<HistoryEntry> _entries = new();

    public IReadOnlyList<HistoryEntry> Entries => _entries;

    public void Load()
    {
        Directory.CreateDirectory(HistoryDir);

        if (!File.Exists(IndexPath)) return;

        try
        {
            var json = File.ReadAllText(IndexPath);
            _entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json, JsonOpts) ?? new();
            // Prune entries whose files no longer exist
            _entries.RemoveAll(e => !File.Exists(e.FilePath));
        }
        catch
        {
            _entries = new();
        }
    }

    public HistoryEntry SaveCapture(Bitmap screenshot)
    {
        Directory.CreateDirectory(HistoryDir);

        var now = DateTime.Now;
        var fileName = $"yoink_{now:yyyyMMdd_HHmmss_fff}.png";
        var filePath = Path.Combine(HistoryDir, fileName);

        screenshot.Save(filePath, ImageFormat.Png);

        var entry = new HistoryEntry
        {
            FileName = fileName,
            FilePath = filePath,
            CapturedAt = now,
            Width = screenshot.Width,
            Height = screenshot.Height
        };

        _entries.Insert(0, entry);

        // Keep max 100 entries
        while (_entries.Count > 100)
        {
            var old = _entries[^1];
            _entries.RemoveAt(_entries.Count - 1);
            try { File.Delete(old.FilePath); } catch { }
        }

        SaveIndex();
        return entry;
    }

    public void DeleteEntry(HistoryEntry entry)
    {
        _entries.Remove(entry);
        try { File.Delete(entry.FilePath); } catch { }
        SaveIndex();
    }

    public void ClearAll()
    {
        foreach (var e in _entries)
            try { File.Delete(e.FilePath); } catch { }
        _entries.Clear();
        SaveIndex();
    }

    private void SaveIndex()
    {
        var json = JsonSerializer.Serialize(_entries, JsonOpts);
        File.WriteAllText(IndexPath, json);
    }
}
