using System.IO;
using System.Net.Http;

namespace Yoink.Services;

/// <summary>
/// Downloads Tesseract traineddata files from the official tessdata repository
/// so OCR can recognize non-Latin scripts.
/// </summary>
public static class TessdataService
{
    private const string TessdataBaseUrl = "https://github.com/tesseract-ocr/tessdata/raw/main/";

    /// <summary>All language packs available from tessdata with human-readable labels.</summary>
    public static readonly IReadOnlyList<(string Code, string Name)> AvailableLanguages = new[]
    {
        ("afr", "Afrikaans"),
        ("amh", "Amharic"),
        ("ara", "Arabic"),
        ("asm", "Assamese"),
        ("aze", "Azerbaijani"),
        ("bel", "Belarusian"),
        ("ben", "Bengali"),
        ("bod", "Tibetan"),
        ("bos", "Bosnian"),
        ("bre", "Breton"),
        ("bul", "Bulgarian"),
        ("cat", "Catalan"),
        ("ceb", "Cebuano"),
        ("ces", "Czech"),
        ("chi_sim", "Chinese (Simplified)"),
        ("chi_tra", "Chinese (Traditional)"),
        ("chr", "Cherokee"),
        ("cos", "Corsican"),
        ("cym", "Welsh"),
        ("dan", "Danish"),
        ("deu", "German"),
        ("div", "Divehi"),
        ("dzo", "Dzongkha"),
        ("ell", "Greek"),
        ("eng", "English"),
        ("enm", "English (Middle)"),
        ("epo", "Esperanto"),
        ("est", "Estonian"),
        ("eus", "Basque"),
        ("fao", "Faroese"),
        ("fas", "Persian"),
        ("fil", "Filipino"),
        ("fin", "Finnish"),
        ("fra", "French"),
        ("frm", "French (Middle)"),
        ("fry", "Western Frisian"),
        ("gla", "Scottish Gaelic"),
        ("gle", "Irish"),
        ("glg", "Galician"),
        ("grc", "Greek (Ancient)"),
        ("guj", "Gujarati"),
        ("hat", "Haitian"),
        ("heb", "Hebrew"),
        ("hin", "Hindi"),
        ("hrv", "Croatian"),
        ("hun", "Hungarian"),
        ("hye", "Armenian"),
        ("iku", "Inuktitut"),
        ("ind", "Indonesian"),
        ("isl", "Icelandic"),
        ("ita", "Italian"),
        ("jav", "Javanese"),
        ("jpn", "Japanese"),
        ("kan", "Kannada"),
        ("kat", "Georgian"),
        ("kaz", "Kazakh"),
        ("khm", "Khmer"),
        ("kir", "Kyrgyz"),
        ("kor", "Korean"),
        ("lao", "Lao"),
        ("lat", "Latin"),
        ("lav", "Latvian"),
        ("lit", "Lithuanian"),
        ("ltz", "Luxembourgish"),
        ("mal", "Malayalam"),
        ("mar", "Marathi"),
        ("mkd", "Macedonian"),
        ("mlt", "Maltese"),
        ("mon", "Mongolian"),
        ("mri", "Maori"),
        ("msa", "Malay"),
        ("mya", "Myanmar"),
        ("nep", "Nepali"),
        ("nld", "Dutch"),
        ("nor", "Norwegian"),
        ("oci", "Occitan"),
        ("ori", "Oriya"),
        ("pan", "Panjabi"),
        ("pol", "Polish"),
        ("por", "Portuguese"),
        ("pus", "Pashto"),
        ("que", "Quechua"),
        ("ron", "Romanian"),
        ("rus", "Russian"),
        ("san", "Sanskrit"),
        ("sin", "Sinhala"),
        ("slk", "Slovak"),
        ("slv", "Slovenian"),
        ("snd", "Sindhi"),
        ("spa", "Spanish"),
        ("sqi", "Albanian"),
        ("srp", "Serbian"),
        ("srp_latn", "Serbian (Latin)"),
        ("sun", "Sundanese"),
        ("swa", "Swahili"),
        ("swe", "Swedish"),
        ("syr", "Syriac"),
        ("tam", "Tamil"),
        ("tat", "Tatar"),
        ("tel", "Telugu"),
        ("tgk", "Tajik"),
        ("tha", "Thai"),
        ("tir", "Tigrinya"),
        ("ton", "Tonga"),
        ("tur", "Turkish"),
        ("uig", "Uyghur"),
        ("ukr", "Ukrainian"),
        ("urd", "Urdu"),
        ("uzb", "Uzbek"),
        ("vie", "Vietnamese"),
        ("yid", "Yiddish"),
        ("yor", "Yoruba"),
        ("zho", "Chinese"),
    };

    public static string GetLanguageName(string code)
    {
        foreach (var (c, n) in AvailableLanguages)
            if (c.Equals(code, StringComparison.OrdinalIgnoreCase)) return $"{n} ({c})";
        return code;
    }

    public static string GetTessdataDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = Path.Combine(baseDir, "Tessdata");
        if (Directory.Exists(dir)) return dir;
        var lower = Path.Combine(baseDir, "tessdata");
        if (Directory.Exists(lower)) return lower;
        return dir;
    }

    public static bool IsLanguageInstalled(string code)
    {
        var dir = GetTessdataDirectory();
        return File.Exists(Path.Combine(dir, $"{code}.traineddata"));
    }

    public static async Task DownloadLanguageAsync(string code, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var dir = GetTessdataDirectory();
        Directory.CreateDirectory(dir);

        var targetPath = Path.Combine(dir, $"{code}.traineddata");
        if (File.Exists(targetPath))
            return;

        var url = $"{TessdataBaseUrl}{code}.traineddata";
        progress?.Report($"Downloading {code}.traineddata...");

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(5);

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var tempPath = targetPath + ".tmp";
        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

            File.Move(tempPath, targetPath, overwrite: true);
            progress?.Report($"Installed {code}.traineddata");
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            throw;
        }
    }

    public static bool RemoveLanguage(string code)
    {
        if (code.Equals("eng", StringComparison.OrdinalIgnoreCase))
            return false; // Don't remove English

        var path = Path.Combine(GetTessdataDirectory(), $"{code}.traineddata");
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch { return false; }
    }
}
