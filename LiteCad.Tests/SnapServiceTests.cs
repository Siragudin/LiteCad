using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Services;
using Xunit;

namespace LiteCad.Tests;

public class SnapServiceTests
{
    private const double Tolerance = 1e-4;
    private const double UiTolerance = 12.0;

    [Fact]
    public void SquareCorner_ReturnsExactlyOneGeometricSnap()
    {
        var document = CreateSquare(UiTolerance);
        var snapService = new SnapService();

        foreach (var vertex in document.Vertices)
        {
            var snaps = snapService.GetVisibleSnaps(document, vertex.Position, UiTolerance);
            Assert.Single(snaps);
            Assert.Equal(SnapKind.Endpoint, snaps[0].Kind);
            Assert.Equal(vertex.Id, snaps[0].VertexId);
        }
    }

    [Fact]
    public void CrossingLines_ReturnSingleIntersectionSnapWhenNoVertexAtCrossing()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 50), new PointF(100, 50), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(50, 100), Tolerance);

        var crossing = new PointF(50, 50);
        var snaps = new SnapService().GetVisibleSnaps(document, crossing, UiTolerance);
        var intersectionSnaps = snaps.Where(snap => snap.Kind == SnapKind.Intersection).ToList();

        Assert.Single(intersectionSnaps);
        Assert.DoesNotContain(snaps, snap => snap.Kind == SnapKind.Endpoint);
    }

    [Fact]
    public void IntersectionAtExistingVertex_ReturnsOnlyVertexSnap()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 50), new PointF(100, 50), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(50, 0), new PointF(50, 100), template, Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);

        var centerVertex = document.Vertices
            .Single(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(50, 50), Tolerance));

        var snaps = new SnapService().GetVisibleSnaps(document, centerVertex.Position, UiTolerance);

        Assert.Single(snaps);
        Assert.Equal(SnapKind.Endpoint, snaps[0].Kind);
        Assert.Equal(centerVertex.Id, snaps[0].VertexId);
    }

    [Fact]
    public void MidpointAtExistingVertex_ReturnsOnlyVertexSnap()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(0, 50), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(0, 50), new PointF(0, 100), template, Tolerance);

        var midpointVertex = document.Vertices
            .Single(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 50), Tolerance));

        var snaps = new SnapService().GetVisibleSnaps(document, midpointVertex.Position, UiTolerance);

        Assert.Single(snaps);
        Assert.Equal(SnapKind.Endpoint, snaps[0].Kind);
        Assert.Equal(midpointVertex.Id, snaps[0].VertexId);
    }

    [Fact]
    public void OnEdgeAtExistingVertex_ReturnsOnlyVertexSnap()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, Tolerance);

        var startVertex = document.Vertices
            .Single(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tolerance));

        var snap = new SnapService().FindBestSnap(document, startVertex.Position, UiTolerance);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Endpoint, snap.Snap!.Value.Kind);
        Assert.Equal(startVertex.Id, snap.Snap.Value.VertexId);
    }

    [Fact]
    public void MultipleEdgePairsAtSameIntersection_ReturnSingleIntersectionSnap()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 50), new PointF(100, 50), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(50, 100), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 100), Tolerance);

        var crossing = new PointF(50, 50);
        var snaps = new SnapService().GetVisibleSnaps(document, crossing, UiTolerance);
        var intersectionSnaps = snaps.Where(snap => snap.Kind == SnapKind.Intersection).ToList();

        Assert.Single(intersectionSnaps);
    }

    [Fact]
    public void NormalMidpoint_IsPreservedWhenNotAtVertex()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, Tolerance);

        var midpoint = new PointF(50, 0);
        var snaps = new SnapService().GetVisibleSnaps(document, midpoint, UiTolerance);

        Assert.Contains(snaps, snap => snap.Kind == SnapKind.Midpoint);
    }

    [Fact]
    public void NormalOnEdgeSnap_IsPreservedWhenNotAtVertex()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, Tolerance);

        var onEdge = new PointF(25, 0);
        var snap = new SnapService().FindBestSnap(document, onEdge, UiTolerance);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.OnEdge, snap.Snap!.Value.Kind);
    }

    [Fact]
    public void AlignmentSnap_StillWorks()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(0, 100), template, Tolerance);

        var start = new PointF(0, 0);
        var target = new PointF(95, 4);
        var snapService = new SnapService();

        Assert.True(snapService.TrySnapDrawingAlignment(document, start, target, UiTolerance, out var aligned));
        Assert.Equal(100, aligned.X, 3);
        Assert.Equal(0, aligned.Y, 3);

        var visibleAlignment = snapService.FindVisibleDrawingAlignmentSnap(document, start, target, UiTolerance);
        Assert.NotNull(visibleAlignment);
        Assert.Equal(SnapKind.Alignment, visibleAlignment.Value.Kind);
    }

    [Fact]
    public void LineToolFlow_StartFromExistingCorner_ReturnsCanonicalVertexSnap()
    {
        var document = CreateSquare(UiTolerance);
        var corner = document.Vertices
            .Single(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tolerance));

        var snap = new SnapService().FindBestSnap(document, corner.Position, UiTolerance);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Endpoint, snap.Snap!.Value.Kind);
        Assert.Equal(corner.Id, snap.Snap.Value.VertexId);
        Assert.Equal(corner.Position, snap.Snap.Value.Position);
    }

    [Fact]
    public void FindBestSnap_PrefersVertexOverCloserMidpoint()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(0, 100), template, Tolerance);

        var corner = document.Vertices
            .Single(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tolerance));

        var cursor = new PointF(corner.Position.X + 2, corner.Position.Y + 2);
        var snap = new SnapService().FindBestSnap(document, cursor, UiTolerance);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Endpoint, snap.Snap!.Value.Kind);
        Assert.Equal(corner.Id, snap.Snap.Value.VertexId);
    }

    private static CadDocument CreateSquare(double tolerance)
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, tolerance);
        EdgeOperations.AddSegment(document, new PointF(100, 0), new PointF(100, 100), template, tolerance);
        EdgeOperations.AddSegment(document, new PointF(100, 100), new PointF(0, 100), template, tolerance);
        EdgeOperations.AddSegment(document, new PointF(0, 100), new PointF(0, 0), template, tolerance);
        PolygonBuilder.SyncFaces(document, tolerance);
        return document;
    }
}
