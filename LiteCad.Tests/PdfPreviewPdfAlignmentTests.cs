using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Rendering.Pdf;
using LiteCad.Services;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class PdfPreviewPdfAlignmentTests : IDisposable
{
    private const double Tol = 1.5;

    private readonly string _tempRoot;

    public PdfPreviewPdfAlignmentTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "LiteCadPdfAlignmentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData(0, 0, 1.0, PdfPageOrientation.Portrait)]
    [InlineData(80, 40, 1.0, PdfPageOrientation.Portrait)]
    [InlineData(20, 10, 0.5, PdfPageOrientation.Portrait)]
    [InlineData(120, 60, 2.0, PdfPageOrientation.Portrait)]
    [InlineData(50, 30, 1.25, PdfPageOrientation.Landscape)]
    public void PreviewVisual_MatchesSheetContentBounds(
        double positionX,
        double positionY,
        double scale,
        PdfPageOrientation orientation)
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var sheet = new PdfSheet(document) { Orientation = orientation };
            sheet.PrimaryView.Position = new Point(positionX, positionY);
            sheet.PrimaryView.Scale = scale;

            var result = PdfRenderer.Render(sheet, LinearDisplayUnit.Millimeters, new Renderer());
            var expectedPageBounds = GetExpectedPageBounds(sheet, result.Layout);
            var visualPageBounds = GetGeometryPageBounds(result.Visual);

            AssertBoundsNear(expectedPageBounds, visualPageBounds, Tol);
        });
    }

    private static Rect GetExpectedPageBounds(PdfSheet sheet, PdfExportLayout layout)
        => PdfSheetTransform.TransformWorldBoundsToPageDip(sheet, layout, new Rect(0, 0, 100, 100));

    [Fact]
    public void ManualPlacement_IsNotRefitOnSave()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var sheet = new PdfSheet(document);
            sheet.PrimaryView.Position = new Point(90, 45);
            sheet.PrimaryView.Scale = 1.6;

            var beforeLayout = PdfExportLayout.Create(document, sheet.Orientation);
            var beforePageBounds = GetExpectedPageBounds(sheet, beforeLayout);

            var result = PdfRenderer.Render(sheet, LinearDisplayUnit.Millimeters, new Renderer());
            Assert.Equal(90, sheet.PrimaryView.Position.X, 1e-9);
            Assert.Equal(45, sheet.PrimaryView.Position.Y, 1e-9);
            Assert.Equal(1.6, sheet.PrimaryView.Scale, 1e-9);

            var afterPageBounds = GetExpectedPageBounds(sheet, result.Layout);
            AssertBoundsNear(beforePageBounds, afterPageBounds, Tol);

            var pdfPath = Path.Combine(_tempRoot, "manual-placement.pdf");
            PdfRenderer.Save(result, LinearDisplayUnit.Millimeters, pdfPath);
            Assert.True(File.Exists(pdfPath));
        });
    }

    [Fact]
    public void SavePdf_UsesExistingSheetInstance()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var sheet = new PdfSheet(document);
            sheet.PrimaryView.Position = new Point(70, 35);
            sheet.PrimaryView.Scale = 1.3;

            var result = PdfRenderer.Render(sheet, LinearDisplayUnit.Millimeters, new Renderer());
            Assert.Same(sheet, result.Sheet);
            Assert.Same(sheet.PrimaryView, result.Sheet.PrimaryView);

            var pdfPath = Path.Combine(_tempRoot, "same-sheet.pdf");
            PdfRenderer.Save(result, LinearDisplayUnit.Millimeters, pdfPath);
            Assert.True(File.Exists(pdfPath));
        });
    }

    [Fact]
    public void PdfSheetTransform_MatchesExportVisualCorners()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var sheet = new PdfSheet(CreateSquareDocument());
            sheet.PrimaryView.Position = new Point(55, 25);
            sheet.PrimaryView.Scale = 1.4;

            var layout = PdfExportLayout.Create(sheet.Document, sheet.Orientation);
            var visual = PdfExporter.BuildExportVisual(sheet, LinearDisplayUnit.Millimeters, new Renderer(), layout);
            var visualBounds = GetGeometryPageBounds(visual);
            var expectedBounds = GetExpectedPageBounds(sheet, layout);

            AssertBoundsNear(expectedBounds, visualBounds, Tol);

            var worldCorner = new Point(0, 0);
            var expectedCorner = PdfSheetTransform.TransformWorldToPageDip(sheet, layout, worldCorner);
            var matrixCorner = PdfSheetTransform.GetWorldToPageDipMatrix(sheet, layout).Transform(worldCorner);
            Assert.Equal(expectedCorner.X, matrixCorner.X, Tol);
            Assert.Equal(expectedCorner.Y, matrixCorner.Y, Tol);
        });
    }

    [Fact]
    public void PreviewWindow_SaveRendersCurrentSheetState()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var pdfPath = Path.Combine(_tempRoot, "window-save.pdf");
            var window = new LiteCad.UI.Pdf.PdfPreviewWindow(
                document,
                LinearDisplayUnit.Millimeters,
                new Renderer(),
                pdfPath,
                null);

            window.Show();
            window.ApplyDragDeltaForTesting(new Vector(55, 25));
            window.ApplyWheelZoomForTesting(new Point(120, 120), 120);

            var expectedPageBounds = GetExpectedPageBounds(window.Sheet, window.RenderResult!.Layout);

            var saveResult = PdfRenderer.Render(window.Sheet, LinearDisplayUnit.Millimeters, new Renderer());
            var visualBounds = GetGeometryPageBounds(saveResult.Visual);
            PdfRenderer.Save(saveResult, LinearDisplayUnit.Millimeters, pdfPath);
            window.Close();

            AssertBoundsNear(expectedPageBounds, visualBounds, Tol);
            Assert.Equal(window.PrimaryView.Position.X, saveResult.Sheet.PrimaryView.Position.X, 1e-9);
            Assert.Equal(window.PrimaryView.Scale, saveResult.Sheet.PrimaryView.Scale, 1e-9);
            Assert.True(File.Exists(pdfPath));
        });
    }

    [Fact]
    public void PreviewEdits_DoNotModifyCadDocument()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var edgeCount = document.Edges.Count;

            var sheet = new PdfSheet(document);
            sheet.PrimaryView.ApplyDragDelta(new Vector(120, 80));
            sheet.PrimaryView.ApplyWheelZoomAtContentPoint(new Point(90, 90), 240);
            PdfRenderer.Render(sheet, LinearDisplayUnit.Millimeters, new Renderer());

            Assert.Equal(edgeCount, document.Edges.Count);
        });
    }

    private static void AssertBoundsNear(Rect expectedDip, Rect actualDip, double tolerance)
    {
        Assert.Equal(expectedDip.X, actualDip.X, tolerance);
        Assert.Equal(expectedDip.Y, actualDip.Y, tolerance);
        Assert.Equal(expectedDip.Width, actualDip.Width, tolerance);
        Assert.Equal(expectedDip.Height, actualDip.Height, tolerance);
    }

    private static Rect GetGeometryPageBounds(DrawingVisual visual)
    {
        var drawing = VisualTreeHelper.GetDrawing(visual) as DrawingGroup;
        Assert.NotNull(drawing);

        var bounds = Rect.Empty;
        AccumulateBounds(drawing!, Matrix.Identity, ref bounds);
        return bounds;
    }

    private static void AccumulateBounds(DrawingGroup group, Matrix parentTransform, ref Rect bounds)
    {
        var groupTransform = group.Transform?.Value ?? Matrix.Identity;
        var combined = groupTransform * parentTransform;

        foreach (var child in group.Children)
        {
            switch (child)
            {
                case GeometryDrawing { Geometry: { } geometry, Brush: SolidColorBrush { Color.R: 255, Color.G: 255, Color.B: 255 } }
                    when geometry.Bounds.Width > 700 && geometry.Bounds.Height > 700:
                    break;
                case GeometryDrawing { Geometry: { } geometry }:
                    if (!geometry.Bounds.IsEmpty)
                    {
                        bounds.Union(TransformBounds(geometry.Bounds, combined));
                    }

                    break;
                case DrawingGroup nested:
                    AccumulateBounds(nested, combined, ref bounds);
                    break;
            }
        }
    }

    private static Rect TransformBounds(Rect source, Matrix matrix)
        => PdfExportLayout.TransformBounds(source, point => matrix.Transform(point));

    private static CadDocument CreateSquareDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), 1e-4);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(100, 100), 1e-4);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 100), new PointF(0, 100), 1e-4);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 100), new PointF(0, 0), 1e-4);
        PolygonBuilder.SyncFaces(document, 1e-4);
        return document;
    }
}
