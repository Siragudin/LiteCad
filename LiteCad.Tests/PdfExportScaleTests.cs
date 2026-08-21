using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Rendering;
using LiteCad.Rendering.Pdf;
using LiteCad.Services;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class PdfExportScaleTests
{
    private const double Tolerance = 1e-4;

    [Fact]
    public void PdfExportLayout_SmallObject_FitsPrintableArea()
        => AssertFitsPrintableArea(CreateRectangleDocument(0, 0, 100, 100));

    [Fact]
    public void PdfExportLayout_WideObject_FitsPrintableArea()
        => AssertFitsPrintableArea(CreateRectangleDocument(0, 0, 10_000, 1_000));

    [Fact]
    public void PdfExportLayout_TallObject_FitsPrintableArea()
        => AssertFitsPrintableArea(CreateRectangleDocument(0, 0, 1_000, 10_000));

    [Fact]
    public void PdfExportLayout_HugeObject_FitsPrintableArea()
        => AssertFitsPrintableArea(CreateRectangleDocument(0, 0, 100_000, 100_000));

    [Fact]
    public void PdfExportLayout_HugeObject_UsesZoomCompensation()
    {
        var layout = PdfExportLayout.Create(CreateRectangleDocument(0, 0, 100_000, 100_000));

        Assert.True(layout.RequiredZoom < Camera.MinZoom);
        Assert.True(layout.NeedsZoomCompensation);
        Assert.True(layout.ZoomCompensation < 1.0);
        AssertFitsPrintableArea(layout);
    }

    [Fact]
    public void PdfExportLayout_ObjectWithDimension_FitsPrintableArea()
    {
        var document = CreateRectangleDocument(0, 0, 5_000, 500);
        var first = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tolerance));
        var second = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(5_000, 0), Tolerance));
        DimensionService.Create(document, first.Id, second.Id, 250, Tolerance);

        AssertFitsPrintableArea(document);
    }

    [Fact]
    public void PdfExportLayout_ObjectWithDiagonalFill_FitsPrintableArea()
    {
        var document = CreateRectangleDocument(0, 0, 8_000, 4_000);
        var face = document.Polygons.First(polygon => polygon.Type == PolygonType.Face);
        Assert.True(FaceFillService.TrySetFillPattern(document, face, FaceFillPattern.Diagonal));
        Assert.True(FaceFillService.TrySetFillColor(document, face, Colors.SteelBlue));

        AssertFitsPrintableArea(document);
    }

    [Fact]
    public void PdfExportLayout_ObjectFarFromOrigin_FitsPrintableArea()
        => AssertFitsPrintableArea(CreateRectangleDocument(1_000_000, 1_000_000, 2_000, 1_500));

    [Fact]
    public void PdfExportLayout_RequiredZoomUsesUniformScale()
    {
        var layout = PdfExportLayout.Create(CreateRectangleDocument(0, 0, 10_000, 1_000));
        var expected = PdfExportLayout.ComputeRequiredZoom(layout.WorldBounds, layout.ContentSizeDip);

        Assert.Equal(expected, layout.RequiredZoom, 12);
    }

    [Fact]
    public void PdfExport_ExportsHugeDrawingWithoutException()
    {
        var pdfPath = Path.Combine(Path.GetTempPath(), "LiteCadPdfScaleTests", Guid.NewGuid().ToString("N") + ".pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(pdfPath)!);

        WpfTestUtilities.RunSta(() =>
        {
            PdfExporter.Export(
                CreateRectangleDocument(0, 0, 100_000, 100_000),
                LinearDisplayUnit.Millimeters,
                new Renderer(),
                pdfPath);
        });

        Assert.True(File.Exists(pdfPath));
        Assert.True(new FileInfo(pdfPath).Length > 256);
    }

    private static void AssertFitsPrintableArea(CadDocument document)
    {
        var layout = PdfExportLayout.Create(document);
        AssertFitsPrintableArea(layout);
    }

    private static void AssertFitsPrintableArea(PdfExportLayout layout)
    {
        var transformed = layout.GetTransformedBoundsInContentDip();

        Assert.True(transformed.Left >= -1.0, $"Left overflow: {transformed.Left}");
        Assert.True(transformed.Top >= -1.0, $"Top overflow: {transformed.Top}");
        Assert.True(transformed.Right <= layout.ContentSizeDip.Width + 1.0, $"Right overflow: {transformed.Right}");
        Assert.True(transformed.Bottom <= layout.ContentSizeDip.Height + 1.0, $"Bottom overflow: {transformed.Bottom}");
    }

    private static CadDocument CreateRectangleDocument(double x, double y, double width, double height)
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(x, y), new PointF(x + width, y), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(x + width, y), new PointF(x + width, y + height), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(x + width, y + height), new PointF(x, y + height), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(x, y + height), new PointF(x, y), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }
}
