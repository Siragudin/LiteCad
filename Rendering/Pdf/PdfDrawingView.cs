using LiteCad.Core.Document;
using System.Windows;

namespace LiteCad.Rendering.Pdf;

public sealed class PdfDrawingView
{
    public const double MinScale = 0.05;

    public const double MaxScale = 50.0;

    private const double WheelZoomFactor = 1.12;

    public PdfDrawingView(CadDocument document)
    {
        Document = document;
    }

    public CadDocument Document { get; }

    public Point Position { get; set; }

    public double Scale { get; set; } = 1.0;

    public static double ClampScale(double scale)
        => Math.Clamp(scale, MinScale, MaxScale);

    public void ApplyWheelZoomAtContentPoint(Point contentPoint, int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return;
        }

        var factor = wheelDelta > 0 ? WheelZoomFactor : 1.0 / WheelZoomFactor;
        ApplyZoomAtContentPoint(contentPoint, Scale * factor);
    }

    public void ApplyZoomAtContentPoint(Point contentPoint, double newScale)
    {
        newScale = ClampScale(newScale);
        if (Math.Abs(newScale - Scale) < 1e-12)
        {
            return;
        }

        var scaleRatio = newScale / Scale;
        Position = new Point(
            contentPoint.X - (contentPoint.X - Position.X) * scaleRatio,
            contentPoint.Y - (contentPoint.Y - Position.Y) * scaleRatio);
        Scale = newScale;
    }

    public void ApplyDragDelta(Vector contentDelta)
    {
        Position = new Point(Position.X + contentDelta.X, Position.Y + contentDelta.Y);
    }
}
