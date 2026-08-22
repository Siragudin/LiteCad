using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class GridRenderer
{
    public void Render(DrawingContext context, Camera camera, Size viewport)
    {
        var spacing = GetGridSpacing(camera.Zoom);
        var majorSpacing = spacing * 5;

        var topLeft = camera.ScreenToWorld(new Point(0, 0), viewport);
        var bottomRight = camera.ScreenToWorld(new Point(viewport.Width, viewport.Height), viewport);

        var minX = Math.Floor(Math.Min(topLeft.X, bottomRight.X) / spacing) * spacing;
        var maxX = Math.Ceiling(Math.Max(topLeft.X, bottomRight.X) / spacing) * spacing;
        var minY = Math.Floor(Math.Min(topLeft.Y, bottomRight.Y) / spacing) * spacing;
        var maxY = Math.Ceiling(Math.Max(topLeft.Y, bottomRight.Y) / spacing) * spacing;

        var minorPen = RenderStyles.CreateScreenPen(CanvasTheme.CreateFrozenBrush(CanvasTheme.GridMinor), 1.0, camera.Zoom);
        var majorPen = RenderStyles.CreateScreenPen(CanvasTheme.CreateFrozenBrush(CanvasTheme.GridMajor), 1.0, camera.Zoom);
        var originPen = RenderStyles.CreateScreenPen(CanvasTheme.CreateFrozenBrush(CanvasTheme.GridOrigin), 1.5, camera.Zoom);

        for (var x = minX; x <= maxX + spacing * 0.5; x += spacing)
        {
            var pen = IsMultipleOf(x, majorSpacing) ? majorPen : minorPen;
            context.DrawLine(pen, new Point(x, minY), new Point(x, maxY));
        }

        for (var y = minY; y <= maxY + spacing * 0.5; y += spacing)
        {
            var pen = IsMultipleOf(y, majorSpacing) ? majorPen : minorPen;
            context.DrawLine(pen, new Point(minX, y), new Point(maxX, y));
        }

        if (minX <= 0 && maxX >= 0)
        {
            context.DrawLine(originPen, new Point(0, minY), new Point(0, maxY));
        }

        if (minY <= 0 && maxY >= 0)
        {
            context.DrawLine(originPen, new Point(minX, 0), new Point(maxX, 0));
        }
    }

    private static double GetGridSpacing(double zoom)
    {
        const double targetPixels = 50;
        var worldSpacing = targetPixels / zoom;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(worldSpacing)));
        var normalized = worldSpacing / magnitude;

        if (normalized < 1.5)
        {
            return magnitude;
        }

        if (normalized < 3.5)
        {
            return 2 * magnitude;
        }

        if (normalized < 7.5)
        {
            return 5 * magnitude;
        }

        return 10 * magnitude;
    }

    private static bool IsMultipleOf(double value, double step)
    {
        if (step <= 0)
        {
            return false;
        }

        var ratio = value / step;
        return Math.Abs(ratio - Math.Round(ratio)) < 1e-6;
    }
}
