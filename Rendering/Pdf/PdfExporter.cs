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
        var sheet = new PdfSheet(document);
        var result = PdfRenderer.Render(sheet, linearUnit, renderer);
        PdfRenderer.Save(result, linearUnit, pdfPath);
    }

    internal static DrawingVisual BuildExportVisual(
        PdfSheet sheet,
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

            context.PushTransform(new MatrixTransform(PdfSheetTransform.GetWorldToPageDipMatrix(sheet, layout)));
            try
            {
                renderer.RenderForExportContent(
                    context,
                    sheet.Document,
                    layout.ExportCamera,
                    layout.ContentSizeDip,
                    linearUnit);
            }
            finally
            {
                context.Pop();
            }
        }

        return visual;
    }

    internal static void WriteVisualToPdf(
        DrawingVisual visual,
        PdfExportLayout layout,
        PdfSheet sheet,
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
                WpfDrawingPdfConverter.Draw(visual, graphics, layout, sheet, linearUnit);
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
