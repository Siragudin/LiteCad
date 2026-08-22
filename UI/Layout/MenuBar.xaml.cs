using LiteCad.Infrastructure;
using System.Windows;
using System.Windows.Controls;

namespace LiteCad.UI.Layout;

public partial class MenuBar : UserControl
{
    public event EventHandler<string>? ToolRequested;

    public event EventHandler<string>? EditCommandRequested;

    public event EventHandler<string>? FileCommandRequested;

    public LanguageSelector LanguageSelector => LanguageSelectorControl;

    public MenuBar()
    {
        InitializeComponent();
        Loaded += (_, _) => SyncThemeMenuChecks();
        Unloaded += (_, _) => ThemeManager.Instance.ThemeChanged -= OnThemeChanged;
        ThemeManager.Instance.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
        => SyncThemeMenuChecks();

    private void SyncThemeMenuChecks()
    {
        var isDark = AppTheme.IsDark(ThemeManager.Instance.Theme);
        ThemeLightMenuItem.IsChecked = !isDark;
        ThemeDarkMenuItem.IsChecked = isDark;
    }

    private void ToolMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string toolTag })
        {
            ToolRequested?.Invoke(this, toolTag);
        }
    }

    private void EditMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string command })
        {
            EditCommandRequested?.Invoke(this, command);
        }
    }

    private void FileMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string command })
        {
            FileCommandRequested?.Invoke(this, command);
        }
    }

    private void ThemeMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string theme })
        {
            ThemeManager.Instance.SetTheme(theme);
        }
    }
}
