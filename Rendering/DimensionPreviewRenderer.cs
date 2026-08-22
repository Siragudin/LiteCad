using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

/// <summary>
/// Lightweight dimension preview for tool overlay during offset placement.
/// Does not touch document, topology, or history.
/// </summary>
public sealed class DimensionPreviewRenderer
{
    private const double TextGapScreen = 12.0;

    private const double TickHalfLengthScreen = 5.0;

    private const double ExtensionThicknessScreen = 1.0;

    private const double DimensionLineThicknessScreen = 1.5;

    private const double ShortExtensionFraction = 0.25;

    private readonly DimensionFormattedTextCache _textCache = new();

    private double _cachedZoom = double.NaN;
    private Pen? _extensionPen;
    private Pen? _dimensionPen;

    internal DimensionFormattedTextCache TextCache => _textCache;

    public void Draw(
        DrawingContext context,
        DimensionLayout layout,
        double zoom,
        Color color,
        Camera camera,
        Size viewport,
        string distanceText,
        DimensionExtensionStyle extensionStyle,
        double textWorldHeight)
    {
        EnsurePens(zoom, color);

        DrawExtensionLine(context, _extensionPen!, layout.FirstAnchor, layout.FirstExtensionEnd, extensionStyle);
        DrawExtensionLine(context, _extensionPen!, layout.SecondAnchor, layout.SecondExtensionEnd, extensionStyle);
        context.DrawLine(
            _dimensionPen!,
            ToPoint(layout.DimensionLineStart),
            ToPoint(layout.DimensionLineEnd));
        DrawTick(context, layout.FirstExtensionEnd, layout.TextAngleRadians, zoom, _dimensionPen!);
        DrawTick(context, layout.SecondExtensionEnd, layout.TextAngleRadians, zoom, _dimensionPen!);
        DrawDistanceText(context, layout, distanceText, textWorldHeight, camera, viewport, zoom);
    }

    public void Invalidate()
    {
        _textCache.Invalidate();
        _cachedZoom = double.NaN;
        _extensionPen = null;
        _dimensionPen = null;
    }

    private void EnsurePens(double zoom, Color color)
    {
        if (Math.Abs(_cachedZoom - zoom) <= 1e-9 && _extensionPen is not null && _dimensionPen is not null)
        {
            return;
        }

        _cachedZoom = zoom;
        var brush = CanvasTheme.CreateFrozenBrush(color);
        _extensionPen = RenderStyles.CreateScreenPen(brush, ExtensionThicknessScreen, zoom);
        _dimensionPen = RenderStyles.CreateScreenPen(brush, DimensionLineThicknessScreen, zoom);
    }

    private void DrawDistanceText(
        DrawingContext context,
        DimensionLayout layout,
        string text,
        double textWorldHeight,
        Camera camera,
        Size viewport,
        double zoom)
    {
        textWorldHeight = Dimension.NormalizeTextSize(textWorldHeight);

        var startScreen = camera.WorldToScreen(layout.DimensionLineStart, viewport);
        var endScreen = camera.WorldToScreen(layout.DimensionLineEnd, viewport);

        var dx = endScreen.X - startScreen.X;
        var dy = endScreen.Y - startScreen.Y;

        var center = new Point(
            (startScreen.X + endScreen.X) * 0.5,
            (startScreen.Y + endScreen.Y) * 0.5);

        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length > 1e-9)
        {
            var nx = -dy / length;
            var ny = dx / length;
            if (ny > 0)
            {
                nx = -nx;
                ny = -ny;
            }

            center = new Point(
                center.X + nx * TextGapScreen,
                center.Y + ny * TextGapScreen);
        }

        var angle = GetReadableTextAngleRadians(dx, dy);
        var screenFontSize = DimensionAnnotationDrawing.GetTextScreenExtent(textWorldHeight, zoom);
        var pixelsPerDip = DimensionFormattedTextCache.ResolvePixelsPerDip();
        var formattedText = _textCache.GetOrCreate(
            text,
            screenFontSize,
            PreviewLineRenderer.GetAnnotationPreviewColor(),
            pixelsPerDip);

        context.PushTransform(new MatrixTransform(camera.GetScreenToWorldMatrix(viewport)));
        context.PushTransform(new TranslateTransform(center.X, center.Y));
        context.PushTransform(new RotateTransform(angle * 180.0 / Math.PI));
        context.DrawText(
            formattedText,
            new Point(
                -formattedText.Width * 0.5,
                -formattedText.Height * 0.5));
        context.Pop();
        context.Pop();
        context.Pop();
    }

    private static void DrawExtensionLine(
        DrawingContext context,
        Pen pen,
        PointF anchor,
        PointF extensionEnd,
        DimensionExtensionStyle extensionStyle)
    {
        var start = extensionStyle == DimensionExtensionStyle.Full
            ? anchor
            : GetShortExtensionStart(anchor, extensionEnd);
        context.DrawLine(pen, ToPoint(start), ToPoint(extensionEnd));
    }

    private static PointF GetShortExtensionStart(PointF anchor, PointF extensionEnd)
    {
        var dx = extensionEnd.X - anchor.X;
        var dy = extensionEnd.Y - anchor.Y;
        var fullLength = Math.Sqrt(dx * dx + dy * dy);
        if (fullLength <= 1e-9)
        {
            return extensionEnd;
        }

        return new PointF(
            (float)(extensionEnd.X - dx * ShortExtensionFraction),
            (float)(extensionEnd.Y - dy * ShortExtensionFraction));
    }

    private static void DrawTick(
        DrawingContext context,
        PointF center,
        double dimensionAngleRadians,
        double zoom,
        Pen pen)
    {
        var tickAngle = dimensionAngleRadians + Math.PI * 0.25;
        var halfLength = TickHalfLengthScreen / zoom;
        var dx = Math.Cos(tickAngle) * halfLength;
        var dy = Math.Sin(tickAngle) * halfLength;
        context.DrawLine(
            pen,
            new Point(center.X - dx, center.Y - dy),
            new Point(center.X + dx, center.Y + dy));
    }

    private static double GetReadableTextAngleRadians(double dx, double dy)
    {
        if (Math.Abs(dy) < Math.Abs(dx) * 0.01)
        {
            return 0.0;
        }

        if (Math.Abs(dx) < Math.Abs(dy) * 0.01)
        {
            return -Math.PI / 2;
        }

        var angle = Math.Atan2(dy, dx);
        if (angle > Math.PI / 2 || angle <= -Math.PI / 2)
        {
            angle += Math.PI;
        }

        return angle;
    }

    private static Point ToPoint(PointF point)
        => new(point.X, point.Y);
}
