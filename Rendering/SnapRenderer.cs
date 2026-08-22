using LiteCad.Core.Geometry;
using System.Windows.Media;

namespace LiteCad.Rendering;

public static class SnapRenderer
{
    public static void DrawSnapPoint(DrawingContext context, SnapPoint snap, double zoom)
    {
        var brush = CanvasTheme.CreateFrozenBrush(CanvasTheme.Snap);
        var point = snap.Position;
        var radius = 4.0 / zoom;
        var pen = RenderStyles.CreateScreenPen(brush, 1.5, zoom);
        context.DrawEllipse(brush, pen, new System.Windows.Point(point.X, point.Y), radius, radius);
    }
}
