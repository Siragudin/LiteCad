using LiteCad.Core.Geometry;
using System.Windows.Media;

namespace LiteCad.Rendering;

public static class SnapRenderer
{
    private static readonly Brush SnapBrush = CreateBrush(0x21, 0x96, 0xF3);

    public static void DrawSnapPoint(DrawingContext context, SnapPoint snap, double zoom)
    {
        var point = snap.Position;
        var radius = 4.0 / zoom;
        var pen = RenderStyles.CreateScreenPen(SnapBrush, 1.5, zoom);
        context.DrawEllipse(SnapBrush, pen, new System.Windows.Point(point.X, point.Y), radius, radius);
    }

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
