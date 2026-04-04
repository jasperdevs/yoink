using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Tesseract;
using Yoink.Helpers;

namespace Yoink.Services;

public enum OcrWorkload
{
    Fast = 0,
    Full = 1
}

public static class OcrService
{
    public const string EngineId = "tesseract-eng-v3";

    private static readonly object LanguagesLock = new();
    private static readonly object EnginesLock = new();
    private static IReadOnlyList<string>? _cachedLanguages;
    private static readonly Dictionary<string, OcrEngineHandle> _engines = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> GetAvailableRecognizerLanguages(bool refresh = false)
    {
        if (!refresh && _cachedLanguages != null)
            return _cachedLanguages;

        lock (LanguagesLock)
        {
            if (!refresh && _cachedLanguages != null)
                return _cachedLanguages;

            var languages = EnumerateAvailableLanguages().ToList();
            if (!languages.Contains("eng", StringComparer.OrdinalIgnoreCase))
                languages.Insert(0, "eng");

            _cachedLanguages = languages
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code == "eng" ? 0 : 1)
                .ThenBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return _cachedLanguages;
        }
    }

    public static async Task<string> RecognizeAsync(Bitmap bitmap, string? languageTag = null, OcrWorkload workload = OcrWorkload.Full)
    {
        return await Task.Run(() => RecognizeSync(bitmap, languageTag, workload)).ConfigureAwait(false);
    }

    private static string RecognizeSync(Bitmap bitmap, string? languageTag, OcrWorkload workload)
    {
        var normalizedLanguage = ResolveLanguageTag(languageTag);
        var handle = GetOrCreateEngine(normalizedLanguage);

        try
        {
            var attempts = BuildRecognitionAttempts(bitmap, workload).ToList();
            foreach (var attempt in attempts)
            {
                using (attempt.Bitmap)
                {
                    foreach (var pageSegMode in attempt.PageSegModes)
                    {
                        var text = TryRecognize(handle, attempt.Bitmap, pageSegMode);
                        if (!string.IsNullOrWhiteSpace(text))
                            return text;
                    }
                }
            }
        }
        catch
        {
        }

        return "";
    }

    private static IEnumerable<OcrAttempt> BuildRecognitionAttempts(Bitmap source, OcrWorkload workload)
    {
        yield return new OcrAttempt(
            (Bitmap)source.Clone(),
            new[] { PageSegMode.SparseText, PageSegMode.Auto });

        yield return new OcrAttempt(
            PrepareForTextRecognition(source, applyThreshold: false),
            new[] { PageSegMode.Auto, PageSegMode.SingleBlock });

        if (workload == OcrWorkload.Fast)
            yield break;

        yield return new OcrAttempt(
            PrepareForTextRecognition(source, applyThreshold: true),
            new[] { PageSegMode.Auto, PageSegMode.SingleBlock });

        foreach (var crop in BuildTextRegionCrops(source))
        {
            yield return new OcrAttempt(
                crop,
                new[] { PageSegMode.SparseText, PageSegMode.Auto, PageSegMode.SingleBlock });
        }
    }

    private static string TryRecognize(OcrEngineHandle handle, Bitmap bitmap, PageSegMode pageSegMode)
    {
        var tmpPath = Path.Combine(Path.GetTempPath(), $"yoink_ocr_{Guid.NewGuid():N}.png");
        try
        {
            bitmap.Save(tmpPath, System.Drawing.Imaging.ImageFormat.Png);
            using var pix = Pix.LoadFromFile(tmpPath);
            lock (handle.Sync)
            {
                using var page = handle.Engine.Process(pix, pageSegMode);
                return page.GetText()?.Trim() ?? "";
            }
        }
        catch
        {
            return "";
        }
        finally
        {
            try { File.Delete(tmpPath); } catch { }
        }
    }

    private static Bitmap PrepareForTextRecognition(Bitmap source, bool applyThreshold)
    {
        var scale = Math.Max(1, source.Width < 1600 ? 2 : 1);
        var width = Math.Max(1, source.Width * scale);
        var height = Math.Max(1, source.Height * scale);
        var prepared = new Bitmap(width, height, PixelFormat.Format24bppRgb);

        using var graphics = Graphics.FromImage(prepared);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(0, 0, width, height));

        var rect = new Rectangle(0, 0, prepared.Width, prepared.Height);
        var data = prepared.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
        try
        {
            var stride = data.Stride;
            var bytes = Math.Abs(stride) * prepared.Height;
            var buffer = new byte[bytes];
            Marshal.Copy(data.Scan0, buffer, 0, bytes);

            for (int y = 0; y < prepared.Height; y++)
            {
                var rowOffset = y * stride;
                for (int x = 0; x < prepared.Width; x++)
                {
                    var offset = rowOffset + (x * 3);
                    var b = buffer[offset];
                    var g = buffer[offset + 1];
                    var r = buffer[offset + 2];
                    var luma = (int)Math.Round((r * 0.299) + (g * 0.587) + (b * 0.114));
                    if (applyThreshold)
                        luma = luma >= 180 ? 255 : 0;

                    buffer[offset] = (byte)luma;
                    buffer[offset + 1] = (byte)luma;
                    buffer[offset + 2] = (byte)luma;
                }
            }

            Marshal.Copy(buffer, 0, data.Scan0, bytes);
        }
        finally
        {
            prepared.UnlockBits(data);
        }

        return prepared;
    }

    private static IEnumerable<Bitmap> BuildTextRegionCrops(Bitmap source)
    {
        if (source.Width < 64 || source.Height < 64)
            yield break;

        var regions = new[]
        {
            new Rectangle(0, 0, source.Width, Math.Max(1, (int)Math.Round(source.Height * 0.38))),
            new Rectangle(0, Math.Max(0, (int)Math.Round(source.Height * 0.22)), source.Width, Math.Max(1, (int)Math.Round(source.Height * 0.56))),
            new Rectangle(0, Math.Max(0, (int)Math.Round(source.Height * 0.58)), source.Width, Math.Max(1, source.Height - (int)Math.Round(source.Height * 0.58)))
        };

        foreach (var region in regions)
        {
            if (region.Width < 32 || region.Height < 24)
                continue;

            using var cropped = source.Clone(region, PixelFormat.Format24bppRgb);
            yield return PrepareForTextRecognition(cropped, applyThreshold: false);
            yield return PrepareForTextRecognition(cropped, applyThreshold: true);
        }
    }

    private static OcrEngineHandle GetOrCreateEngine(string languageTag)
    {
        lock (EnginesLock)
        {
            if (_engines.TryGetValue(languageTag, out var existing))
                return existing;

            var engine = new TesseractEngine(GetTessdataDirectory(), languageTag, EngineMode.LstmOnly)
            {
                DefaultPageSegMode = PageSegMode.SparseText
            };
            engine.SetVariable("user_defined_dpi", "300");

            var created = new OcrEngineHandle(engine);
            _engines[languageTag] = created;
            return created;
        }
    }

    private static string ResolveLanguageTag(string? languageTag)
    {
        var available = GetAvailableRecognizerLanguages();

        if (string.IsNullOrWhiteSpace(languageTag) ||
            languageTag.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return available.FirstOrDefault() ?? "eng";
        }

        if (available.Contains(languageTag, StringComparer.OrdinalIgnoreCase))
            return languageTag;

        var primary = languageTag.Split('-', '_')[0];
        if (available.Contains(primary, StringComparer.OrdinalIgnoreCase))
            return primary;

        return available.FirstOrDefault() ?? "eng";
    }

    private static IEnumerable<string> EnumerateAvailableLanguages()
    {
        var dir = GetTessdataDirectory();
        if (!Directory.Exists(dir))
            yield break;

        foreach (var file in Directory.EnumerateFiles(dir, "*.traineddata", SearchOption.TopDirectoryOnly))
        {
            var code = Path.GetFileNameWithoutExtension(file);
            if (!string.IsNullOrWhiteSpace(code))
                yield return code;
        }
    }

    private static string GetTessdataDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        var localDir = Path.Combine(baseDir, "Tessdata");
        if (Directory.Exists(localDir))
            return localDir;

        var lowerDir = Path.Combine(baseDir, "tessdata");
        if (Directory.Exists(lowerDir))
            return lowerDir;

        return localDir;
    }

    private sealed record OcrEngineHandle(TesseractEngine Engine)
    {
        public object Sync { get; } = new();
    }

    private sealed record OcrAttempt(Bitmap Bitmap, IReadOnlyList<PageSegMode> PageSegModes);
}
