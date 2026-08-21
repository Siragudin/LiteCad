using System.Globalization;

namespace LiteCad.Services;

public static class LinearInputParser
{
    private const double MillimetersPerMeter = 1000.0;

    public static bool TryParse(
        string text,
        LinearDisplayUnit unit,
        bool allowNegative,
        bool allowEmpty,
        out double internalMillimeters)
    {
        internalMillimeters = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return allowEmpty;
        }

        var normalized = text.Trim().Replace(',', '.');
        var styles = NumberStyles.Float;
        if (allowNegative)
        {
            styles |= NumberStyles.AllowLeadingSign;
        }

        if (!double.TryParse(normalized, styles, CultureInfo.InvariantCulture, out var displayValue))
        {
            return false;
        }

        if (!allowNegative && displayValue < 0)
        {
            return false;
        }

        internalMillimeters = unit == LinearDisplayUnit.Meters
            ? displayValue * MillimetersPerMeter
            : displayValue;

        return true;
    }

    public static bool TryParsePositiveDistance(
        string text,
        LinearDisplayUnit unit,
        out double internalMillimeters)
        => TryParse(text, unit, allowNegative: false, allowEmpty: false, out internalMillimeters)
            && internalMillimeters > 0;

    public static bool TryParseSigned(
        string text,
        LinearDisplayUnit unit,
        out double internalMillimeters)
        => TryParse(text, unit, allowNegative: true, allowEmpty: true, out internalMillimeters);
}
