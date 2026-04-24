using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;

namespace OddSnap.Services;

public sealed record LocalizationLanguage(string Code, string EnglishName, string NativeName, bool IsRightToLeft);

public static class LocalizationService
{
    public const string AutoLanguageCode = "auto";
    public const string DefaultLanguageCode = "en";

    private static readonly LocalizationLanguage[] BuiltInLanguages =
    [
        new(DefaultLanguageCode, "English", "English", false),
        new("ar", "Arabic", "العربية", true),
        new("bg", "Bulgarian", "Български", false),
        new("ca", "Catalan", "Català", false),
        new("cs", "Czech", "Čeština", false),
        new("da", "Danish", "Dansk", false),
        new("de", "German", "Deutsch", false),
        new("el", "Greek", "Ελληνικά", false),
        new("es", "Spanish", "Español", false),
        new("et", "Estonian", "Eesti", false),
        new("fi", "Finnish", "Suomi", false),
        new("fr", "French", "Français", false),
        new("he", "Hebrew", "עברית", true),
        new("hi", "Hindi", "हिन्दी", false),
        new("hr", "Croatian", "Hrvatski", false),
        new("hu", "Hungarian", "Magyar", false),
        new("id", "Indonesian", "Indonesia", false),
        new("it", "Italian", "Italiano", false),
        new("ja", "Japanese", "日本語", false),
        new("ko", "Korean", "한국어", false),
        new("lt", "Lithuanian", "Lietuvių", false),
        new("lv", "Latvian", "Latviešu", false),
        new("ms", "Malay", "Melayu", false),
        new("nb", "Norwegian Bokmål", "Norsk bokmål", false),
        new("nl", "Dutch", "Nederlands", false),
        new("pl", "Polish", "Polski", false),
        new("pt-BR", "Portuguese (Brazil)", "Português (Brasil)", false),
        new("pt-PT", "Portuguese (Portugal)", "Português (Portugal)", false),
        new("ro", "Romanian", "Română", false),
        new("ru", "Russian", "Русский", false),
        new("sk", "Slovak", "Slovenčina", false),
        new("sl", "Slovenian", "Slovenščina", false),
        new("sr-Latn", "Serbian (Latin)", "Srpski (latinica)", false),
        new("sv", "Swedish", "Svenska", false),
        new("th", "Thai", "ไทย", false),
        new("tr", "Turkish", "Türkçe", false),
        new("uk", "Ukrainian", "Українська", false),
        new("vi", "Vietnamese", "Tiếng Việt", false),
        new("zh-Hans", "Chinese (Simplified)", "简体中文", false),
        new("zh-Hant", "Chinese (Traditional)", "繁體中文", false),
    ];

    private static readonly DependencyProperty SourceTextProperty =
        DependencyProperty.RegisterAttached("SourceText", typeof(string), typeof(LocalizationService));

    private static readonly DependencyProperty SourceContentProperty =
        DependencyProperty.RegisterAttached("SourceContent", typeof(string), typeof(LocalizationService));

    private static readonly DependencyProperty SourceHeaderProperty =
        DependencyProperty.RegisterAttached("SourceHeader", typeof(string), typeof(LocalizationService));

    private static readonly DependencyProperty SourceToolTipProperty =
        DependencyProperty.RegisterAttached("SourceToolTip", typeof(string), typeof(LocalizationService));

    private static readonly object Gate = new();
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> TranslationCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static string CurrentLanguageCode { get; private set; } = DefaultLanguageCode;
    public static string CurrentLanguageSetting { get; private set; } = AutoLanguageCode;

    public static IReadOnlyList<LocalizationLanguage> Languages => GetAvailableLanguages();

