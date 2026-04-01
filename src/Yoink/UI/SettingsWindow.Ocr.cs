using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Windows.Globalization;
using Yoink.Services;

namespace Yoink.UI;

public partial class SettingsWindow
{
    private void LoadOcrLanguageOptions(string? selectedTag)
    {
        OcrLanguageCombo.Items.Clear();

        OcrLanguageCombo.Items.Add(new ComboBoxItem
        {
            Content = "Auto (use Windows profile languages)",
            Tag = "auto"
        });

        // Refresh once per settings open so newly installed language packs appear without restarting Yoink.
        foreach (var language in OcrService.GetAvailableRecognizerLanguages(refresh: true))
        {
            OcrLanguageCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{language.DisplayName} ({language.LanguageTag})",
                Tag = language.LanguageTag
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
}
