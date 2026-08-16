using LiteCad.Resources;
using System.IO;
using System.Text.Json;

namespace LiteCad.Infrastructure;

public sealed class LanguageSettingsStore
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

    public static string LoadLanguage()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return AppLanguage.English;
            }

            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json);
            return AppLanguage.Normalize(settings?.Language);
        }
        catch
        {
            return AppLanguage.English;
        }
    }

    public static void SaveLanguage(string language)
    {
        var normalized = AppLanguage.Normalize(language);
        Directory.CreateDirectory(SettingsDirectory);
        var settings = new UserSettings { Language = normalized };
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFilePath, json);
    }

    private sealed class UserSettings
    {
        public string Language { get; set; } = AppLanguage.English;
    }
}
