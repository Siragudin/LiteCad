using LiteCad.Infrastructure;
using System.ComponentModel;
using System.Globalization;

namespace LiteCad.Resources;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    private LocalizationManager()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public string Language { get; private set; } = AppLanguage.English;

    public CultureInfo CurrentCulture { get; private set; } = AppLanguage.EnglishCulture;

    public string LanguageDisplayCode => AppLanguage.ToDisplayCode(Language);

    public string this[string key] => Strings.Get(key);

    public void Initialize(string? language)
        => SetLanguage(AppLanguage.Normalize(language), persist: false);

    public void SetLanguage(string language, bool persist = true)
    {
        var normalized = AppLanguage.Normalize(language);
        if (string.Equals(Language, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Language = normalized;
        CurrentCulture = AppLanguage.ToCulture(normalized);
        ApplyCulture(CurrentCulture);

        if (persist)
        {
            LanguageSettingsStore.SaveLanguage(normalized);
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageDisplayCode)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
    }
}
