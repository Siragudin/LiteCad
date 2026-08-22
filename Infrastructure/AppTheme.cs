namespace LiteCad.Infrastructure;

public static class AppTheme
{
    public const string Light = "Light";

    public const string Dark = "Dark";

    public static readonly IReadOnlyList<string> Supported = [Light, Dark];

    public static string Normalize(string? theme)
        => string.Equals(theme, Dark, StringComparison.OrdinalIgnoreCase) ? Dark : Light;

    public static bool IsDark(string theme)
        => string.Equals(Normalize(theme), Dark, StringComparison.OrdinalIgnoreCase);
}
