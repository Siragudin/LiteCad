using LiteCad.Core.Document;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class AxisRenderer
{
    public void Render(DrawingContext context, CadDocument document, Camera camera, bool forScreenDisplay = true)
    {
        var pen = RenderStyles.AxisLinePen(camera.Zoom);

        foreach (var axis in document.Axes)
        {
            context.DrawLine(
                pen,
                new Point(axis.Start.X, axis.Start.Y),
                new Point(axis.End.X, axis.End.Y));
        }
    }
}
