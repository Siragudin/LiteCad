using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Rendering.Pdf;
using LiteCad.Services;
using LiteCad.Tools;
using PdfSharp.Pdf.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class PdfExporterTests : IDisposable
{
    private const double Tolerance = 1e-4;

    private readonly string _tempRoot;

    public PdfExporterTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "LiteCadPdfTests", Guid.NewGuid().ToString("N"));
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
    public void Export_CreatesPdfFileWithPdfExtension()
    {
        var pdfPath = Path.Combine(_tempRoot, "Plan.pdf");

        WpfTestUtilities.RunSta(() =>
        {
            PdfExporter.Export(CreateSquareDocument(), LinearDisplayUnit.Millimeters, new Renderer(), pdfPath);
        });

        Assert.True(File.Exists(pdfPath));
        Assert.Equal(".pdf", Path.GetExtension(pdfPath), ignoreCase: true);
        Assert.StartsWith("%PDF", ReadFileHeader(pdfPath));
        Assert.True(new FileInfo(pdfPath).Length > 512);
    }

    [Fact]
    public void Export_DoesNotChangeCurrentSitProjectState()
    {
        var sitPath = Path.Combine(_tempRoot, "House.sit");
        var pdfPath = Path.Combine(_tempRoot, "Plan.pdf");
        var storage = new ProjectStorage(_tempRoot);
        var session = CreateSquareSession();

        WpfTestUtilities.RunSta(() =>
        {
            storage.SaveProject(
                session.Document,
                session.DisplayUnitSettings.LinearUnit,
                sitPath,
                session.Renderer);

            session.Selection.SelectedEdgeIds.Add(session.Document.Edges[0].Id);
            session.ProjectFile.MarkDirty();
            var historyCount = session.History.CanUndo;
            var currentPath = session.ProjectFile.CurrentFilePath;
            var wasDirty = session.ProjectFile.IsDirty;
            var selectedCount = session.Selection.SelectedEdgeIds.Count;
            var activeTool = session.ToolService.ActiveTool?.Id;

            PdfExporter.Export(
                session.Document,
                session.DisplayUnitSettings.LinearUnit,
                session.Renderer,
                pdfPath);

            Assert.Equal(currentPath, session.ProjectFile.CurrentFilePath);
            Assert.Equal(wasDirty, session.ProjectFile.IsDirty);
            Assert.Equal(selectedCount, session.Selection.SelectedEdgeIds.Count);
            Assert.Equal(activeTool, session.ToolService.ActiveTool?.Id);
            Assert.Equal(historyCount, session.History.CanUndo);

            storage.SaveProject(
                session.Document,
                session.DisplayUnitSettings.LinearUnit,
                sitPath,
                session.Renderer);
        });

        Assert.True(File.Exists(sitPath));
        Assert.True(File.Exists(pdfPath));

        WpfTestUtilities.RunSta(() =>
        {
            var dto = storage.LoadProject(sitPath, out _);
            var reloadSession = new CadSession();
            reloadSession.LoadProject(dto, sitPath);

            Assert.Equal(4, reloadSession.Document.Edges.Count);
            Assert.Single(reloadSession.Document.Polygons);
            TopologyValidator.AssertValid(reloadSession.Document, Tolerance);
        });
    }

    [Fact]
    public void DocumentBoundsCalculator_ComputesSquareBounds()
    {
        var document = CreateSquareDocument();
        var bounds = DocumentBoundsCalculator.ComputeWorldBounds(document);

        Assert.InRange(bounds.X, -10, 0);
        Assert.InRange(bounds.Y, -10, 0);
        Assert.InRange(bounds.Width, 100, 120);
        Assert.InRange(bounds.Height, 100, 120);
    }

    [Fact]
    public void PdfExportLayout_SelectsLandscapeForWideDrawing()
    {
        var document = CreateWideDocument();
        var layout = PdfExportLayout.Create(document);

        Assert.True(layout.IsLandscape);
        Assert.True(layout.PageWidthPoints > layout.PageHeightPoints);
    }

    [Fact]
    public void PdfExportLayout_SelectsPortraitForTallDrawing()
    {
        var document = CreateTallDocument();
        var layout = PdfExportLayout.Create(document);

        Assert.False(layout.IsLandscape);
        Assert.True(layout.PageHeightPoints > layout.PageWidthPoints);
    }

    [Fact]
    public void PdfExportLayout_SelectsPortraitForSquareDrawing()
    {
        var layout = PdfExportLayout.Create(CreateSquareDocument());

        Assert.False(layout.IsLandscape);
    }

    [Fact]
    public void PdfExportLayout_FitsGeometryIntoA4ContentArea()
    {
        var layout = PdfExportLayout.Create(CreateSquareDocument());
        var transformed = layout.GetTransformedBoundsInContentDip();

        Assert.True(transformed.Left >= -1.0);
        Assert.True(transformed.Top >= -1.0);
        Assert.True(transformed.Right <= layout.ContentSizeDip.Width + 1.0);
        Assert.True(transformed.Bottom <= layout.ContentSizeDip.Height + 1.0);
    }

    [Fact]
    public void PdfExportLayout_CentersDrawingInContentViewport()
    {
        var layout = PdfExportLayout.Create(CreateSquareDocument());
        var center = new Point(
            layout.WorldBounds.X + layout.WorldBounds.Width / 2.0,
            layout.WorldBounds.Y + layout.WorldBounds.Height / 2.0);
        var screenCenter = layout.TransformWorldToContent(center);

        Assert.Equal(layout.ContentSizeDip.Width / 2, screenCenter.X, 1.0);
        Assert.Equal(layout.ContentSizeDip.Height / 2, screenCenter.Y, 1.0);
    }

    [Fact]
    public void BuildExportVisual_IncludesEdgesFacesFillAndDimensions()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateRichDocument();
            var layout = PdfExportLayout.Create(document);
            var visual = PdfExporter.BuildExportVisual(
                document,
                LinearDisplayUnit.Millimeters,
                new Renderer(),
                layout);

            var drawing = ExtractDrawing(visual);
            Assert.NotNull(drawing);
            Assert.NotEmpty(drawing!.Children);

            var bounds = GetContentBounds(drawing);
            Assert.True(bounds.Width > 10);
            Assert.True(bounds.Height > 10);
        });
    }

    [Fact]
    public void RenderForExport_DoesNotIncludeSelectionOverlay()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var session = CreateSquareSession();
            var firstVertex = session.Document.Vertices.First(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tolerance));
            var secondVertex = session.Document.Vertices.First(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(100, 0), Tolerance));
            var dimension = DimensionService.Create(session.Document, firstVertex.Id, secondVertex.Id, 25, Tolerance);
            session.Selection.SelectedDimensionIds.Add(dimension.Id);

            var layout = PdfExportLayout.Create(session.Document);
            var exportDrawing = RenderExportDrawing(session, layout);
            var selectionBlue = Color.FromRgb(0x1E, 0x88, 0xE5);

            Assert.DoesNotContain(
                CollectBrushes(exportDrawing),
                brush => brush.Color == selectionBlue);
        });
    }

    [Fact]
    public void PdfFileNameHelper_NormalizesPdfExtension()
    {
        var path = PdfFileNameHelper.NormalizePdfFilePath(Path.Combine(_tempRoot, "Plan дома.pdf.pdf"));

        Assert.EndsWith("Plan дома.pdf", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Export_PreservesColoredFaceFillInVisualTree()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var face = Assert.Single(document.Polygons);
            Assert.True(FaceFillService.TrySetFillColor(document, face, Colors.OrangeRed));

            var layout = PdfExportLayout.Create(document);
            var visual = PdfExporter.BuildExportVisual(
                document,
                LinearDisplayUnit.Millimeters,
                new Renderer(),
                layout);
            var drawing = ExtractDrawing(visual);

            Assert.Contains(CollectBrushes(drawing!), brush => brush.Color.R > 200 && brush.Color.G < 100);
        });
    }

    [Fact]
    public void Export_OpenPdf_HasAtLeastOnePage()
    {
        var pdfPath = Path.Combine(_tempRoot, "Drawing.pdf");

        WpfTestUtilities.RunSta(() =>
        {
            PdfExporter.Export(CreateRichDocument(), LinearDisplayUnit.Millimeters, new Renderer(), pdfPath);
        });

        using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        Assert.True(document.PageCount >= 1);
    }

    [Fact]
    public void Export_IsIndependentOfSessionCameraZoom()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var session = CreateSquareSession();
            session.Camera.ZoomAt(new Point(100, 100), 0.05, new Size(800, 600));

            var pdfPathLowZoom = Path.Combine(_tempRoot, "LowZoom.pdf");
            PdfExporter.Export(
                session.Document,
                session.DisplayUnitSettings.LinearUnit,
                session.Renderer,
                pdfPathLowZoom);

            session.Camera.Reset();
            session.Camera.ZoomAt(new Point(100, 100), 20, new Size(800, 600));

            var pdfPathHighZoom = Path.Combine(_tempRoot, "HighZoom.pdf");
            PdfExporter.Export(
                session.Document,
                session.DisplayUnitSettings.LinearUnit,
                session.Renderer,
                pdfPathHighZoom);

            var lowLayout = PdfExportLayout.Create(session.Document);
            Assert.Equal(lowLayout.Scale, PdfExportLayout.Create(session.Document).Scale, 5);
            Assert.InRange(Math.Abs(new FileInfo(pdfPathLowZoom).Length - new FileInfo(pdfPathHighZoom).Length), 0, 4096);
        });
    }

    private static DrawingGroup RenderExportDrawing(CadSession session, PdfExportLayout layout)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            session.Renderer.RenderForExport(
                context,
                session.Document,
                layout.ExportCamera,
                layout.ContentSizeDip,
                session.DisplayUnitSettings.LinearUnit);
        }

        return ExtractDrawing(visual)!;
    }

    private static Rect GetContentBounds(DrawingGroup drawing)
    {
        var bounds = Rect.Empty;
        foreach (var child in drawing.Children)
        {
            bounds.Union(child.Bounds);
        }

        return bounds;
    }

    private static IEnumerable<SolidColorBrush> CollectBrushes(Drawing drawing)
    {
        switch (drawing)
        {
            case GeometryDrawing geometryDrawing when geometryDrawing.Brush is SolidColorBrush fillBrush:
                yield return fillBrush;
                break;
            case GlyphRunDrawing glyph when glyph.ForegroundBrush is SolidColorBrush textBrush:
                yield return textBrush;
                break;
            case DrawingGroup group:
                foreach (var child in group.Children)
                {
                    foreach (var brush in CollectBrushes(child))
                    {
                        yield return brush;
                    }
                }

                break;
        }
    }

    private static DrawingGroup? ExtractDrawing(DrawingVisual visual)
    {
        DrawingGroup? drawing = null;
        visual.Dispatcher.Invoke(() => drawing = VisualTreeHelper.GetDrawing(visual) as DrawingGroup);
        return drawing;
    }

    private static CadDocument CreateSquareDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(100, 100), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 100), new PointF(0, 100), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 100), new PointF(0, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }

    private static CadDocument CreateWideDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(300, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(300, 0), new PointF(300, 50), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(300, 50), new PointF(0, 50), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 50), new PointF(0, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }

    private static CadDocument CreateTallDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(50, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(50, 300), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(50, 300), new PointF(0, 300), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 300), new PointF(0, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }

    private static CadDocument CreateRichDocument()
    {
        var document = CreateSquareDocument();
        var face = Assert.Single(document.Polygons);
        Assert.True(FaceFillService.TrySetFillColor(document, face, Colors.MediumSeaGreen));
        Assert.True(FaceFillService.TrySetFillPattern(document, face, FaceFillPattern.Diagonal));

        var firstVertex = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tolerance));
        var secondVertex = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(100, 0), Tolerance));
        DimensionService.Create(document, firstVertex.Id, secondVertex.Id, 25, Tolerance);

        document.Edges[0].Color = Colors.DarkRed;
        document.Edges[0].Thickness = 2.5;
        return document;
    }

    private static CadSession CreateSquareSession()
    {
        var session = new CadSession();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(session.Document, new PointF(0, 0), new PointF(100, 0), template, Tolerance);
        EdgeOperations.AddSegment(session.Document, new PointF(100, 0), new PointF(100, 100), template, Tolerance);
        EdgeOperations.AddSegment(session.Document, new PointF(100, 100), new PointF(0, 100), template, Tolerance);
        EdgeOperations.AddSegment(session.Document, new PointF(0, 100), new PointF(0, 0), template, Tolerance);
        PolygonBuilder.SyncFaces(session.Document, Tolerance);
        return session;
    }

    private static string ReadFileHeader(string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[4];
        _ = stream.Read(buffer, 0, buffer.Length);
        return Encoding.ASCII.GetString(buffer);
    }
}
