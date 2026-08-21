using LiteCad.Core.Document;
using LiteCad.Services;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering.Pdf;

public static class PdfExporter
{
    public static PdfExportLayout CreateLayout(CadDocument document, LinearDisplayUnit linearUnit = LinearDisplayUnit.Millimeters)
        => PdfExportLayout.Create(document, linearUnit);

    public static void Export(
        CadDocument document,
        LinearDisplayUnit linearUnit,
        Renderer renderer,
        string pdfPath)
    {
        PdfFontResolver.EnsureRegistered();
        var layout = PdfExportLayout.Create(document, linearUnit);
        var visual = BuildExportVisual(document, linearUnit, renderer, layout);
        WriteVisualToPdf(visual, layout, document, linearUnit, pdfPath);
    }

    internal static DrawingVisual BuildExportVisual(
        CadDocument document,
        LinearDisplayUnit linearUnit,
        Renderer renderer,
        PdfExportLayout layout)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(
                Brushes.White,
                null,
                new Rect(0, 0, layout.PageWidthDip, layout.PageHeightDip));

            context.PushTransform(new TranslateTransform(layout.MarginDip, layout.MarginDip));
            try
            {
                if (layout.NeedsZoomCompensation)
                {
                    var center = new Point(layout.ContentSizeDip.Width / 2.0, layout.ContentSizeDip.Height / 2.0);
                    context.PushTransform(new ScaleTransform(
                        layout.ZoomCompensation,
                        layout.ZoomCompensation,
                        center.X,
                        center.Y));
                }

                try
                {
                    context.PushTransform(new MatrixTransform(
                        layout.ExportCamera.GetWorldToScreenMatrix(layout.ContentSizeDip)));
                    try
                    {
                        renderer.RenderForExportContent(
                            context,
                            document,
                            layout.ExportCamera,
                            layout.ContentSizeDip,
                            linearUnit);
                    }
                    finally
                    {
                        context.Pop();
                    }
                }
                finally
                {
                    if (layout.NeedsZoomCompensation)
                    {
                        context.Pop();
                    }
                }
            }
            finally
            {
                context.Pop();
            }
        }

        return visual;
    }

    private static void WriteVisualToPdf(
        DrawingVisual visual,
        PdfExportLayout layout,
        CadDocument document,
        LinearDisplayUnit linearUnit,
        string pdfPath)
    {
        var directory = Path.GetDirectoryName(pdfPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPdf = pdfPath + ".part";

        try
        {
            var pdfDocument = new PdfDocument();
            var page = pdfDocument.AddPage();
            page.Width = XUnit.FromPoint(layout.PageWidthPoints);
            page.Height = XUnit.FromPoint(layout.PageHeightPoints);

            using (var graphics = XGraphics.FromPdfPage(page))
            {
                WpfDrawingPdfConverter.Draw(visual, graphics, layout, document, linearUnit);
            }

            pdfDocument.Save(tempPdf);

            if (File.Exists(pdfPath))
            {
                File.Delete(pdfPath);
            }

            File.Move(tempPdf, pdfPath);
        }
        catch
        {
            if (File.Exists(tempPdf))
            {
                File.Delete(tempPdf);
            }

            if (File.Exists(pdfPath))
            {
                File.Delete(pdfPath);
            }

            throw;
        }
    }
}
