using System.Windows.Media;

namespace LiteCad.Rendering.Pdf;

public sealed class PdfRenderResult
{
    public PdfRenderResult(PdfSheet sheet, PdfExportLayout layout, DrawingVisual visual)
    {
        Sheet = sheet;
        Layout = layout;
        Visual = visual;
    }

    public PdfSheet Sheet { get; }

    public PdfExportLayout Layout { get; }

    public DrawingVisual Visual { get; }
}
