using LiteCad.Core.Geometry;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class Camera
{
    public const double MinZoom = 0.016;
    public const double MaxZoom = 50.0;

    public PointF PanOffset { get; private set; } = PointF.Zero;

    public double Zoom { get; private set; } = 1.0;

    public event Action? Changed;

    public Matrix GetWorldToScreenMatrix(Size viewport)
    {
        var centerX = viewport.Width / 2.0;
        var centerY = viewport.Height / 2.0;

        var matrix = Matrix.Identity;
        matrix.Scale(Zoom, -Zoom);
        matrix.Translate(centerX + PanOffset.X, centerY + PanOffset.Y);
        return matrix;
    }

    public Matrix GetScreenToWorldMatrix(Size viewport)
    {
        var matrix = GetWorldToScreenMatrix(viewport);
        matrix.Invert();
        return matrix;
    }

    public PointF ScreenToWorld(Point screen, Size viewport)
    {
        var world = GetScreenToWorldMatrix(viewport).Transform(screen);
        return new PointF(world.X, world.Y);
    }

    public Point WorldToScreen(PointF world, Size viewport)
    {
        return GetWorldToScreenMatrix(viewport).Transform(new Point(world.X, world.Y));
    }

    public void PanScreen(double deltaX, double deltaY)
    {
        PanOffset = new PointF(PanOffset.X + deltaX, PanOffset.Y + deltaY);
        Changed?.Invoke();
    }

    public void ZoomAt(Point screen, double factor, Size viewport)
    {
        var worldBefore = ScreenToWorld(screen, viewport);
        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);

        var screenAfter = WorldToScreen(worldBefore, viewport);
        PanOffset = new PointF(
            PanOffset.X + screen.X - screenAfter.X,
            PanOffset.Y + screen.Y - screenAfter.Y);

        Changed?.Invoke();
    }

    public void Reset()
    {
        PanOffset = PointF.Zero;
        Zoom = 1.0;
        Changed?.Invoke();
    }

    public void FitWorldBounds(
        double minX,
        double minY,
        double maxX,
        double maxY,
        Size viewport,
        double paddingPixels = 24)
    {
        var worldWidth = Math.Max(maxX - minX, 1e-6);
        var worldHeight = Math.Max(maxY - minY, 1e-6);
        var zoomX = (viewport.Width - paddingPixels * 2) / worldWidth;
        var zoomY = (viewport.Height - paddingPixels * 2) / worldHeight;
        Zoom = Math.Clamp(Math.Min(zoomX, zoomY), MinZoom, MaxZoom);

        var centerX = (minX + maxX) / 2.0;
        var centerY = (minY + maxY) / 2.0;
        PanOffset = new PointF((float)(-centerX * Zoom), (float)(centerY * Zoom));
        Changed?.Invoke();
    }
}
