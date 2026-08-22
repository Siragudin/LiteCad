using LiteCad.Resources;
using System.IO;
using System.Text.Json;

namespace LiteCad.Infrastructure;

public sealed class AppSettingsStore
{
    private const string SettingsFileName = "settings.json";

    private static string? _settingsDirectoryOverride;

    public static string SettingsDirectory
        => _settingsDirectoryOverride
           ?? Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
               "LiteCad");

    public static string SettingsFilePath
        => Path.Combine(SettingsDirectory, SettingsFileName);

    public static void SetSettingsDirectoryForTests(string? directory)
        => _settingsDirectoryOverride = directory;

    public static UserSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new UserSettings();
            }

            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json);
            return Normalize(settings ?? new UserSettings());
        }
        catch
        {
            return new UserSettings();
        }
    }

    public static void Save(UserSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var normalized = Normalize(settings);
        var json = JsonSerializer.Serialize(normalized, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFilePath, json);
    }

    public static string LoadLanguage()
        => Load().Language;

    public static void SaveLanguage(string language)
    {
        var settings = Load();
        settings.Language = language;
        Save(settings);
    }

    public static string LoadTheme()
        => Load().Theme;

    public static void SaveTheme(string theme)
    {
        var settings = Load();
        settings.Theme = theme;
        Save(settings);
    }

    private static UserSettings Normalize(UserSettings settings)
    {
        settings.Language = AppLanguage.Normalize(settings.Language);
        settings.Theme = AppTheme.Normalize(settings.Theme);
        return settings;
    }

    public sealed class UserSettings
    {
        public string Language { get; set; } = AppLanguage.English;

        public string Theme { get; set; } = AppTheme.Light;
    }
}
