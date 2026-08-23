using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public static class FaceFillRenderer
{
    private const double DiagonalSpacingScreenPixels = 8.0;
    private const double DiagonalWideSpacingMultiplier = 3.0;
    private const double DiagonalLineWidthScreenPixels = 1.0;

    public static double GetHatchSpacingScreenPixels(FaceFillPattern pattern)
        => pattern == FaceFillPattern.DiagonalWide
            ? DiagonalSpacingScreenPixels * DiagonalWideSpacingMultiplier
            : DiagonalSpacingScreenPixels;

    public static void Render(
        DrawingContext context,
        CadDocument document,
        Polygon polygon,
        Geometry geometry,
        FaceFillStyle style,
        double zoom,
        bool forScreenDisplay = true)
    {
        var displayColor = DocumentDisplayColors.ResolveFillColor(style.FillColor, forScreenDisplay);
        if (style.FillPattern == FaceFillPattern.Solid)
        {
            var fill = new SolidColorBrush(displayColor);
            fill.Freeze();
            context.DrawGeometry(fill, null, geometry);
            return;
        }

        var bounds = GetBounds(document, polygon);
        if (bounds.IsEmpty)
        {
            return;
        }

        var pen = RenderStyles.CreateScreenPen(
            new SolidColorBrush(displayColor),
            DiagonalLineWidthScreenPixels,
            zoom);
        var spacing = GetHatchSpacingScreenPixels(style.FillPattern) / zoom;
        var span = bounds.Width + bounds.Height;

        context.PushClip(geometry);
        try
        {
            for (var offset = bounds.Left - span; offset <= bounds.Right + span; offset += spacing)
            {
                context.DrawLine(
                    pen,
                    new Point(offset, bounds.Bottom),
                    new Point(offset + bounds.Height, bounds.Top));
            }
        }
        finally
        {
            context.Pop();
        }
    }

    private static Rect GetBounds(CadDocument document, Polygon polygon)
    {
        var points = PolygonGeometry.GetBoundaryPoints(document, polygon.OuterLoop);
        if (points.Count == 0)
        {
            return Rect.Empty;
        }

        var minX = points[0].X;
        var maxX = points[0].X;
        var minY = points[0].Y;
        var maxY = points[0].Y;

        for (var i = 1; i < points.Count; i++)
        {
            var point = points[i];
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }
}
