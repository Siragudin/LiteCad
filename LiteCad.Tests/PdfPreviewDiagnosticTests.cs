using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Rendering.Pdf;
using LiteCad.Services;
using LiteCad.UI.Pdf;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;
using Xunit.Abstractions;

namespace LiteCad.Tests;

public class PdfPreviewDiagnosticTests : IDisposable
{
    private const double Tolerance = 1e-4;

    private readonly string _tempRoot;
    private readonly ITestOutputHelper _output;

    public PdfPreviewDiagnosticTests(ITestOutputHelper output)
    {
        _output = output;
        _tempRoot = Path.Combine(Path.GetTempPath(), "LiteCadPdfPreviewDiagTests", Guid.NewGuid().ToString("N"));
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
    public void PdfPreviewWindow_InitializeRender_DoesNotThrow_OnSquareDocument()
        => RunPreviewDiagnostics(CreateSquareDocument(), "square");

    [Fact]
    public void PdfPreviewWindow_InitializeRender_DoesNotThrow_OnRichDocument()
        => RunPreviewDiagnostics(CreateRichDocument(), "rich");

    [Fact]
    public void PdfPreviewWindow_InitializeRender_DoesNotThrow_OnLargeDimensionedDocument()
        => RunPreviewDiagnostics(CreateLargeDrawingWithDimensions(), "large-dimensioned");

    [Fact]
    public void PdfPreviewWindow_InitializeRender_DoesNotThrow_OnComplexFloorPlanLikeDocument()
        => RunPreviewDiagnostics(CreateComplexFloorPlanLikeDocument(), "floor-plan-like");

    private void RunPreviewDiagnostics(CadDocument document, string label)
    {
        Exception? caught = null;
        PdfPreviewWindow? window = null;

        WpfTestUtilities.RunSta(() =>
        {
            try
            {
                window = new PdfPreviewWindow(
                    document,
                    LinearDisplayUnit.Millimeters,
                    new Renderer(),
                    Path.Combine(_tempRoot, label + ".pdf"),
                    null);
                window.Show();
                window.UpdateLayout();

                var report = BuildLayoutReport(window, label);
                _output.WriteLine(report);
                File.WriteAllText(Path.Combine(_tempRoot, label + "-diag.txt"), report, Encoding.UTF8);

                AssertPreviewLayoutIsHealthy(window);
            }
            catch (Exception ex)
            {
                caught = ex;
                _output.WriteLine(BuildExceptionReport(label, ex));
                throw;
            }
            finally
            {
                window?.Close();
            }
        });

        Assert.Null(caught);
    }

    private static void AssertPreviewLayoutIsHealthy(PdfPreviewWindow window)
    {
        var layout = window.RenderResult!.Layout;
        var view = window.PrimaryView;

        Assert.Equal(1.0, view.Scale, 1e-9);
        Assert.Equal(0, view.Position.X, 1e-9);
        Assert.Equal(0, view.Position.Y, 1e-9);

        var drawingBounds = layout.GetDrawingBoundsInContent(view);
        Assert.True(drawingBounds.Width > 1.0, $"Drawing width too small: {drawingBounds}");
        Assert.True(drawingBounds.Height > 1.0, $"Drawing height too small: {drawingBounds}");

        var contentCenterX = layout.ContentSizeDip.Width / 2.0;
        var contentCenterY = layout.ContentSizeDip.Height / 2.0;
        var drawingCenterX = drawingBounds.X + drawingBounds.Width / 2.0;
        var drawingCenterY = drawingBounds.Y + drawingBounds.Height / 2.0;
        Assert.Equal(contentCenterX, drawingCenterX, 4.0);
        Assert.Equal(contentCenterY, drawingCenterY, 4.0);

        var previewImage = window.FindName("PreviewImage") as Image;
        Assert.NotNull(previewImage);
        Assert.Equal(layout.PageWidthDip, previewImage!.Width, 0.5);
        Assert.Equal(layout.PageHeightDip, previewImage.Height, 0.5);
        Assert.True(previewImage.ActualWidth > layout.PageWidthDip * 0.25, $"PreviewImage.ActualWidth={previewImage.ActualWidth}");
        Assert.True(previewImage.ActualHeight > layout.PageHeightDip * 0.25, $"PreviewImage.ActualHeight={previewImage.ActualHeight}");

        var displayScale = window.GetPreviewSheetDisplayScaleForTesting();
        Assert.InRange(displayScale, 0.25, 1.0);
        var fitSize = window.PreviewViewportFitSizeForTesting();
        Assert.True(fitSize.Width >= layout.PageWidthDip * displayScale * 0.95);
        Assert.True(fitSize.Height >= layout.PageHeightDip * displayScale * 0.95);
    }

    private static string BuildLayoutReport(PdfPreviewWindow window, string label)
    {
        var layout = window.RenderResult!.Layout;
        var view = window.PrimaryView;
        var drawingBounds = layout.GetDrawingBoundsInContent(view);
        var contentBounds = layout.GetTransformedBoundsInContentDip();
        var drawing = VisualTreeHelper.GetDrawing(window.RenderResult!.Visual);
        var drawingBoundsFromVisual = drawing?.Bounds ?? Rect.Empty;

        var previewImage = window.FindName("PreviewImage") as Image;
        var previewViewbox = window.FindName("PreviewViewbox") as Viewbox;
        var previewScrollViewer = window.FindName("PreviewScrollViewer") as ScrollViewer;

        var builder = new StringBuilder();
        builder.AppendLine($"=== PdfPreview diagnostics: {label} ===");
        builder.AppendLine($"PrimaryView.Scale={view.Scale}");
        builder.AppendLine($"PrimaryView.Position=({view.Position.X}, {view.Position.Y})");
        builder.AppendLine($"ExportCamera.Zoom={layout.ExportCamera.Zoom}");
        builder.AppendLine($"RequiredZoom={layout.RequiredZoom}");
        builder.AppendLine($"ZoomCompensation={layout.ZoomCompensation}");
        builder.AppendLine($"ContentSizeDip=({layout.ContentSizeDip.Width}, {layout.ContentSizeDip.Height})");
        builder.AppendLine($"PageSizeDip=({layout.PageWidthDip}, {layout.PageHeightDip})");
        builder.AppendLine($"WorldBounds={layout.WorldBounds}");
        builder.AppendLine($"TransformedBoundsInContentDip={contentBounds}");
        builder.AppendLine($"DrawingBoundsInContent(view)={drawingBounds}");
        builder.AppendLine($"VisualDrawingBounds={drawingBoundsFromVisual}");

        if (previewImage is not null)
        {
            builder.AppendLine($"PreviewImage Width/Height=({previewImage.Width}, {previewImage.Height})");
            builder.AppendLine($"PreviewImage Actual=({previewImage.ActualWidth}, {previewImage.ActualHeight})");
        }

        if (previewViewbox is not null)
        {
            builder.AppendLine($"PreviewViewbox Actual=({previewViewbox.ActualWidth}, {previewViewbox.ActualHeight})");
        }

        if (previewScrollViewer is not null)
        {
            builder.AppendLine($"ScrollViewer Viewport=({previewScrollViewer.ViewportWidth}, {previewScrollViewer.ViewportHeight})");
            builder.AppendLine($"ScrollViewer Extent=({previewScrollViewer.ExtentWidth}, {previewScrollViewer.ExtentHeight})");
            builder.AppendLine($"ScrollViewer Offset=({previewScrollViewer.HorizontalOffset}, {previewScrollViewer.VerticalOffset})");
        }

        return builder.ToString();
    }

    private static string BuildExceptionReport(string label, Exception ex)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"=== PdfPreview EXCEPTION: {label} ===");
        for (var current = ex; current is not null; current = current.InnerException)
        {
            builder.AppendLine($"{current.GetType().FullName}: {current.Message}");
            builder.AppendLine(current.StackTrace);
        }

