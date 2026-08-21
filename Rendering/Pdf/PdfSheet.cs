using LiteCad.Core.Document;

namespace LiteCad.Rendering.Pdf;

public sealed class PdfSheet
{
    public const double DefaultMarginMm = 10.0;

    public PdfSheet(CadDocument document)
    {
        Document = document;
        Views = new[] { new PdfDrawingView(document) };
    }

    public PdfPaperFormat Format { get; init; } = PdfPaperFormat.A4;

    public PdfPageOrientation Orientation { get; set; } = PdfPageOrientation.Portrait;

    public double MarginMm { get; init; } = DefaultMarginMm;

    public CadDocument Document { get; }

    public IReadOnlyList<PdfDrawingView> Views { get; }

    public PdfDrawingView PrimaryView => Views[0];
}
