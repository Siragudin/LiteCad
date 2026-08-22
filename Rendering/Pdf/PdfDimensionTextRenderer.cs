using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Services;
using LiteCad.UI;
using PdfSharp.Drawing;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering.Pdf;

internal static class PdfDimensionTextRenderer
{
    public static void Draw(
        XGraphics graphics,
        CadDocument document,
        PdfExportLayout layout,
        PdfDrawingView view,
        LinearDisplayUnit linearUnit)
    {
        var camera = layout.ExportCamera;
        var viewport = layout.ContentSizeDip;

        foreach (var dimension in document.Dimensions)
        {
            if (!DimensionGeometry.TryGetAnchorPoints(document, dimension, out var firstAnchor, out var secondAnchor)
                || !DimensionGeometry.TryCreateLayout(
                    firstAnchor,
                    secondAnchor,
                    dimension,
                    TopologyTolerance.ForMutation,
                    out var dimensionLayout))
            {
                continue;
            }

            var distanceText = UnitDisplayFormatter.FormatLinear(dimensionLayout.MeasuredDistance, linearUnit);
            DrawDistanceText(graphics, layout, view, dimensionLayout, distanceText, dimension, camera, viewport);
        }
    }

    private static void DrawDistanceText(
        XGraphics graphics,
        PdfExportLayout layout,
        PdfDrawingView view,
        DimensionLayout dimensionLayout,
        string text,
        Dimension dimension,
        Camera camera,
        Size viewport)
    {
        var startScreen = camera.WorldToScreen(dimensionLayout.DimensionLineStart, viewport);
        var endScreen = camera.WorldToScreen(dimensionLayout.DimensionLineEnd, viewport);
        var dx = endScreen.X - startScreen.X;
        var dy = endScreen.Y - startScreen.Y;

        var center = new Point(
            (startScreen.X + endScreen.X) * 0.5,
            (startScreen.Y + endScreen.Y) * 0.5);

        const double dipToPoint = PdfExportLayout.PointsPerInch / PdfExportLayout.DipPerInch;
        var annotativeSize = PdfAnnotationTable.TextGapPoints / dipToPoint * view.Scale;

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
                center.X + nx * annotativeSize,
                center.Y + ny * annotativeSize);
        }

        var angleRadians = GetTextAngleRadians(dx, dy);
        var exportPoint = ToExportContentPoint(layout, view, center);
        var fontSize = PdfAnnotationTable.DimensionTextHeightPoints / dipToPoint * view.Scale;
        var font = new XFont("Segoe UI", fontSize, XFontStyleEx.Regular);
        var brush = new XSolidBrush(XColor.FromArgb(0x15, 0x65, 0xC0));
        var format = new XStringFormat
        {
            Alignment = XStringAlignment.Center,
            LineAlignment = XLineAlignment.Center
        };

        graphics.Save();
        graphics.TranslateTransform(exportPoint.X, exportPoint.Y);
        graphics.RotateTransform(angleRadians * 180.0 / Math.PI);
        graphics.DrawString(text, font, brush, 0, 0, format);
        graphics.Restore();
    }

    private static double GetTextAngleRadians(double dx, double dy)
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

    private static Point ToExportContentPoint(PdfExportLayout layout, PdfDrawingView view, Point contentPoint)
    {
        if (layout.NeedsZoomCompensation)
        {
            var centerX = layout.ContentSizeDip.Width / 2.0;
            var centerY = layout.ContentSizeDip.Height / 2.0;
            contentPoint = new Point(
                centerX + (contentPoint.X - centerX) * layout.ZoomCompensation,
                centerY + (contentPoint.Y - centerY) * layout.ZoomCompensation);
        }

        return PdfViewTransform.ToPageDip(layout, view, contentPoint);
    }
}
