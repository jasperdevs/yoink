using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Yoink.Services;

public enum TranslationModel
{
    Argos = 0,
    Google = 1
}

public static class TranslationService
{
    private const string PythonLauncherArg = "-3";

    private sealed record PythonRunResult(int ExitCode, string StdOut, string StdErr);

    public static string GetModelLabel(TranslationModel model) => model switch
    {
        TranslationModel.Argos => "Argos Translate",
        TranslationModel.Google => "Google Translate",
        _ => "Argos Translate"
    };

    public static readonly IReadOnlyList<(string Code, string Name)> SupportedLanguages = new[]
    {
        ("auto", "Auto-detect"),
        ("ar", "Arabic"),
        ("az", "Azerbaijani"),
        ("bg", "Bulgarian"),
        ("bn", "Bengali"),
        ("ca", "Catalan"),
        ("cs", "Czech"),
        ("da", "Danish"),
        ("de", "German"),
        ("el", "Greek"),
        ("en", "English"),
        ("eo", "Esperanto"),
        ("es", "Spanish"),
        ("et", "Estonian"),
        ("fa", "Persian"),
        ("fi", "Finnish"),
        ("fr", "French"),
        ("ga", "Irish"),
        ("he", "Hebrew"),
        ("hi", "Hindi"),
        ("hu", "Hungarian"),
        ("id", "Indonesian"),
        ("it", "Italian"),
        ("ja", "Japanese"),
        ("ko", "Korean"),
        ("lt", "Lithuanian"),
        ("lv", "Latvian"),
        ("ms", "Malay"),
        ("nb", "Norwegian"),
        ("nl", "Dutch"),
        ("pl", "Polish"),
        ("pt", "Portuguese"),
        ("ro", "Romanian"),
        ("ru", "Russian"),
        ("sk", "Slovak"),
        ("sl", "Slovenian"),
        ("sq", "Albanian"),
        ("sr", "Serbian"),
        ("sv", "Swedish"),
        ("th", "Thai"),
        ("tl", "Tagalog"),
        ("tr", "Turkish"),
        ("uk", "Ukrainian"),
        ("ur", "Urdu"),
        ("vi", "Vietnamese"),
        ("zh", "Chinese"),
    };

    public static string GetLanguageName(string code)
    {
        foreach (var (c, n) in SupportedLanguages)
            if (c.Equals(code, StringComparison.OrdinalIgnoreCase)) return n;
        return code;
    }

    // --- Install (Argos only — Google needs no install) ---

    public static async Task EnsureInstalledAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (await IsArgosReadyAsync(cancellationToken).ConfigureAwait(false))
            return;

        progress?.Report("Installing Argos Translate...");

        var install = await RunPythonAsync(new[]
        {
            PythonLauncherArg, "-m", "pip", "install", "--user", "--upgrade", "argostranslate"
        }, cancellationToken).ConfigureAwait(false);

        if (install.ExitCode != 0)
        {
            var message = !string.IsNullOrWhiteSpace(install.StdErr)
                ? install.StdErr.Trim()
                : install.StdOut.Trim();

            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                ? "Couldn't install Argos Translate."
                : message);
        }
    }

    public static async Task UninstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report("Uninstalling Argos Translate...");
        await RunPythonAsync(new[]
        {
            PythonLauncherArg, "-m", "pip", "uninstall", "-y", "argostranslate"
        }, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<bool> IsArgosReadyAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsPythonLauncherAvailableAsync(cancellationToken).ConfigureAwait(false))
            return false;

        var result = await RunPythonAsync(new[]
        {
            PythonLauncherArg, "-c", "import argostranslate; print('ok')"
        }, cancellationToken).ConfigureAwait(false);

        return result.ExitCode == 0;
    }

    // --- Translate ---

    public static async Task<string> TranslateAsync(string text, string fromCode, string toCode, TranslationModel model, CancellationToken cancellationToken = default)
    {
        if (model == TranslationModel.Google)
        {
            var apiKey = _googleApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Google Translate API key not set. Add it in Settings → OCR.");

            return await TranslateWithGoogleAsync(text, fromCode, toCode, apiKey, cancellationToken).ConfigureAwait(false);
        }

        // Argos Translate
        var result = await RunPythonAsync(new[]
        {
            PythonLauncherArg, "-c", BuildArgosTranslateScript(), text, fromCode, toCode
        }, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            var message = !string.IsNullOrWhiteSpace(result.StdErr)
                ? result.StdErr.Trim()
                : result.StdOut.Trim();

            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                ? "Translation failed."
                : message);
        }

        return result.StdOut.TrimEnd();
    }

    private static string? _googleApiKey;

    public static void SetGoogleApiKey(string? key)
    {
        _googleApiKey = key;
    }

    public static bool HasGoogleApiKey => !string.IsNullOrWhiteSpace(_googleApiKey);

    private static async Task<string> TranslateWithGoogleAsync(string text, string fromCode, string toCode, string apiKey, CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        var source = fromCode == "auto" ? "" : $"&source={Uri.EscapeDataString(fromCode)}";
        var url = $"https://translation.googleapis.com/language/translate/v2?key={Uri.EscapeDataString(apiKey)}&target={Uri.EscapeDataString(toCode)}{source}&q={Uri.EscapeDataString(text)}";

        var response = await http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(response);
        return doc.RootElement
            .GetProperty("data")
            .GetProperty("translations")[0]
            .GetProperty("translatedText")
            .GetString() ?? "";
    }

    private static async Task<bool> IsPythonLauncherAvailableAsync(CancellationToken cancellationToken)
    {
        var result = await RunPythonAsync(new[] { PythonLauncherArg, "--version" }, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    private static async Task<PythonRunResult> RunPythonAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "py",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        psi.EnvironmentVariables["PYTHONUTF8"] = "1";
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
            return new PythonRunResult(-1, "", "Could not start Python launcher.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return new PythonRunResult(process.ExitCode, stdout, stderr);
    }

    private static string BuildArgosTranslateScript() => """
import sys
import argostranslate.translate as tr

text = sys.argv[1]
from_code = sys.argv[2]
to_code = sys.argv[3]

if from_code == "auto":
    from_code = "en"

# Check if language pack is installed, install if needed
installed = tr.get_installed_languages()
from_lang = next((l for l in installed if l.code == from_code), None)
to_lang = next((l for l in installed if l.code == to_code), None)

if not from_lang or not to_lang or not from_lang.get_translation(to_lang):
    import argostranslate.package as pkg
    print("Installing language pack...", file=sys.stderr, flush=True)
    pkg.update_package_index()
    available = pkg.get_available_packages()
    pack = next((p for p in available if p.from_code == from_code and p.to_code == to_code), None)
    if pack:
        pack.install()

translated = tr.translate(text, from_code, to_code)
print(translated)
""";
}
