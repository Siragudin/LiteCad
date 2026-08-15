using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class EdgeRenderer
{
    public void Render(DrawingContext context, CadDocument document, Camera camera)
    {
        foreach (var edge in document.Edges)
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);

            if (edge.IsAxis)
            {
                context.DrawLine(
                    RenderStyles.AxisEdgePen(camera.Zoom),
                    new Point(start.X, start.Y),
                    new Point(end.X, end.Y));
                continue;
            }

            var brush = new SolidColorBrush(edge.Color);
            var pen = RenderStyles.CreateScreenPen(brush, edge.Thickness, camera.Zoom, GetDashArray(edge.LineType));
            context.DrawLine(
                pen,
                new Point(start.X, start.Y),
                new Point(end.X, end.Y));
        }
    }

    private static DoubleCollection? GetDashArray(EdgeLineType lineType) => lineType switch
    {
        EdgeLineType.Dashed => [8, 4],
        EdgeLineType.Dotted => [2, 4],
        _ => null
    };
}
