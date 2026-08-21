using LiteCad.Core.Document;
using LiteCad.Rendering;
using LiteCad.Services;

namespace LiteCad.Rendering.Pdf;

public static class PdfRenderer
{
    public static PdfRenderResult Render(
        PdfSheet sheet,
        LinearDisplayUnit linearUnit,
        Renderer renderer)
    {
        PdfFontResolver.EnsureRegistered();
        var layout = PdfExportLayout.Create(
            sheet.Document,
            sheet.Orientation,
            linearUnit,
            sheet.MarginMm);
        var visual = PdfExporter.BuildExportVisual(
            sheet,
            linearUnit,
            renderer,
            layout);
        return new PdfRenderResult(sheet, layout, visual);
    }

    public static void Save(PdfRenderResult result, LinearDisplayUnit linearUnit, string pdfPath)
    {
        PdfFontResolver.EnsureRegistered();
        PdfExporter.WriteVisualToPdf(
            result.Visual,
            result.Layout,
            result.Sheet,
            linearUnit,
            pdfPath);
    }
}