    public static bool HasInterfaceTranslations(string languageCode)
    {
        var normalized = NormalizeLanguageSetting(languageCode);
        if (string.Equals(normalized, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(normalized, AutoLanguageCode, StringComparison.OrdinalIgnoreCase))
            normalized = ResolveLanguageCode(normalized);

        return File.Exists(Path.Combine(GetLocalizationDirectory(), $"{normalized}.json"));
    }

    public static string NormalizeLanguageSetting(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode) ||
            string.Equals(languageCode, AutoLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return AutoLanguageCode;
        }

        var normalized = NormalizeLanguageAlias(languageCode.Trim().Replace('_', '-'));
        var languages = Languages;
        var exact = languages.FirstOrDefault(language =>
            string.Equals(language.Code, normalized, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact.Code;

        var neutral = NormalizeLanguageAlias(normalized.Split('-', 2)[0]);
        return languages.Any(language => string.Equals(language.Code, neutral, StringComparison.OrdinalIgnoreCase))
            ? neutral
            : AutoLanguageCode;
    }

    private static string NormalizeLanguageAlias(string languageCode)
    {
        if (languageCode.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return languageCode.Equals("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                   languageCode.Equals("zh-HK", StringComparison.OrdinalIgnoreCase) ||
                   languageCode.Equals("zh-MO", StringComparison.OrdinalIgnoreCase) ||
                   languageCode.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase)
                ? "zh-Hant"
                : "zh-Hans";
        }

        if (languageCode.Equals("pt", StringComparison.OrdinalIgnoreCase))
            return "pt-BR";

        if (languageCode.Equals("no", StringComparison.OrdinalIgnoreCase))
            return "nb";

        return languageCode;
    }

    public static string ResolveLanguageCode(string? languageSetting, CultureInfo? systemCulture = null)
    {
        var normalized = NormalizeLanguageSetting(languageSetting);
        if (!string.Equals(normalized, AutoLanguageCode, StringComparison.OrdinalIgnoreCase))
            return normalized;

        var culture = systemCulture ?? CultureInfo.CurrentUICulture;
        var languages = Languages;
        var exact = languages.FirstOrDefault(language =>
            string.Equals(language.Code, culture.Name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact.Code;

        var neutral = culture.TwoLetterISOLanguageName;
        return languages.Any(language => string.Equals(language.Code, neutral, StringComparison.OrdinalIgnoreCase))
            ? neutral
            : DefaultLanguageCode;
    }

    public static LocalizationLanguage GetLanguage(string languageCode)
    {
        var resolved = ResolveLanguageCode(languageCode);
        return Languages.First(language => string.Equals(language.Code, resolved, StringComparison.OrdinalIgnoreCase));
    }

    public static void ApplyCurrentCulture(string? languageSetting)
    {
        CurrentLanguageSetting = NormalizeLanguageSetting(languageSetting);
        var languageCode = ResolveLanguageCode(CurrentLanguageSetting);
        CurrentLanguageCode = languageCode;

        try
        {
            var culture = CultureInfo.GetCultureInfo(languageCode);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
        }
        catch (CultureNotFoundException)
        {
            CurrentLanguageCode = DefaultLanguageCode;
        }
    }

    public static string ResolveContentLanguageCode(string? languageSetting = null, CultureInfo? systemCulture = null)
    {
        var normalized = NormalizeLanguageSetting(languageSetting ?? CurrentLanguageSetting);
        if (!string.Equals(normalized, AutoLanguageCode, StringComparison.OrdinalIgnoreCase))
            return ResolveLanguageCode(normalized);

        var culture = systemCulture ?? CultureInfo.InstalledUICulture;
        return string.IsNullOrWhiteSpace(culture.Name) ? DefaultLanguageCode : culture.Name;
    }

    public static string Translate(string text) => Translate(CurrentLanguageCode, text);

    public static string Translate(string languageCode, string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var resolved = ResolveLanguageCode(languageCode);
        if (string.Equals(resolved, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
            return text;

        var translations = GetTranslations(resolved);
        return translations.TryGetValue(text, out var translated) && !string.IsNullOrWhiteSpace(translated)
            ? translated
            : text;
    }

    public static void ApplyTo(DependencyObject root, string? languageSetting = null)
    {
        var languageCode = ResolveLanguageCode(languageSetting ?? CurrentLanguageCode);
        CurrentLanguageCode = languageCode;
        var language = GetLanguage(languageCode);
        var flowDirection = language.IsRightToLeft ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;

        foreach (var element in Enumerate(root))
        {
            try
            {
                ApplyToElement(element, languageCode, flowDirection);
            }
            catch (Exception ex)
            {
                AppDiagnostics.LogWarning("localization.apply-element", ex.Message, ex);
            }
        }
    }

    private static void ApplyToElement(DependencyObject element, string languageCode, System.Windows.FlowDirection flowDirection)
    {
        if (element is FrameworkElement frameworkElement)
        {
            frameworkElement.FlowDirection = ShouldPreserveLeftToRight(frameworkElement)
                ? System.Windows.FlowDirection.LeftToRight
                : flowDirection;
            try { frameworkElement.Language = XmlLanguage.GetLanguage(languageCode); } catch { }
        }

        if (element is Window window)
        {
            window.FlowDirection = flowDirection;
            if (!string.IsNullOrWhiteSpace(window.Title))
            {
                var source = GetOrSetSource(window, SourceTextProperty, window.Title);
                window.Title = Translate(languageCode, source);
            }
        }

        if (element is TextBlock textBlock)
        {
            if (IsIconTextBlock(textBlock))
            {
                textBlock.FlowDirection = System.Windows.FlowDirection.LeftToRight;
                return;
            }

            if (textBlock.Inlines.Count > 0)
            {
                foreach (var inline in textBlock.Inlines.ToArray())
                    ApplyToInline(inline, languageCode);
            }
            else if (!string.IsNullOrEmpty(textBlock.Text))
            {
                var source = GetOrSetSource(textBlock, SourceTextProperty, textBlock.Text);
                textBlock.Text = Translate(languageCode, source);
            }
        }

        if (element is HeaderedContentControl headered && headered.Header is string header)
        {
            var source = GetOrSetSource(headered, SourceHeaderProperty, header);
            headered.Header = Translate(languageCode, source);
        }

        if (element is ContentControl contentControl && contentControl.Content is string content)
        {
            var source = GetOrSetSource(contentControl, SourceContentProperty, content);
            contentControl.Content = Translate(languageCode, source);
        }

        if (element is FrameworkElement { ToolTip: string toolTip } toolTipElement)
        {
            var source = GetOrSetSource(toolTipElement, SourceToolTipProperty, toolTip);
            toolTipElement.ToolTip = Translate(languageCode, source);
        }
    }

    private static string GetOrSetSource(DependencyObject element, DependencyProperty property, string currentValue)
    {
        if (element.GetValue(property) is string existing)
            return existing;

        element.SetValue(property, currentValue);
        return currentValue;
    }

    private static bool ShouldPreserveLeftToRight(FrameworkElement element) =>
        element is TextBlock textBlock && IsIconTextBlock(textBlock);

    private static bool IsIconTextBlock(TextBlock textBlock)
    {
        if (IsIconFont(textBlock.FontFamily))
            return true;

        return IsPrivateUseGlyph(textBlock.Text);
    }

    private static bool IsIconFont(System.Windows.Media.FontFamily? fontFamily)
    {
        var family = fontFamily?.ToString();
        return !string.IsNullOrWhiteSpace(family) &&
               (family.Contains("Segoe Fluent Icons", StringComparison.OrdinalIgnoreCase) ||
                family.Contains("Segoe MDL2 Assets", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPrivateUseGlyph(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        return trimmed.Length <= 2 && trimmed.All(ch => ch is >= '\uE000' and <= '\uF8FF');
    }

    private static void ApplyToInline(Inline inline, string languageCode)
    {
        if (inline is Run run)
        {
            if (string.IsNullOrEmpty(run.Text))
                return;

            var source = GetOrSetSource(run, SourceTextProperty, run.Text);
            run.Text = Translate(languageCode, source);
            return;
        }

        if (inline is Span span)
        {
            foreach (var child in span.Inlines.ToArray())
                ApplyToInline(child, languageCode);
        }
    }

    private static IEnumerable<DependencyObject> Enumerate(DependencyObject root)
    {
        var seen = new HashSet<DependencyObject>();
        var stack = new Stack<DependencyObject>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current))
                continue;

            yield return current;

            DependencyObject[] logicalChildren;
            try
            {
                logicalChildren = LogicalTreeHelper.GetChildren(current)
                    .OfType<DependencyObject>()
                    .ToArray();
            }
            catch (Exception ex)
            {
                AppDiagnostics.LogWarning("localization.enumerate-logical", ex.Message, ex);
                logicalChildren = [];
            }

            foreach (var child in logicalChildren)
                stack.Push(child);

            int visualChildren;
            try { visualChildren = System.Windows.Media.VisualTreeHelper.GetChildrenCount(current); }
            catch { visualChildren = 0; }

            for (int i = visualChildren - 1; i >= 0; i--)
            {
                try { stack.Push(System.Windows.Media.VisualTreeHelper.GetChild(current, i)); }
                catch { }
            }
        }
    }

    private static IReadOnlyDictionary<string, string> GetTranslations(string languageCode)
    {
        lock (Gate)
        {
            if (TranslationCache.TryGetValue(languageCode, out var cached))
                return cached;

            var loaded = LoadTranslations(languageCode);
            TranslationCache[languageCode] = loaded;
            return loaded;
        }
    }

    private static IReadOnlyDictionary<string, string> LoadTranslations(string languageCode)
    {
        var path = Path.Combine(GetLocalizationDirectory(), $"{languageCode}.json");
        if (!File.Exists(path))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ??
                   new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            AppDiagnostics.LogError("localization.load", ex, $"Failed to load {path}.");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static IReadOnlyList<LocalizationLanguage> GetAvailableLanguages()
    {
        var languages = new Dictionary<string, LocalizationLanguage>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in BuiltInLanguages)
            languages[language.Code] = language;

        var directory = GetLocalizationDirectory();
        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
            {
                var code = Path.GetFileNameWithoutExtension(file);
                if (!string.IsNullOrWhiteSpace(code) && !languages.ContainsKey(code))
                    languages[code] = CreateLanguageFromCode(code);
            }
        }

        return languages.Values
            .OrderBy(language => string.Equals(language.Code, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(language => language.EnglishName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static LocalizationLanguage CreateLanguageFromCode(string code)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            return new LocalizationLanguage(code, culture.EnglishName, culture.NativeName, culture.TextInfo.IsRightToLeft);
        }
        catch (CultureNotFoundException)
        {
            return new LocalizationLanguage(code, code, code, false);
        }
    }

    private static string GetLocalizationDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "Localization");
}
