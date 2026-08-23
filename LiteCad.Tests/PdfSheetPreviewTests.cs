using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Rendering.Pdf;
using LiteCad.Services;
using LiteCad.UI.Pdf;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class PdfSheetPreviewTests : IDisposable
{
    private const double Tol = 1e-4;

    private readonly string _tempRoot;

    public PdfSheetPreviewTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "LiteCadPdfSheetTests", Guid.NewGuid().ToString("N"));
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
    public void PdfSheet_DefaultOrientation_IsPortrait()
    {
        var sheet = new PdfSheet(new CadDocument());
        Assert.Equal(PdfPageOrientation.Portrait, sheet.Orientation);
        Assert.Single(sheet.Views);
    }

    [Fact]
    public void PdfRenderer_Render_ProducesPageVisual()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var sheet = new PdfSheet(document);
            var result = PdfRenderer.Render(sheet, LinearDisplayUnit.Millimeters, new Renderer());

            Assert.NotNull(result.Visual);
            Assert.Equal(PdfPageOrientation.Portrait, result.Layout.Orientation);
            Assert.True(result.Layout.PageWidthDip > 0);
            Assert.NotNull(VisualTreeHelper.GetDrawing(result.Visual));
        });
    }

    [Fact]
    public void PdfRenderer_LandscapeOrientation_UpdatesPageSize()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var sheet = new PdfSheet(CreateSquareDocument()) { Orientation = PdfPageOrientation.Landscape };
            var result = PdfRenderer.Render(sheet, LinearDisplayUnit.Millimeters, new Renderer());

            Assert.True(result.Layout.IsLandscape);
            Assert.True(result.Layout.PageWidthDip > result.Layout.PageHeightDip);
        });
    }

    [Fact]
    public void PdfRenderer_PreviewAndSave_UseSamePipeline()
    {
        var pdfPath = Path.Combine(_tempRoot, "PreviewSave.pdf");

        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var sheet = new PdfSheet(document);
            var renderer = new Renderer();
            var preview = PdfRenderer.Render(sheet, LinearDisplayUnit.Millimeters, renderer);
            PdfRenderer.Save(preview, LinearDisplayUnit.Millimeters, pdfPath);

            Assert.True(File.Exists(pdfPath));
            Assert.StartsWith("%PDF", ReadFileHeader(pdfPath));
        });
    }

    [Fact]
    public void PdfPreviewWindow_OrientationSwitch_RefreshesLayout()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var window = new PdfPreviewWindow(
                document,
                LinearDisplayUnit.Millimeters,
                new Renderer(),
                Path.Combine(_tempRoot, "out.pdf"),
                null);

            window.Show();
            window.OrientationCombo.SelectedIndex = 1;
            window.UpdateLayout();

            var portraitWidth = PdfExportLayout.Create(document, PdfPageOrientation.Portrait).PageWidthDip;
            var landscapeWidth = PdfExportLayout.Create(document, PdfPageOrientation.Landscape).PageWidthDip;
            Assert.Equal(landscapeWidth, window.PreviewImage.Width, 1.0);
            Assert.NotEqual(portraitWidth, window.PreviewImage.Width);

            window.Close();
        });
    }

    [Fact]
    public void PdfPreview_DoesNotModifyDocumentOrSelection()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var session = new CadSession();
            TestDocumentHelpers.AddEdge(session.Document, new PointF(0, 0), new PointF(100, 0), Tol);
            var dimension = DimensionService.Create(
                session.Document,
                session.Document.Edges[0].StartVertexId,
                session.Document.Edges[0].EndVertexId,
                10,
                Tol,
                textSize: 10);
            session.Selection.SelectedDimensionIds.Add(dimension.Id);
            session.ProjectFile.MarkDirty();
            var edgeCount = session.Document.Edges.Count;
            var dimensionCount = session.Document.Dimensions.Count;
            var selected = session.Selection.SelectedDimensionIds.Count;
            var wasDirty = session.ProjectFile.IsDirty;
            var cameraZoom = session.Camera.Zoom;
            var cameraPan = session.Camera.PanOffset;

            var window = new PdfPreviewWindow(
                session.Document,
                session.DisplayUnitSettings.LinearUnit,
                session.Renderer,
                Path.Combine(_tempRoot, "cancel.pdf"),
                null);
            window.Show();
            window.OrientationCombo.SelectedIndex = 1;
            window.ApplyDragDeltaForTesting(new Vector(40, 25));
            window.ApplyWheelZoomForTesting(new Point(120, 120), 120);
            window.UpdateLayout();
            window.Close();

            Assert.Equal(edgeCount, session.Document.Edges.Count);
            Assert.Equal(dimensionCount, session.Document.Dimensions.Count);
            Assert.Equal(selected, session.Selection.SelectedDimensionIds.Count);
            Assert.Equal(wasDirty, session.ProjectFile.IsDirty);
            Assert.Equal(cameraZoom, session.Camera.Zoom, 1e-9);
            Assert.Equal(cameraPan.X, session.Camera.PanOffset.X, 1e-9);
            Assert.Equal(cameraPan.Y, session.Camera.PanOffset.Y, 1e-9);
        });
    }

    [Fact]
    public void PdfDrawingView_Drag_UpdatesPositionOnly()
    {
        var sheet = new PdfSheet(CreateSquareDocument());
        var view = sheet.PrimaryView;
        var initialScale = view.Scale;

        view.ApplyDragDelta(new Vector(35, -20));

        Assert.Equal(35, view.Position.X, 1e-9);
        Assert.Equal(-20, view.Position.Y, 1e-9);
        Assert.Equal(initialScale, view.Scale, 1e-9);
    }

    [Fact]
    public void PdfDrawingView_Wheel_UpdatesScale()
    {
        var sheet = new PdfSheet(CreateSquareDocument());
        var view = sheet.PrimaryView;
        var initialScale = view.Scale;

        view.ApplyWheelZoomAtContentPoint(new Point(150, 150), 120);

        Assert.True(view.Scale > initialScale);

        var scaleAfterZoomIn = view.Scale;
        view.ApplyWheelZoomAtContentPoint(new Point(150, 150), -120);
        Assert.True(view.Scale < scaleAfterZoomIn);
    }

    [Fact]
    public void PdfDrawingView_Scale_IsClamped()
    {
        var view = new PdfDrawingView(new CadDocument());
        view.ApplyZoomAtContentPoint(new Point(0, 0), PdfDrawingView.MaxScale * 2);
        Assert.Equal(PdfDrawingView.MaxScale, view.Scale, 1e-9);

        view.ApplyZoomAtContentPoint(new Point(0, 0), PdfDrawingView.MinScale / 2);
        Assert.Equal(PdfDrawingView.MinScale, view.Scale, 1e-9);
    }

    [Fact]
    public void PdfSheet_OrientationChange_PreservesViewPositionAndScale()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var sheet = new PdfSheet(CreateSquareDocument())
            {
                Orientation = PdfPageOrientation.Portrait
            };
            sheet.PrimaryView.Position = new Point(42, 18);
            sheet.PrimaryView.Scale = 1.75;

            sheet.Orientation = PdfPageOrientation.Landscape;

            Assert.Equal(42, sheet.PrimaryView.Position.X, 1e-9);
            Assert.Equal(18, sheet.PrimaryView.Position.Y, 1e-9);
            Assert.Equal(1.75, sheet.PrimaryView.Scale, 1e-9);
        });
    }

    [Fact]
    public void PdfRenderer_ViewTransform_ChangesDrawingBounds()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var sheet = new PdfSheet(CreateSquareDocument());
            var defaultResult = PdfRenderer.Render(sheet, LinearDisplayUnit.Millimeters, new Renderer());
            var defaultBounds = defaultResult.Layout.GetDrawingBoundsInContent(sheet.PrimaryView);

            sheet.PrimaryView.Position = new Point(50, 30);
            sheet.PrimaryView.Scale = 1.5;
            var adjustedResult = PdfRenderer.Render(sheet, LinearDisplayUnit.Millimeters, new Renderer());
            var adjustedBounds = adjustedResult.Layout.GetDrawingBoundsInContent(sheet.PrimaryView);

            Assert.NotEqual(defaultBounds.X, adjustedBounds.X, 1.0);
            Assert.True(adjustedBounds.Width > defaultBounds.Width);
        });
    }

    [Fact]
    public void PdfRenderer_Save_IncludesViewPositionAndScale()
    {
        var pdfPath = Path.Combine(_tempRoot, "ViewTransform.pdf");

        WpfTestUtilities.RunSta(() =>
        {
            var sheet = new PdfSheet(CreateSquareDocument());
            sheet.PrimaryView.Position = new Point(60, 40);
            sheet.PrimaryView.Scale = 1.25;
            var renderer = new Renderer();
            var result = PdfRenderer.Render(sheet, LinearDisplayUnit.Millimeters, renderer);
            PdfRenderer.Save(result, LinearDisplayUnit.Millimeters, pdfPath);

            Assert.True(File.Exists(pdfPath));
            Assert.StartsWith("%PDF", ReadFileHeader(pdfPath));

            var savedBounds = result.Layout.GetDrawingBoundsInContent(sheet.PrimaryView);
            var baseBounds = result.Layout.GetTransformedBoundsInContentDip();
            Assert.Equal(60, savedBounds.X - baseBounds.X * sheet.PrimaryView.Scale, 1.0);
            Assert.Equal(40, savedBounds.Y - baseBounds.Y * sheet.PrimaryView.Scale, 1.0);
        });
    }

    [Fact]
    public void PdfPreviewWindow_ViewportFit_FillsAvailablePreviewArea()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var window = new PdfPreviewWindow(
                document,
                LinearDisplayUnit.Millimeters,
                new Renderer(),
                Path.Combine(_tempRoot, "viewport-fit.pdf"),
                null);

            window.PrimaryView.Position = new Point(80, 120);
            window.PrimaryView.Scale = 0.5;

            window.Show();
            window.UpdateLayout();

            Assert.Equal(80, window.PrimaryView.Position.X, 1e-9);
            Assert.Equal(120, window.PrimaryView.Position.Y, 1e-9);
            Assert.Equal(0.5, window.PrimaryView.Scale, 1e-9);

            var fitSize = window.PreviewViewportFitSizeForTesting();
            Assert.True(fitSize.Width > 200);
            Assert.True(fitSize.Height > 200);

            var displayScale = window.GetPreviewSheetDisplayScaleForTesting();
            Assert.InRange(displayScale, 0.25, 1.5);

            var layout = window.RenderResult!.Layout;
            var scaledWidth = layout.PageWidthDip * displayScale;
            var scaledHeight = layout.PageHeightDip * displayScale;
            var widthFill = scaledWidth / fitSize.Width;
            var heightFill = scaledHeight / fitSize.Height;
            Assert.True(Math.Max(widthFill, heightFill) >= 0.85);

            window.Close();
        });
    }

    [Fact]
    public void PdfPreviewWindow_ViewportFit_RecalculatesOnOrientationChangeWithoutChangingViewTransform()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var window = new PdfPreviewWindow(
                document,
                LinearDisplayUnit.Millimeters,
                new Renderer(),
                Path.Combine(_tempRoot, "viewport-orientation.pdf"),
                null);

            window.PrimaryView.Position = new Point(42, 18);
            window.PrimaryView.Scale = 1.75;

            window.Show();
            window.UpdateLayout();

            var portraitScale = window.GetPreviewSheetDisplayScaleForTesting();
            Assert.True(portraitScale > 0);

            window.OrientationCombo.SelectedIndex = 1;
            window.UpdateLayout();

            Assert.Equal(42, window.PrimaryView.Position.X, 1e-9);
            Assert.Equal(18, window.PrimaryView.Position.Y, 1e-9);
            Assert.Equal(1.75, window.PrimaryView.Scale, 1e-9);

            var landscapeScale = window.GetPreviewSheetDisplayScaleForTesting();
            Assert.True(landscapeScale > 0);
            Assert.NotEqual(portraitScale, landscapeScale, 3);

            window.Close();
        });
    }

    [Fact]
    public void PdfPreviewWindow_InitialView_IsFitToPageCenter()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var window = new PdfPreviewWindow(
                document,
                LinearDisplayUnit.Millimeters,
                new Renderer(),
                Path.Combine(_tempRoot, "initial.pdf"),
                null);

            Assert.Equal(0, window.PrimaryView.Position.X, 1e-9);
            Assert.Equal(0, window.PrimaryView.Position.Y, 1e-9);
            Assert.Equal(1.0, window.PrimaryView.Scale, 1e-9);

            var layout = window.RenderResult!.Layout;
            var bounds = layout.GetDrawingBoundsInContent(window.PrimaryView);
            Assert.Equal(layout.ContentSizeDip.Width / 2, bounds.X + bounds.Width / 2, 2.0);
            Assert.Equal(layout.ContentSizeDip.Height / 2, bounds.Y + bounds.Height / 2, 2.0);

            window.Close();
        });
    }

    [Fact]
    public void PdfPreviewWindow_Cancel_DoesNotModifyDocument()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var edgeCount = document.Edges.Count;

            var window = new PdfPreviewWindow(
                document,
                LinearDisplayUnit.Millimeters,
                new Renderer(),
                Path.Combine(_tempRoot, "cancel-only.pdf"),
                null);
            window.Show();
            window.ApplyDragDeltaForTesting(new Vector(100, 100));
            window.ApplyWheelZoomForTesting(new Point(80, 80), 240);
            window.Close();

            Assert.Equal(edgeCount, document.Edges.Count);
            Assert.False(window.Saved);
        });
    }

    [Fact]
    public void DocumentBoundsCalculator_IncludesSmallAndFarGeometry()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(1000, 1000), new PointF(1010, 1000), Tol);

        var bounds = DocumentBoundsCalculator.ComputeWorldBounds(document);

        Assert.True(bounds.X <= 1000);
        Assert.True(bounds.Right >= 1010);
    }

    private static CadDocument CreateSquareDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(100, 100), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 100), new PointF(0, 100), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 100), new PointF(0, 0), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        return document;
    }

    private static string ReadFileHeader(string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[4];
        _ = stream.Read(buffer, 0, buffer.Length);
        return System.Text.Encoding.ASCII.GetString(buffer);
    }
}
