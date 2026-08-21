using LiteCad.Core.Geometry;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

/// <summary>
/// Lightweight preview line drawing for tool overlays.
/// Does not touch document, topology, or history.
/// </summary>
public static class PreviewLineRenderer
{
    public static void Draw(
        DrawingContext context,
        PointF start,
        PointF end,
        Pen pen)
    {
        context.DrawLine(
            pen,
            new Point(start.X, start.Y),
            new Point(end.X, end.Y));
    }
}
