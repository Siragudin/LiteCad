using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Rendering.Pdf;
using LiteCad.Services;
using System.Windows;
using Xunit;

namespace LiteCad.Tests;

public class ProjectOpenCameraTests
{
    private const double Tol = 1e-3;

    private static readonly Size DefaultViewport = new(800, 600);

    [Fact]
    public void FitCameraToDocument_EmptyProject_LeavesCameraUnchanged()
    {
        var session = new CadSession();
        session.Camera.PanScreen(250, 180);
        session.Camera.ZoomAt(new Point(400, 300), 2.5, DefaultViewport);

        var zoomBefore = session.Camera.Zoom;
        var panBefore = session.Camera.PanOffset;

        session.FitCameraToDocument(DefaultViewport);

        Assert.Equal(zoomBefore, session.Camera.Zoom, Tol);
        Assert.Equal(panBefore.X, session.Camera.PanOffset.X, Tol);
        Assert.Equal(panBefore.Y, session.Camera.PanOffset.Y, Tol);
    }

    [Fact]
    public void TryComputeWorldBounds_EmptyProject_ReturnsFalse()
    {
        var document = new CadDocument();

        Assert.False(DocumentBoundsCalculator.TryComputeWorldBounds(
            document,
            LinearDisplayUnit.Millimeters,
            out _));
    }

    [Theory]
    [InlineData(0, 0, 100, 100)]
    [InlineData(0, 0, 5000, 5000)]
    [InlineData(0, 0, 4000, 200)]
    [InlineData(0, 0, 200, 3000)]
    public void FitCameraToDocument_CentersAndFitsContent(
        double minX,
        double minY,
        double maxX,
        double maxY)
    {
        var session = CreateSessionWithRectangle(minX, minY, maxX, maxY);
        session.Camera.PanScreen(900, -700);
        session.Camera.ZoomAt(new Point(100, 100), 0.05, DefaultViewport);

        session.FitCameraToDocument(DefaultViewport);

        Assert.True(DocumentBoundsCalculator.TryComputeWorldBounds(
            session.Document,
            session.DisplayUnitSettings.LinearUnit,
            out var bounds));
        AssertCameraFitsBounds(session.Camera, bounds, DefaultViewport);
    }

    [Fact]
    public void FitCameraToDocument_IncludesAxisOutsideEdges()
    {
        var session = CreateSessionWithRectangle(0, 0, 100, 100);
        AxisService.Create(session.Document, new PointF(-50, 50), new PointF(150, 50), Tol);

        session.Camera.PanScreen(500, 500);
        session.FitCameraToDocument(DefaultViewport);

        Assert.True(DocumentBoundsCalculator.TryComputeWorldBounds(
            session.Document,
            session.DisplayUnitSettings.LinearUnit,
            out var bounds));
        AssertCameraFitsBounds(session.Camera, bounds, DefaultViewport);
    }

    [Fact]
    public void FitCameraToDocument_IncludesDimensionAnnotation()
    {
        var document = new CadDocument();
        var edge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(200, 0), Tol);
        DimensionService.Create(document, edge.StartVertexId, edge.EndVertexId, 80, Tol);

        var session = new CadSession();
        ProjectDocumentSerializer.Apply(
            session.Document,
            ProjectDocumentSerializer.ToDto(document, LinearDisplayUnit.Millimeters));
        session.Camera.PanScreen(-400, 300);
        session.FitCameraToDocument(DefaultViewport);

        Assert.True(DocumentBoundsCalculator.TryComputeWorldBounds(
            session.Document,
            session.DisplayUnitSettings.LinearUnit,
            out var bounds));
        AssertCameraFitsBounds(session.Camera, bounds, DefaultViewport);
    }

    [Fact]
    public void LoadProjectThenFit_ReplacesPreviousCameraForNextProject()
    {
        var first = CreateSessionWithRectangle(0, 0, 50, 50);
        first.FitCameraToDocument(DefaultViewport);
        var firstZoom = first.Camera.Zoom;

        var secondSession = new CadSession();
        var largeDocument = CreateRectangleDocument(0, 0, 2000, 2000);
        var dto = ProjectDocumentSerializer.ToDto(largeDocument, LinearDisplayUnit.Millimeters);
        secondSession.LoadProject(dto, "Large.sit");
        secondSession.FitCameraToDocument(DefaultViewport);

        Assert.NotEqual(firstZoom, secondSession.Camera.Zoom, 0.01);
        Assert.True(DocumentBoundsCalculator.TryComputeWorldBounds(
            secondSession.Document,
            secondSession.DisplayUnitSettings.LinearUnit,
            out var bounds));
        AssertCameraFitsBounds(secondSession.Camera, bounds, DefaultViewport);
    }

    [Fact]
    public void FitCameraToDocument_ZeroViewport_DoesNotThrow()
    {
        var session = CreateSessionWithRectangle(0, 0, 100, 100);

        session.FitCameraToDocument(new Size(0, 600));
        session.FitCameraToDocument(new Size(800, 0));
    }

    private static CadSession CreateSessionWithRectangle(double minX, double minY, double maxX, double maxY)
    {
        var session = new CadSession();
        var document = CreateRectangleDocument(minX, minY, maxX, maxY);
        var dto = ProjectDocumentSerializer.ToDto(document, LinearDisplayUnit.Millimeters);
        session.LoadProject(dto, "Test.sit");
        return session;
    }

    private static CadDocument CreateRectangleDocument(double minX, double minY, double maxX, double maxY)
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF((float)minX, (float)minY), new PointF((float)maxX, (float)minY), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF((float)maxX, (float)minY), new PointF((float)maxX, (float)maxY), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF((float)maxX, (float)maxY), new PointF((float)minX, (float)maxY), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF((float)minX, (float)maxY), new PointF((float)minX, (float)minY), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        return document;
    }

    private static void AssertCameraFitsBounds(Camera camera, Rect bounds, Size viewport)
    {
        var center = new PointF(
            (float)((bounds.Left + bounds.Right) * 0.5),
            (float)((bounds.Top + bounds.Bottom) * 0.5));
        var screenCenter = camera.WorldToScreen(center, viewport);

        Assert.InRange(screenCenter.X, viewport.Width * 0.5 - 1.5, viewport.Width * 0.5 + 1.5);
        Assert.InRange(screenCenter.Y, viewport.Height * 0.5 - 1.5, viewport.Height * 0.5 + 1.5);

        var padding = Math.Min(viewport.Width, viewport.Height) * CadSession.OpenProjectFitPaddingFraction;
        var corners = new[]
        {
            new PointF((float)bounds.Left, (float)bounds.Top),
            new PointF((float)bounds.Right, (float)bounds.Top),
            new PointF((float)bounds.Right, (float)bounds.Bottom),
            new PointF((float)bounds.Left, (float)bounds.Bottom)
        };

        foreach (var corner in corners)
        {
            var screen = camera.WorldToScreen(corner, viewport);
            Assert.InRange(screen.X, padding - 2, viewport.Width - padding + 2);
            Assert.InRange(screen.Y, padding - 2, viewport.Height - padding + 2);
        }
    }
}
