using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering.Pdf;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class EdgeRenderer
{
    private static readonly Color AxisEdgeColor = Color.FromRgb(0xE5, 0x39, 0x35);

    private static readonly DoubleCollection AxisEdgeDashArray = new() { 6, 4 };

    public void Render(DrawingContext context, CadDocument document, Camera camera, bool forScreenDisplay = true)
    {
        foreach (var edge in document.Edges)
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);

            if (edge.IsAxis)
            {
                var axisPen = forScreenDisplay
                    ? RenderStyles.AxisEdgePen(camera.Zoom)
                    : PdfExportPenFactory.Create(PenStyle.AxisEdge, AxisEdgeColor, AxisEdgeDashArray);
                context.DrawLine(
                    axisPen,
                    new Point(start.X, start.Y),
                    new Point(end.X, end.Y));
                continue;
            }

            var displayColor = DocumentDisplayColors.ResolveEdgeColor(edge.Color, forScreenDisplay);
            var pen = forScreenDisplay
                ? RenderStyles.CreateScreenPen(
                    new SolidColorBrush(displayColor),
                    edge.Thickness,
                    camera.Zoom,
                    GetDashArray(edge.LineType))
                : PdfExportPenFactory.Create(
                    GetExportPenStyle(edge.LineType),
                    displayColor,
                    GetDashArray(edge.LineType));

            context.DrawLine(
                pen,
                new Point(start.X, start.Y),
                new Point(end.X, end.Y));
        }
    }

    private static DoubleCollection? GetDashArray(EdgeLineType lineType) => lineType switch
    {
        EdgeLineType.Dashed => new DoubleCollection { 8, 4 },
        EdgeLineType.Dotted => new DoubleCollection { 2, 4 },
        _ => null
    };

    private static PenStyle GetExportPenStyle(EdgeLineType lineType) => lineType switch
    {
        EdgeLineType.Dashed => PenStyle.EdgeDashed,
        EdgeLineType.Dotted => PenStyle.EdgeDotted,
        _ => PenStyle.Edge
    };
}