        return builder.ToString();
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

    private static CadDocument CreateLargeDrawingWithDimensions()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(80_000, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(80_000, 0), new PointF(80_000, 40_000), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(80_000, 40_000), new PointF(0, 40_000), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 40_000), new PointF(0, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);

        var horizontalStart = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tolerance));
        var horizontalEnd = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(80_000, 0), Tolerance));
        var verticalEnd = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(80_000, 40_000), Tolerance));
        DimensionService.Create(document, horizontalStart.Id, horizontalEnd.Id, 2_000, Tolerance);
        DimensionService.Create(document, horizontalEnd.Id, verticalEnd.Id, 2_000, Tolerance);
        return document;
    }

    private static CadDocument CreateComplexFloorPlanLikeDocument()
    {
        var document = new CadDocument();
        const float cell = 3_000;
        for (var row = 0; row < 4; row++)
        {
            for (var col = 0; col < 5; col++)
            {
                var x = col * cell;
                var y = row * cell;
                TestDocumentHelpers.AddEdge(document, new PointF(x, y), new PointF(x + cell, y), Tolerance);
                TestDocumentHelpers.AddEdge(document, new PointF(x + cell, y), new PointF(x + cell, y + cell), Tolerance);
                TestDocumentHelpers.AddEdge(document, new PointF(x + cell, y + cell), new PointF(x, y + cell), Tolerance);
                TestDocumentHelpers.AddEdge(document, new PointF(x, y + cell), new PointF(x, y), Tolerance);
            }
        }

        PolygonBuilder.SyncFaces(document, Tolerance);

        foreach (var face in document.Polygons.Where(polygon => polygon.Type == PolygonType.Face).Take(3))
        {
            FaceFillService.TrySetFillColor(document, face, Colors.Gainsboro);
        }

        var first = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tolerance));
        var second = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(cell * 5, 0), Tolerance));
        var third = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(cell * 5, cell * 4), Tolerance));
        DimensionService.Create(document, first.Id, second.Id, 800, Tolerance);
        DimensionService.Create(document, second.Id, third.Id, 800, Tolerance);

        AxisService.Create(document, new PointF(-1_000, cell * 2), new PointF(cell * 5 + 1_000, cell * 2), Tolerance);

        document.Edges[0].LineType = EdgeLineType.Dashed;
        document.Edges[1].LineType = EdgeLineType.Dotted;
        document.Edges[2].Thickness = 3.0;
        return document;
    }
}
