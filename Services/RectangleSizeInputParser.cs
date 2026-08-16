using System.Globalization;
using System.Text.RegularExpressions;

namespace LiteCad.Services;

public static partial class RectangleSizeInputParser
{
    public static bool TryParse(string text, out double width, out double height)
    {
        width = 0;
        height = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim()
            .Replace('×', 'x')
            .Replace('X', 'x')
            .Replace(',', 'x');

        normalized = MultipleSpaces().Replace(normalized, string.Empty);

        var parts = normalized.Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!TryParsePositive(parts[0], out width) || !TryParsePositive(parts[1], out height))
        {
            return false;
        }

        return width > 0 && height > 0;
    }

    public static string Format(double width, double height)
        => $"{width.ToString("F2", CultureInfo.InvariantCulture)} × {height.ToString("F2", CultureInfo.InvariantCulture)}";

    public static bool TryParseSingle(string text, out double value)
        => TryParsePositive(text, out value);

    private static bool TryParsePositive(string text, out double value)
    {
        value = 0;
        var normalized = text.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && value > 0;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpaces();
}
