using System.IO;
using System.Text.Json;
using Yoink.Models;

namespace Yoink.Services;

public sealed partial class HistoryService
{
    private void MigrateLegacyStorage()
    {
        bool changed = false;
        var trackedFileNames = new HashSet<string>(_entries.Select(e => e.FileName), StringComparer.OrdinalIgnoreCase);

        if (File.Exists(LegacyIndexPath))
        {
            try
            {
                var legacyEntries = JsonSerializer.Deserialize<List<HistoryEntry>>(
                    File.ReadAllText(LegacyIndexPath), JsonOpts) ?? new();

                foreach (var legacyEntry in legacyEntries.OrderBy(e => e.CapturedAt))
                {
                    if (trackedFileNames.Contains(legacyEntry.FileName))
                        continue;

                    if (TryMigrateLegacyFile(legacyEntry.FilePath, legacyEntry.Kind, out var migrated))
                    {
                        _entries.Add(migrated);
                        trackedFileNames.Add(migrated.FileName);
                        changed = true;
                    }
                }
            }
            catch { }
        }

        if (Directory.Exists(LegacyHistoryDir))
        {
            foreach (var file in Directory.EnumerateFiles(LegacyHistoryDir, "*.*", SearchOption.AllDirectories))
            {
                if (!IsSupportedHistoryFile(file))
                    continue;

                var fileName = Path.GetFileName(file);
                if (trackedFileNames.Contains(fileName))
                    continue;

                var kind = GetKindForPath(file);
                if (TryMigrateLegacyFile(file, kind, out var migrated))
                {
                    _entries.Add(migrated);
                    trackedFileNames.Add(migrated.FileName);
                    changed = true;
                }
            }
        }

        if (!File.Exists(OcrIndexPath) && File.Exists(LegacyOcrIndexPath))
        {
            try
            {
                _ocrEntries = JsonSerializer.Deserialize<List<OcrHistoryEntry>>(
                    File.ReadAllText(LegacyOcrIndexPath), JsonOpts) ?? new();
                SaveOcrIndex();
                changed = true;
            }
            catch { }
        }

        if (!File.Exists(ColorIndexPath) && File.Exists(LegacyColorIndexPath))
        {
            try
            {
                _colorEntries = JsonSerializer.Deserialize<List<ColorHistoryEntry>>(
                    File.ReadAllText(LegacyColorIndexPath), JsonOpts) ?? new();
                SaveColorIndex();
                changed = true;
            }
            catch { }
        }

        if (changed)
        {
            _entries = _entries.OrderByDescending(e => e.CapturedAt).ToList();
            InvalidateFilteredCache();
            SaveIndex();
        }
    }

