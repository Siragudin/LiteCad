using System.Globalization;

namespace LiteCad.Leaders;

public static class ElevationFormatting
{
    /// <summary>
    /// Model Y increases upward (camera uses Scale(zoom, -zoom)).
    /// A point 900 mm above the base therefore has a positive delta.
    /// </summary>
    public static double ComputeDelta(double baseModelY, double currentModelY)
        => currentModelY - baseModelY;

    public static string Format(double deltaMillimeters)
    {
        var rounded = Math.Round(deltaMillimeters, 0, MidpointRounding.AwayFromZero);
        if (Math.Abs(rounded) < 0.5)
        {
            return "0";
        }

        return rounded.ToString("+0;-0", CultureInfo.InvariantCulture);
    }
}
