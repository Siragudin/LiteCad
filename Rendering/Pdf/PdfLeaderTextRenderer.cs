using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Leaders;
using LiteCad.Services;
using PdfSharp.Drawing;
using System.Windows;

namespace LiteCad.Rendering.Pdf;

internal static class PdfLeaderTextRenderer
{
    public static void Draw(
        XGraphics graphics,
        CadDocument document,
        PdfExportLayout layout,
        PdfDrawingView view)
    {
        var camera = layout.ExportCamera;
        var viewport = layout.ContentSizeDip;

        foreach (var leader in document.Leaders)
        {
            if (string.IsNullOrWhiteSpace(leader.Text))
            {
                continue;
            }

            DrawText(graphics, layout, view, leader, camera, viewport);
        }
    }

    private static void DrawText(
        XGraphics graphics,
        PdfExportLayout layout,
        PdfDrawingView view,
        Leader leader,
        Camera camera,
        Size viewport)
    {
        var leaderLayout = LeaderGeometry.CreateLayout(leader.Target, leader.TextPosition, camera.Zoom);
        var elbowScreen = camera.WorldToScreen(leaderLayout.Elbow, viewport);
        var landingScreen = camera.WorldToScreen(leaderLayout.LandingEnd, viewport);
        var screen = new Point(
            (elbowScreen.X + landingScreen.X) * 0.5,
            (elbowScreen.Y + landingScreen.Y) * 0.5 - LeaderGeometry.TextGapAboveShelfScreen);

        var exportPoint = ToExportContentPoint(layout, view, screen);
        var fontSize = LeaderGeometry.TextHeightScreen * view.Scale;
        var font = new XFont("Segoe UI", fontSize, XFontStyleEx.Italic);
        var brush = new XSolidBrush(XColor.FromArgb(0x15, 0x65, 0xC0));
        var format = new XStringFormat
        {
            Alignment = XStringAlignment.Center,
            LineAlignment = XLineAlignment.Far
        };

        graphics.Save();
        graphics.TranslateTransform(exportPoint.X, exportPoint.Y);
        graphics.DrawString(leader.Text, font, brush, 0, 0, format);
        graphics.Restore();
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
