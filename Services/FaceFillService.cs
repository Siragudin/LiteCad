using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using System.Windows.Media;

namespace LiteCad.Services;

public static class FaceFillService
{
    public static FaceFillStyle GetFill(CadDocument document, Polygon polygon)
    {
        if (polygon.Type != PolygonType.Face)
        {
            return FaceFillStyle.Default.Clone();
        }

        var key = FaceIdentity.Create(document, polygon);
        if (string.IsNullOrEmpty(key))
        {
            return FaceFillStyle.Default.Clone();
        }

        return document.FaceFillStyles.TryGetValue(key, out var style)
            ? style.Clone()
            : FaceFillStyle.Default.Clone();
    }

    public static bool TrySetFillColor(CadDocument document, Polygon polygon, Color fillColor)
    {
        if (!TryGetFaceKey(document, polygon, out var key))
        {
            return false;
        }

        var style = GetOrCreateStyle(document, key);
        if (style.FillColor == fillColor)
        {
            return true;
        }

        style.FillColor = fillColor;
        document.FaceFillStyles[key] = style;
        return true;
    }

    public static bool TrySetFillPattern(CadDocument document, Polygon polygon, FaceFillPattern fillPattern)
    {
        if (!TryGetFaceKey(document, polygon, out var key))
        {
            return false;
        }

        var style = GetOrCreateStyle(document, key);
        if (style.FillPattern == fillPattern)
        {
            return true;
        }

        style.FillPattern = fillPattern;
        document.FaceFillStyles[key] = style;
        return true;
    }

    public static bool TryApplyFill(CadDocument document, Polygon polygon, FaceFillStyle style)
    {
        if (!TryGetFaceKey(document, polygon, out var key))
        {
            return false;
        }

        document.FaceFillStyles[key] = style.Clone();
        return true;
    }

    public static void RemoveFill(CadDocument document, Polygon polygon)
    {
        if (!TryGetFaceKey(document, polygon, out var key))
        {
            return;
        }

        document.FaceFillStyles.Remove(key);
    }

    public static Color ParsePaletteTag(string? tag)
        => tag switch
        {
            "LightGray" => Color.FromRgb(0xF0, 0xF0, 0xF0),
            "Gray" => Color.FromRgb(0x66, 0x66, 0x66),
            "Blue" => Colors.Blue,
            "Red" => Colors.Red,
            "Green" => Colors.Green,
            _ => Color.FromRgb(0x22, 0x22, 0x22)
        };

    public static FaceFillPattern ParsePatternTag(string? tag)
        => tag switch
        {
            "DiagonalWide" => FaceFillPattern.DiagonalWide,
            "Diagonal" => FaceFillPattern.Diagonal,
            _ => FaceFillPattern.Solid
        };

    public static string GetPatternTag(FaceFillPattern pattern)
        => pattern switch
        {
            FaceFillPattern.DiagonalWide => "DiagonalWide",
            FaceFillPattern.Diagonal => "Diagonal",
            _ => "Solid"
        };

    public static string? GetPaletteTag(Color color)
    {
        if (color == Color.FromRgb(0x66, 0x66, 0x66))
        {
            return "Gray";
        }

        if (color == Colors.Blue)
        {
            return "Blue";
        }

        if (color == Colors.Red)
        {
            return "Red";
        }

        if (color == Colors.Green)
        {
            return "Green";
        }

        if (color == Color.FromRgb(0x22, 0x22, 0x22))
        {
            return "Black";
        }

        if (color == FaceFillStyle.Default.FillColor)
        {
            return "LightGray";
        }

        return null;
    }

    private static FaceFillStyle GetOrCreateStyle(CadDocument document, string key)
        => document.FaceFillStyles.TryGetValue(key, out var style)
            ? style.Clone()
            : FaceFillStyle.Default.Clone();

    private static bool TryGetFaceKey(CadDocument document, Polygon polygon, out string key)
    {
        key = string.Empty;
        if (polygon.Type != PolygonType.Face)
        {
            return false;
        }

        key = FaceIdentity.Create(document, polygon);
        return !string.IsNullOrEmpty(key);
    }
}
