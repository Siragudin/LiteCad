using System.Globalization;

namespace LiteCad.Resources;

public static class AppLanguage
{
    public const string English = "en";
    public const string Russian = "ru";

    public static readonly IReadOnlyList<string> Supported = [English, Russian];

    public static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en");

    public static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru");

    public static bool IsSupported(string? language)
        => language is English or Russian;

    public static CultureInfo ToCulture(string language)
        => language switch
        {
            Russian => RussianCulture,
            _ => EnglishCulture
        };

    public static string Normalize(string? language)
        => IsSupported(language) ? language! : English;

    public static string ToDisplayCode(string language)
        => Normalize(language).ToUpperInvariant();
}
