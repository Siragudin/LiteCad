using LiteCad.Services;
using System.Globalization;

namespace LiteCad.UI;

public static class UnitDisplayFormatter
{
    private const double MillimetersPerMeter = 1000.0;
    private const double SquareMillimetersPerSquareMeter = 1_000_000.0;

    public static string FormatLinear(double internalMillimeters, LinearDisplayUnit unit)
        => unit == LinearDisplayUnit.Meters
            ? FormatTrimmedMeters(internalMillimeters)
            : FormatMillimeters(internalMillimeters);

    public static string FormatCoordinate(double internalMillimeters, LinearDisplayUnit unit)
        => FormatLinear(internalMillimeters, unit);

    public static string FormatArea(double internalSquareMillimeters)
    {
        var squareMeters = (decimal)internalSquareMillimeters / (decimal)SquareMillimetersPerSquareMeter;
        return $"{FormatTrimmedDecimal(squareMeters)} m²";
    }

    private static string FormatMillimeters(double internalMillimeters)
        => Math.Round(internalMillimeters, 0, MidpointRounding.AwayFromZero)
            .ToString(CultureInfo.InvariantCulture);

    private static string FormatTrimmedMeters(double internalMillimeters)
    {
        var meters = (decimal)internalMillimeters / (decimal)MillimetersPerMeter;
        return FormatTrimmedDecimal(meters);
    }

    private static string FormatTrimmedDecimal(decimal value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        var formatted = rounded.ToString("F2", CultureInfo.InvariantCulture);
        if (!formatted.Contains('.'))
        {
            return formatted;
        }

        return formatted.TrimEnd('0').TrimEnd('.');
    }
}
