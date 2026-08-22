using LiteCad.Core.Document;
using LiteCad.Rendering.Pdf;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class AxisRenderer
{
    private static readonly Color AxisLineColor = Color.FromRgb(0x15, 0x65, 0xC0);

    private static readonly DoubleCollection AxisLineDashArray = new() { 12, 4, 2, 4 };

    public void Render(DrawingContext context, CadDocument document, Camera camera, bool forScreenDisplay = true)
    {
        var pen = forScreenDisplay
            ? RenderStyles.AxisLinePen(camera.Zoom)
            : PdfExportPenFactory.Create(PenStyle.Axis, AxisLineColor, AxisLineDashArray);

        foreach (var axis in document.Axes)
        {
            context.DrawLine(
                pen,
                new Point(axis.Start.X, axis.Start.Y),
                new Point(axis.End.X, axis.End.Y));
        }
    }
}
