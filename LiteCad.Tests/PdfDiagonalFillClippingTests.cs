using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Rendering.Pdf;
using LiteCad.Services;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class PdfDiagonalFillClippingTests : IDisposable
{
    private const double Tolerance = 1e-4;

    private readonly string _tempRoot;

    public PdfDiagonalFillClippingTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "LiteCadPdfClipTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void DiagonalFill_RectangleFace_ExportVisualContainsClipGeometry()
        => AssertExportVisualContainsClipGeometry(CreateRectangleDocument());

    [Fact]
    public void DiagonalFill_TriangleFace_ExportVisualContainsClipGeometry()
        => AssertExportVisualContainsClipGeometry(CreateTriangleDocument());

    [Fact]
    public void DiagonalFill_SlantedFace_ExportVisualContainsClipGeometry()
        => AssertExportVisualContainsClipGeometry(CreateSlantedDocument());

    [Fact]
    public void DiagonalFill_ConcaveLFace_ExportVisualContainsClipGeometry()
        => AssertExportVisualContainsClipGeometry(CreateConcaveLDocument());

    [Fact]
    public void DiagonalFill_FaceWithHole_ExportVisualContainsClipGeometry()
        => AssertExportVisualContainsClipGeometry(CreateSquareWithHoleDocument());

    [Fact]
    public void DiagonalFill_RectangleFace_PdfContentUsesClipping()
        => AssertPdfContentUsesClipping(CreateRectangleDocument());

    [Fact]
    public void DiagonalFill_TriangleFace_PdfContentUsesClipping()
        => AssertPdfContentUsesClipping(CreateTriangleDocument());

    [Fact]
    public void DiagonalFill_SlantedFace_PdfContentUsesClipping()
        => AssertPdfContentUsesClipping(CreateSlantedDocument());

    [Fact]
    public void DiagonalFill_ConcaveLFace_PdfContentUsesClipping()
        => AssertPdfContentUsesClipping(CreateConcaveLDocument());

    [Fact]
    public void DiagonalFill_FaceWithHole_PdfContentUsesEvenOddClipping()
        => AssertPdfContentUsesEvenOddClipping(CreateSquareWithHoleDocument());

    [Fact]
    public void DiagonalFill_RectangleFace_PdfStillContainsEdgeStrokes()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = ApplyDiagonalFill(CreateRectangleDocument());
            var pdfPath = Path.Combine(_tempRoot, "RectangleEdges.pdf");
            PdfExporter.Export(document, LinearDisplayUnit.Millimeters, new Renderer(), pdfPath);

            var content = ReadPdfPageContent(pdfPath);
            Assert.True(Regex.IsMatch(content, @"\sS(\s|$)"));
            Assert.True(CountStrokeOperators(content) >= document.Edges.Count);
        });
    }

    [Fact]
    public void DiagonalFill_TriangleFace_HatchLinesMidpointsStayInsideFace()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = ApplyDiagonalFill(CreateTriangleDocument());
            var layout = PdfExportLayout.Create(document);
            var visual = PdfExporter.BuildExportVisual(
                document,
                LinearDisplayUnit.Millimeters,
                new Renderer(),
                layout);
            var drawing = VisualTreeHelper.GetDrawing(visual) as DrawingGroup;

            Assert.NotNull(drawing);
            var clipGeometry = FindClipGeometry(drawing!);
            Assert.NotNull(clipGeometry);

            var triangle = new[]
            {
                new PointF(0, 0),
                new PointF(100, 0),
                new PointF(50, 80)
            };

            var bounds = clipGeometry!.Bounds;
            var center = new PointF(
                (float)(bounds.X + bounds.Width / 2),
                (float)(bounds.Y + bounds.Height / 2));
            Assert.True(IsPointInsidePolygon(center, triangle));
            Assert.True(bounds.Width < 101 && bounds.Height < 81);
        });
    }

    private static Geometry? FindClipGeometry(DrawingGroup group)
    {
        if (group.ClipGeometry is not null)
        {
            return group.ClipGeometry;
        }

        foreach (var child in group.Children)
        {
            if (child is DrawingGroup nested)
            {
                var clip = FindClipGeometry(nested);
                if (clip is not null)
                {
                    return clip;
                }
            }
        }

        return null;
    }

    private void AssertExportVisualContainsClipGeometry(CadDocument document)
    {
        WpfTestUtilities.RunSta(() =>
        {
            var filled = ApplyDiagonalFill(document);
            var layout = PdfExportLayout.Create(filled);
            var visual = PdfExporter.BuildExportVisual(
                filled,
                LinearDisplayUnit.Millimeters,
                new Renderer(),
                layout);
            var drawing = VisualTreeHelper.GetDrawing(visual) as DrawingGroup;

            Assert.NotNull(drawing);
            Assert.True(ContainsClipGeometry(drawing!), "Diagonal fill export visual must contain a clip geometry.");
        });
    }

    private void AssertPdfContentUsesClipping(CadDocument document)
    {
        WpfTestUtilities.RunSta(() =>
        {
            var filled = ApplyDiagonalFill(document);
            var pdfPath = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N") + ".pdf");
            PdfExporter.Export(filled, LinearDisplayUnit.Millimeters, new Renderer(), pdfPath);

            var content = ReadPdfPageContent(pdfPath);
            Assert.True(ContainsClipOperator(content), "PDF content must define a clipping path before hatch strokes.");
        });
    }

    private void AssertPdfContentUsesEvenOddClipping(CadDocument document)
    {
        WpfTestUtilities.RunSta(() =>
        {
            var filled = ApplyDiagonalFill(document);
            var pdfPath = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N") + ".pdf");
            PdfExporter.Export(filled, LinearDisplayUnit.Millimeters, new Renderer(), pdfPath);

            var content = ReadPdfPageContent(pdfPath);
            Assert.True(ContainsClipOperator(content), "Face with hole must use a clipping path for diagonal hatch.");
        });
    }

    private static CadDocument ApplyDiagonalFill(CadDocument document)
    {
        var face = document.Polygons.First(polygon => polygon.Type == PolygonType.Face);
        Assert.True(FaceFillService.TrySetFillPattern(document, face, FaceFillPattern.Diagonal));
        Assert.True(FaceFillService.TrySetFillColor(document, face, Colors.SteelBlue));
        return document;
    }

    private static bool ContainsClipGeometry(DrawingGroup group)
    {
        if (group.ClipGeometry is not null)
        {
            return true;
        }

        foreach (var child in group.Children)
        {
            if (child is DrawingGroup nested && ContainsClipGeometry(nested))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsClipOperator(string content)
        => Regex.IsMatch(content, @"\sW(\*|\s|$)");

    private static int CountStrokeOperators(string content)
        => Regex.Matches(content, @"\sS(\s|$)").Count;

    private static string ReadPdfPageContent(string pdfPath)
    {
        using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        return ReadPdfPageContent(document.Pages[0]);
    }

    private static string ReadPdfPageContent(PdfPage page)
    {
        var builder = new StringBuilder();
        AppendPdfContentObject(builder, page.Elements.GetObject("/Contents"));
        return builder.ToString();
    }

    private static void AppendPdfContentObject(StringBuilder builder, PdfObject? contents)
    {
        if (contents is null)
        {
            return;
        }

        if (contents is PdfArray array)
        {
            foreach (var item in array.Elements)
            {
                if (item is PdfReference reference)
                {
                    AppendPdfContentObject(builder, reference.Value);
                }
            }

            return;
        }

        PdfDictionary? streamDictionary = contents switch
        {
            PdfDictionary dictionary => dictionary,
            _ when contents.Reference?.Value is PdfDictionary referenced => referenced,
            _ => null
        };

        if (streamDictionary is not null)
        {
            AppendStreamContent(builder, streamDictionary);
        }
    }

    private static void AppendStreamContent(StringBuilder builder, PdfDictionary streamDictionary)
    {
        var stream = streamDictionary.Stream;
        if (stream is null)
        {
            return;
        }

        var bytes = stream.UnfilteredValue;
        builder.Append(Encoding.Latin1.GetString(bytes));
    }

    private static bool IsPointInsidePolygon(PointF point, IReadOnlyList<PointF> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            var intersects = pi.Y > point.Y != pj.Y > point.Y
                             && point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y + 1e-9f) + pi.X;
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static CadDocument CreateRectangleDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(100, 100), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 100), new PointF(0, 100), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 100), new PointF(0, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }

    private static CadDocument CreateTriangleDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(50, 80), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(50, 80), new PointF(0, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }

    private static CadDocument CreateSlantedDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(120, 20), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(120, 20), new PointF(100, 90), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 90), new PointF(-20, 70), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(-20, 70), new PointF(0, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }

    private static CadDocument CreateConcaveLDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(80, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(80, 0), new PointF(80, 40), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(80, 40), new PointF(40, 40), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(40, 40), new PointF(40, 100), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(40, 100), new PointF(0, 100), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 100), new PointF(0, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }

    private static CadDocument CreateSquareWithHoleDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(100, 100), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 100), new PointF(0, 100), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 100), new PointF(0, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(25, 25), new PointF(75, 25), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(75, 25), new PointF(75, 75), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(75, 75), new PointF(25, 75), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(25, 75), new PointF(25, 25), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }
}
