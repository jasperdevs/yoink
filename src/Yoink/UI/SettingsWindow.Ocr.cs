using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Yoink.Services;

namespace Yoink.UI;

public partial class SettingsWindow
{
    private void LoadOcrLanguageOptions(string? selectedTag)
    {
        OcrLanguageCombo.Items.Clear();

        OcrLanguageCombo.Items.Add(new ComboBoxItem
        {
            Content = "Auto (English)",
            Tag = "auto"
        });

        foreach (var language in OcrService.GetAvailableRecognizerLanguages(refresh: true))
        {
            OcrLanguageCombo.Items.Add(new ComboBoxItem
            {
                Content = GetLanguageLabel(language),
                Tag = language
            });
        }

        var targetTag = string.IsNullOrWhiteSpace(selectedTag) ? "auto" : selectedTag;
        var selectedItem = OcrLanguageCombo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, targetTag, StringComparison.OrdinalIgnoreCase))
            ?? OcrLanguageCombo.Items.OfType<ComboBoxItem>().First();

        OcrLanguageCombo.SelectedItem = selectedItem;
    }

    private void OcrLanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (OcrLanguageCombo.SelectedItem is not ComboBoxItem item) return;

        _settingsService.Settings.OcrLanguageTag = item.Tag as string ?? "auto";
        _settingsService.Save();
    }

    private static string GetLanguageLabel(string languageTag) => languageTag.ToLowerInvariant() switch
    {
        "eng" => "English (eng)",
        _ => languageTag
    };
}
