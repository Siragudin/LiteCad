using LiteCad.Core.Document;
using System.Windows.Media;

namespace LiteCad.Rendering;

public static class RenderStyles
{
    public static Pen CreateScreenPen(Brush brush, double thickness, double zoom, DoubleCollection? dashArray = null)
    {
        var pen = new Pen(brush, thickness / zoom);
        if (dashArray is not null)
        {
            pen.DashStyle = new DashStyle(dashArray, 0);
        }

        pen.Freeze();
        return pen;
    }

    public static (Brush Fill, Pen? Stroke) ForPolygonType(PolygonType type, double zoom)
    {
        return type switch
        {
            PolygonType.Face => (
                new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)),
                null),
            PolygonType.Wall => (
                new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90)),
                CreateScreenPen(new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), 2.0, zoom)),
            PolygonType.Room => (
                new SolidColorBrush(Color.FromArgb(0x80, 0xA5, 0xD6, 0xA7)),
                CreateScreenPen(new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)), 1.5, zoom)),
            PolygonType.Axis => (
                Brushes.Transparent,
                CreateScreenPen(Brushes.Transparent, 1.0, zoom)),
            _ => (
                Brushes.Transparent,
                CreateScreenPen(Brushes.Black, 1.0, zoom))
        };
    }

    public static Pen EdgePen(double zoom)
        => CreateScreenPen(new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)), 1.5, zoom);

    public static Pen AxisLinePen(double zoom)
        => CreateScreenPen(new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0)), 1.5, zoom, [12, 4, 2, 4]);

    public static Pen AxisEdgePen(double zoom)
        => CreateScreenPen(new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)), 1.5, zoom, [6, 4]);
}
