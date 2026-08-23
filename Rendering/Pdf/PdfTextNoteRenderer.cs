using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Services;
using LiteCad.Texts;
using PdfSharp.Drawing;
using System.Windows;

namespace LiteCad.Rendering.Pdf;

internal static class PdfTextNoteRenderer
{
    public static void Draw(
        XGraphics graphics,
        CadDocument document,
        PdfExportLayout layout,
        PdfDrawingView view)
    {
        var camera = layout.ExportCamera;
        var viewport = layout.ContentSizeDip;

        foreach (var note in document.Texts)
        {
            if (string.IsNullOrWhiteSpace(note.Text))
            {
                continue;
            }

            DrawText(graphics, layout, view, note, camera, viewport);
        }
    }

    private static void DrawText(
        XGraphics graphics,
        PdfExportLayout layout,
        PdfDrawingView view,
        TextNote note,
        Camera camera,
        Size viewport)
    {
        var screenFontSize = TextNote.NormalizeTextSize(note.TextSize) * view.Scale;
        var originScreen = camera.WorldToScreen(note.Origin, viewport);
        var lines = note.Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var font = new XFont("Segoe UI", screenFontSize, XFontStyleEx.Regular);
        var brush = new XSolidBrush(XColor.FromArgb(0x15, 0x65, 0xC0));
        var format = new XStringFormat
        {
            Alignment = XStringAlignment.Near,
            LineAlignment = XLineAlignment.Near
        };

        var totalHeight = screenFontSize * 1.2 * lines.Length;
        var start = new Point(
            originScreen.X,
            originScreen.Y - totalHeight - TextGeometry.TextGapAboveLineScreen * view.Scale);

        for (var index = 0; index < lines.Length; index++)
        {
            var linePoint = new Point(start.X, start.Y + index * screenFontSize * 1.2);
            var exportPoint = ToExportContentPoint(layout, view, linePoint);
            graphics.Save();
            graphics.TranslateTransform(exportPoint.X, exportPoint.Y);
            graphics.DrawString(lines[index], font, brush, 0, 0, format);
            graphics.Restore();
        }
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
