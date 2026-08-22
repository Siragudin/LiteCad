using LiteCad.Rendering;
using System.Windows;

namespace LiteCad.Infrastructure;

public sealed class ThemeManager
{
    private const string ThemeDictionaryMarker = "CadThemeColors";

    public static ThemeManager Instance { get; } = new();

    private ThemeManager()
    {
    }

    public event EventHandler? ThemeChanged;

    public string Theme { get; private set; } = AppTheme.Light;

    public void Initialize(string? theme)
    {
        Theme = AppTheme.Normalize(theme);
        ApplyThemeResources(Theme);
        CanvasTheme.Apply(Theme);
    }

    public void SetTheme(string theme, bool persist = true)
    {
        var normalized = AppTheme.Normalize(theme);
        if (string.Equals(Theme, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Theme = normalized;
        ApplyThemeResources(normalized);
        CanvasTheme.Apply(normalized);

        if (persist)
        {
            AppSettingsStore.SaveTheme(normalized);
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void ApplyThemeResources(string theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        var source = AppTheme.IsDark(theme)
            ? new Uri("/LiteCad;component/UI/Resources/CadThemeDark.xaml", UriKind.Relative)
            : new Uri("/LiteCad;component/UI/Resources/CadThemeLight.xaml", UriKind.Relative);

        var themeDictionary = new ResourceDictionary { Source = source };
        var merged = Application.Current.Resources.MergedDictionaries;
        for (var i = 0; i < merged.Count; i++)
        {
            if (IsThemeColorDictionary(merged[i]))
            {
                merged[i] = themeDictionary;
                return;
            }
        }

        merged.Insert(0, themeDictionary);
    }

    private static bool IsThemeColorDictionary(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString;
        return source?.Contains(ThemeDictionaryMarker, StringComparison.OrdinalIgnoreCase) == true
               || source?.Contains("CadThemeLight.xaml", StringComparison.OrdinalIgnoreCase) == true
               || source?.Contains("CadThemeDark.xaml", StringComparison.OrdinalIgnoreCase) == true;
    }
}
