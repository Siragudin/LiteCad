using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Services;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class PolygonRenderer
{
    public void Render(
        DrawingContext context,
        CadDocument document,
        Camera camera,
        bool forScreenDisplay = true,
        Size? viewport = null)
    {
        RenderSolidGeometry(context, document, camera, forScreenDisplay);
        RenderFaceHatches(context, document, camera, forScreenDisplay, viewport);
    }

    public void RenderSolidGeometry(
        DrawingContext context,
        CadDocument document,
        Camera camera,
        bool forScreenDisplay = true)
    {
        foreach (var polygon in document.Polygons)
        {
            if (polygon.OuterLoop.Edges.Count < 3)
            {
                continue;
            }

            var geometry = CreateGeometry(document, polygon);
            if (geometry is null)
            {
                continue;
            }

            if (polygon.Type == PolygonType.Face)
            {
                var fillStyle = FaceFillService.GetFill(document, polygon);
                if (fillStyle.FillPattern != FaceFillPattern.Solid)
                {
                    continue;
                }

                FaceFillRenderer.Render(
                    context,
                    document,
                    polygon,
                    geometry,
                    fillStyle,
                    camera.Zoom,
                    forScreenDisplay,
                    viewportWorldBounds: null);
                continue;
            }

            var (fill, stroke) = RenderStyles.ForPolygonType(polygon.Type, camera.Zoom);
            context.DrawGeometry(fill, stroke, geometry);
        }
    }

    public void RenderFaceHatches(
        DrawingContext context,
        CadDocument document,
        Camera camera,
        bool forScreenDisplay = true,
        Size? viewport = null)
    {
        var viewportWorldBounds = forScreenDisplay && viewport is Size viewportSize
            ? camera.GetWorldViewportBounds(viewportSize)
            : (Rect?)null;

        foreach (var polygon in document.Polygons)
        {
            if (polygon.Type != PolygonType.Face || polygon.OuterLoop.Edges.Count < 3)
            {
                continue;
            }

            var fillStyle = FaceFillService.GetFill(document, polygon);
            if (fillStyle.FillPattern == FaceFillPattern.Solid)
            {
                continue;
            }

            var geometry = CreateGeometry(document, polygon);
            if (geometry is null)
            {
                continue;
            }

            FaceFillRenderer.Render(
                context,
                document,
                polygon,
                geometry,
                fillStyle,
                camera.Zoom,
                forScreenDisplay,
                viewportWorldBounds);
        }
    }

    internal static Geometry? CreateGeometry(CadDocument document, Polygon polygon, double tolerance = MathUtils.DefaultTolerance)
    {
        var outer = PolygonGeometry.GetBoundaryPoints(document, polygon.OuterLoop);
        if (outer.Count < 3)
        {
            return null;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            AppendContour(context, outer);

            foreach (var hole in polygon.InnerLoops)
            {
                var holePoints = PolygonGeometry.GetBoundaryPoints(document, hole);
                if (holePoints.Count >= 3)
                {
                    AppendContour(context, holePoints);
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static void AppendContour(StreamGeometryContext context, IReadOnlyList<PointF> points)
    {
        var first = points[0];
        context.BeginFigure(new Point(first.X, first.Y), true, true);

        for (var i = 1; i < points.Count; i++)
        {
            var vertex = points[i];
            context.LineTo(new Point(vertex.X, vertex.Y), true, false);
        }
    }
}