    private static bool TryMigrateLegacyFile(string sourcePath, HistoryKind legacyKind, out HistoryEntry migrated)
    {
        migrated = new HistoryEntry();

        if (!File.Exists(sourcePath))
            return false;

        try
        {
            var fileName = Path.GetFileName(sourcePath);
            var targetDir = legacyKind == HistoryKind.Sticker || sourcePath.StartsWith(LegacyStickerDir, StringComparison.OrdinalIgnoreCase)
                ? StickerDir
                : HistoryDir;
            var targetPath = Path.Combine(targetDir, fileName);

            Directory.CreateDirectory(targetDir);
            if (!sourcePath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                File.Move(sourcePath, targetPath, overwrite: true);

            var fi = new FileInfo(targetPath);
            migrated = new HistoryEntry
            {
                FileName = fi.Name,
                FilePath = targetPath,
                CapturedAt = fi.CreationTime,
                Width = 0,
                Height = 0,
                FileSizeBytes = fi.Length,
                Kind = GetKindForPath(targetPath, legacyKind)
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSupportedHistoryFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp";
    }

    private static HistoryKind GetKindForPath(string path, HistoryKind? fallback = null)
    {
        if (path.StartsWith(StickerDir, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(LegacyStickerDir, StringComparison.OrdinalIgnoreCase))
            return HistoryKind.Sticker;

        if (Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase))
            return HistoryKind.Gif;

        return fallback ?? HistoryKind.Image;
    }

    /// <summary>
    /// Scans one or more directories for image files not tracked in the index
    /// and adds them so the history is complete. Call after Load().
    /// </summary>
    public void RecoverFromDirectories(params string[] dirs)
    {
        bool changed = false;
        lock (_gate)
        {
            var tracked = new HashSet<string>(_entries.Select(e => e.FilePath), StringComparer.OrdinalIgnoreCase);

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
                {
                    if (file.StartsWith(StickerDir, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp")) continue;
                    if (tracked.Contains(file)) continue;

                    try
                    {
                        var fi = new FileInfo(file);
                        _entries.Add(new HistoryEntry
                        {
                            FileName = fi.Name,
                            FilePath = file,
                            CapturedAt = fi.CreationTime,
                            Width = 0,
                            Height = 0,
                            FileSizeBytes = fi.Length,
                            Kind = ext == ".gif" ? HistoryKind.Gif : HistoryKind.Image
                        });
                        tracked.Add(file);
                        changed = true;
                    }
                    catch { }
                }
            }

            if (changed)
            {
                _entries = _entries.OrderByDescending(e => e.CapturedAt).ToList();
                InvalidateFilteredCache();
                SaveIndex();
            }
        }

        if (changed)
            NotifyChanged();
    }

    private static void AddDirectorySignature(HashCode hash, string path)
    {
        hash.Add(Directory.Exists(path));
        if (!Directory.Exists(path))
            return;

        hash.Add(Directory.GetLastWriteTimeUtc(path).Ticks);
    }

    private static void AddFileSignature(HashCode hash, string path)
    {
        hash.Add(File.Exists(path));
        if (!File.Exists(path))
            return;

        var info = new FileInfo(path);
        hash.Add(info.Length);
        hash.Add(info.LastWriteTimeUtc.Ticks);
    }

    public void PruneByRetention(HistoryRetentionPeriod retention)
    {
        lock (_gate)
        {
            RetentionPeriod = retention;
            var cutoff = retention switch
            {
                HistoryRetentionPeriod.OneDay => DateTime.Now.AddDays(-1),
                HistoryRetentionPeriod.SevenDays => DateTime.Now.AddDays(-7),
                HistoryRetentionPeriod.ThirtyDays => DateTime.Now.AddDays(-30),
                HistoryRetentionPeriod.NinetyDays => DateTime.Now.AddDays(-90),
                _ => DateTime.MinValue
            };

            if (retention == HistoryRetentionPeriod.Never) return;

            foreach (var e in _entries.Where(e => e.CapturedAt < cutoff).ToList())
            {
                _entries.Remove(e);
                try { File.Delete(e.FilePath); } catch { }
            }
            InvalidateFilteredCache();
            _ocrEntries.RemoveAll(e => e.CapturedAt < cutoff);
            _colorEntries.RemoveAll(e => e.CapturedAt < cutoff);
            SaveIndex();
            SaveOcrIndex();
            SaveColorIndex();
        }
        NotifyChanged();
    }

    public void SaveIndex()
    {
        lock (_gate)
        {
            _indexDirty = true;
            ScheduleFlush_NoLock();
        }
    }

    private void SaveOcrIndex()
    {
        lock (_gate)
        {
            _ocrDirty = true;
            ScheduleFlush_NoLock();
        }
    }

    private void SaveColorIndex()
    {
        lock (_gate)
        {
            _colorDirty = true;
            ScheduleFlush_NoLock();
        }
    }

    public void FlushPendingWrites()
    {
        lock (_gate)
        {
            if (_indexDirty)
            {
                SafeWriteAllText(IndexPath, JsonSerializer.Serialize(_entries, JsonOpts));
                _indexDirty = false;
            }

            if (_ocrDirty)
            {
                SafeWriteAllText(OcrIndexPath, JsonSerializer.Serialize(_ocrEntries, JsonOpts));
                _ocrDirty = false;
            }

            if (_colorDirty)
            {
                SafeWriteAllText(ColorIndexPath, JsonSerializer.Serialize(_colorEntries, JsonOpts));
                _colorDirty = false;
            }
        }
    }

    private void ScheduleFlush_NoLock()
    {
        _flushTimer.Change(250, Timeout.Infinite);
    }

    private static void SafeWriteAllText(string path, string contents)
    {
        var tmpPath = path + ".tmp";
        try
        {
            File.WriteAllText(tmpPath, contents);
            File.Move(tmpPath, path, overwrite: true);
        }
        catch
        {
            File.WriteAllText(path, contents);
        }
    }
}
