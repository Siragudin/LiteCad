using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class SelectionRenderer
{
    public void Render(DrawingContext context, CadDocument document, Selection selection, Camera camera)
    {
        var fill = CanvasTheme.CreateFrozenBrush(CanvasTheme.SelectionFill);
        var stroke = RenderStyles.CreateScreenPen(
            CanvasTheme.CreateFrozenBrush(CanvasTheme.SelectionStroke),
            2.0,
            camera.Zoom);

        foreach (var polygon in document.Polygons)
        {
            if (!selection.SelectedPolygonIds.Contains(polygon.Id) || polygon.OuterLoop.Edges.Count < 3)
            {
                continue;
            }

            var geometry = PolygonRenderer.CreateGeometry(document, polygon);
            if (geometry is null)
            {
                continue;
            }

            context.DrawGeometry(fill, stroke, geometry);
        }

        foreach (var edge in document.Edges)
        {
            if (!selection.SelectedEdgeIds.Contains(edge.Id))
            {
                continue;
            }

            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            context.DrawLine(
                stroke,
                new Point(start.X, start.Y),
                new Point(end.X, end.Y));
        }

        foreach (var axis in document.Axes)
        {
            if (!selection.SelectedAxisIds.Contains(axis.Id))
            {
                continue;
            }

            context.DrawLine(
                stroke,
                new Point(axis.Start.X, axis.Start.Y),
                new Point(axis.End.X, axis.End.Y));
        }
    }
}
