using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Services;
using Xunit;

namespace LiteCad.Tests;

public class TopologyToleranceTests
{
    private const double Tol = MathUtils.DefaultTolerance;
    private const double UiTolerance = 12.0;

    [Fact]
    public void ParallelOffset12_IsNotCollinear()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, Tol);
        EdgeOperations.AddSegment(document, new PointF(0, 12), new PointF(120, 12), template, UiTolerance);

        Assert.False(Geometry2D.TryGetCollinearSegmentOverlap(
            new PointF(0, 12),
            new PointF(120, 12),
            new PointF(0, 0),
            new PointF(100, 0),
            Tol,
            out _,
            out _));

        Assert.Equal(2, document.Edges.Count);
        Assert.Contains(document.Edges, edge =>
            SegmentMatches(document, edge, new PointF(0, 0), new PointF(100, 0)));
        Assert.Contains(document.Edges, edge =>
            SegmentMatches(document, edge, new PointF(0, 12), new PointF(120, 12)));
        Assert.DoesNotContain(document.Vertices, vertex =>
            MathUtils.ArePointsEqual(vertex.Position, new PointF(100, 12), Tol));
    }

    [Fact]
    public void ParallelOffset1eMinus5_RemainsTopologyEquivalent()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        const double offset = 1e-5;

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, Tol);
        var edgesBefore = document.Edges.Count;
        var verticesBefore = document.Vertices.Count;

        EdgeOperations.AddSegment(document, new PointF(0, offset), new PointF(100, offset), template, UiTolerance);

        Assert.True(Geometry2D.AreSegmentsCollinear(
            new PointF(0, offset),
            new PointF(100, offset),
            new PointF(0, 0),
            new PointF(100, 0),
            Tol));

        Assert.Equal(edgesBefore, document.Edges.Count);
        Assert.InRange(document.Vertices.Count, verticesBefore, verticesBefore + 1);
    }

    [Fact]
    public void ParallelOffset20_NoOverlap()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, Tol);
        EdgeOperations.AddSegment(document, new PointF(0, 20), new PointF(120, 20), template, UiTolerance);

        Assert.Equal(2, document.Edges.Count);
        Assert.Single(document.Edges, edge =>
            SegmentMatches(document, edge, new PointF(0, 20), new PointF(120, 20)));
    }

    [Fact]
    public void CrossingLineStillSplits()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, Tol);
        EdgeOperations.AddSegment(document, new PointF(100, 0), new PointF(100, 100), template, Tol);
        EdgeOperations.AddSegment(document, new PointF(100, 100), new PointF(0, 100), template, Tol);
        EdgeOperations.AddSegment(document, new PointF(0, 100), new PointF(0, 0), template, Tol);
        PolygonBuilder.SyncFaces(document, TopologyTolerance.ForMutation);

        EdgeOperations.AddSegment(document, new PointF(0, 20), new PointF(120, 20), template, UiTolerance);
        PolygonBuilder.SyncFaces(document, TopologyTolerance.ForMutation);

        Assert.Contains(document.Vertices, vertex =>
            MathUtils.ArePointsEqual(vertex.Position, new PointF(100, 20), Tol));
        Assert.Contains(document.Edges, edge =>
            SegmentMatches(document, edge, new PointF(0, 20), new PointF(100, 20)));
        Assert.Contains(document.Edges, edge =>
            SegmentMatches(document, edge, new PointF(100, 20), new PointF(120, 20)));
        Assert.Equal(2, document.Polygons.Count(polygon => polygon.Type == PolygonType.Face));
    }

    [Fact]
    public void ExactSameLineOverlap_StillTrimsExisting()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(2, 0), Tol);

        var template = Edge.CreateStyleTemplate();
        EdgeOperations.AddSegment(document, new PointF(0.5, 0), new PointF(1.5, 0), template, UiTolerance);

        Assert.Equal(3, document.Edges.Count);
        Assert.Contains(document.Edges, edge =>
            SegmentMatches(document, edge, new PointF(0, 0), new PointF(0.5, 0)));
        Assert.Contains(document.Edges, edge =>
            SegmentMatches(document, edge, new PointF(0.5, 0), new PointF(1.5, 0)));
        Assert.Contains(document.Edges, edge =>
            SegmentMatches(document, edge, new PointF(1.5, 0), new PointF(2, 0)));
    }

    [Fact]
    public void OnEdgeUiSnap_StillUsesUiToleranceIndependently()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, Tol);

        var onEdge = new PointF(25, 0);
        var snap = new SnapService().FindBestSnap(document, onEdge, UiTolerance, includeOnEdge: true);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.OnEdge, snap.Snap!.Value.Kind);
        Assert.Equal(onEdge.X, snap.Snap.Value.Position.X, 3);
        Assert.Equal(onEdge.Y, snap.Snap.Value.Position.Y, 3);

        Assert.False(new SnapService().FindBestSnap(document, new PointF(25, 13), UiTolerance, includeOnEdge: true).HasSnap);
    }

    private static bool SegmentMatches(
        CadDocument document,
        Edge edge,
        PointF expectedStart,
        PointF expectedEnd)
    {
        var start = TopologyService.GetEdgeStartPoint(document, edge);
        var end = TopologyService.GetEdgeEndPoint(document, edge);
        return (MathUtils.ArePointsEqual(start, expectedStart, Tol) &&
                MathUtils.ArePointsEqual(end, expectedEnd, Tol)) ||
               (MathUtils.ArePointsEqual(start, expectedEnd, Tol) &&
                MathUtils.ArePointsEqual(end, expectedStart, Tol));
    }
}
