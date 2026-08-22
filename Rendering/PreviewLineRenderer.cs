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
    public static Color GetPreviewColor(Color commitColor)
        => CanvasTheme.IsDark ? CanvasTheme.PreviewForeground : commitColor;

    public static Color GetAnnotationPreviewColor()
        => CanvasTheme.Preview;

    public static Pen CreatePen(
        double zoom,
        Color commitColor,
        double thickness = 1.5,
        DoubleCollection? dashArray = null)
        => RenderStyles.CreateScreenPen(
            CanvasTheme.CreateFrozenBrush(GetPreviewColor(commitColor)),
            thickness,
            zoom,
            dashArray);

    public static Pen CreateGhostPen(
        double zoom,
        double thickness = 1.5,
        DoubleCollection? dashArray = null)
        => RenderStyles.CreateScreenPen(
            CanvasTheme.CreateFrozenBrush(CanvasTheme.PreviewGhost),
            thickness,
            zoom,
            dashArray);

    public static Pen CreateAnnotationPen(
        double zoom,
        double thickness = 1.5,
        DoubleCollection? dashArray = null)
        => RenderStyles.CreateScreenPen(
            CanvasTheme.CreateFrozenBrush(GetAnnotationPreviewColor()),
            thickness,
            zoom,
            dashArray);

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
