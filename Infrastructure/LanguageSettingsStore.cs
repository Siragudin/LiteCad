using LiteCad.Resources;
using System.IO;
using System.Text.Json;

namespace LiteCad.Infrastructure;

public sealed class LanguageSettingsStore
{
    public static string SettingsDirectory
        => AppSettingsStore.SettingsDirectory;

    public static string SettingsFilePath => AppSettingsStore.SettingsFilePath;

    public static void SetSettingsDirectoryForTests(string? directory)
        => AppSettingsStore.SetSettingsDirectoryForTests(directory);

    public static string LoadLanguage()
        => AppSettingsStore.LoadLanguage();

    public static void SaveLanguage(string language)
        => AppSettingsStore.SaveLanguage(language);
}
