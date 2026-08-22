using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Services;
using Xunit;

namespace LiteCad.Tests;

public class SnapContactPointTests
{
    private const double GeometryTolerance = 1e-4;
    private const double SnapTolerance = 12.0;

    [Fact]
    public void LineLine_EndpointToEndpoint_ProducesSingleSnap()
    {
        var document = CreateCollinearJointWithoutSharedVertex();
        var joint = new PointF(100, 0);

        var snaps = QuerySnaps(document, joint);

        Assert.Single(snaps);
        Assert.True(
            snaps[0].Kind is SnapKind.Intersection or SnapKind.Endpoint,
            $"Expected intersection or endpoint snap, got {snaps[0].Kind}");
    }

    [Fact]
    public void LineLine_EndpointToInterior_ProducesIntersectionSnap()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), GeometryTolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(50, 50), GeometryTolerance);

        var touch = new PointF(50, 0);
        var snap = FindBest(document, touch);

        Assert.True(snap.HasSnap);
        Assert.True(
            snap.Snap!.Value.Kind is SnapKind.Intersection or SnapKind.Endpoint,
            $"Expected intersection or endpoint snap, got {snap.Snap.Value.Kind}");
    }

    [Fact]
    public void LineLine_InteriorToEndpoint_ProducesIntersectionSnap()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(50, 50), GeometryTolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), GeometryTolerance);

        var touch = new PointF(50, 0);
        var snap = FindBest(document, touch);

        Assert.True(snap.HasSnap);
        Assert.True(
            snap.Snap!.Value.Kind is SnapKind.Intersection or SnapKind.Endpoint,
            $"Expected intersection or endpoint snap, got {snap.Snap.Value.Kind}");
    }

    [Fact]
    public void LineLine_InteriorToInterior_ProducesIntersectionSnap()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 50), new PointF(100, 50), GeometryTolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(50, 100), GeometryTolerance);

        var crossing = new PointF(50, 50);
        var snap = FindBest(document, crossing);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Intersection, snap.Snap!.Value.Kind);
    }

    [Fact]
    public void LineAxis_EndpointTouchesAxis_ProducesSnap()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(50, 50), GeometryTolerance);
        AxisService.Create(document, new PointF(0, 0), new PointF(100, 0), GeometryTolerance);

        var touch = new PointF(50, 0);
        var snap = FindBest(document, touch);

        Assert.True(snap.HasSnap);
        Assert.True(
            snap.Snap!.Value.Kind is SnapKind.Intersection or SnapKind.Endpoint,
            $"Expected intersection or endpoint snap, got {snap.Snap.Value.Kind}");
    }

    [Fact]
    public void LineAxis_AxisEndpointToLineInterior_ProducesSnap()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), GeometryTolerance);
        AxisService.Create(document, new PointF(50, 0), new PointF(50, 80), GeometryTolerance);

        var touch = new PointF(50, 0);
        var snap = FindBest(document, touch);

        Assert.True(snap.HasSnap);
        Assert.True(
            snap.Snap!.Value.Kind is SnapKind.Intersection or SnapKind.Endpoint,
            $"Expected intersection or endpoint snap, got {snap.Snap.Value.Kind}");
    }

    [Fact]
    public void LineAxis_AxisInteriorToLineEndpoint_ProducesSnap()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(50, 50), GeometryTolerance);
        AxisService.Create(document, new PointF(0, 0), new PointF(100, 0), GeometryTolerance);

        var touch = new PointF(50, 0);
        var snap = FindBest(document, touch);

        Assert.True(snap.HasSnap);
        Assert.True(
            snap.Snap!.Value.Kind is SnapKind.Intersection or SnapKind.Endpoint,
            $"Expected intersection or endpoint snap, got {snap.Snap.Value.Kind}");
    }

    [Fact]
    public void AxisAxis_TouchPoint_ProducesAxisIntersectionSnap()
    {
        var document = new CadDocument();
        AxisService.Create(document, new PointF(0, 0), new PointF(100, 0), GeometryTolerance);
        AxisService.Create(document, new PointF(50, 0), new PointF(50, 80), GeometryTolerance);

        var touch = new PointF(50, 0);
        var snap = FindBest(document, touch);

        Assert.True(snap.HasSnap);
        Assert.True(
            snap.Snap!.Value.Kind is SnapKind.AxisIntersection or SnapKind.Endpoint,
            $"Expected axis intersection or endpoint snap, got {snap.Snap.Value.Kind}");
    }

    [Fact]
    public void CollinearSegments_EndToEndJoint_ProducesSnap()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), GeometryTolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(200, 0), GeometryTolerance);

        var joint = new PointF(100, 0);
        var snap = FindBest(document, joint);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Endpoint, snap.Snap!.Value.Kind);
    }

    [Fact]
    public void CollinearSegments_WithoutSharedVertex_ProducesSingleSnapAtJoint()
    {
        var document = CreateCollinearJointWithoutSharedVertex();
        var joint = new PointF(100, 0);

        var snaps = QuerySnaps(document, joint);

        Assert.Single(snaps);
    }

    [Fact]
    public void MultipleSourcesAtSamePoint_DeduplicateToSingleCandidate()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 50), new PointF(100, 50), GeometryTolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(50, 100), GeometryTolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 100), GeometryTolerance);

        var crossing = new PointF(50, 50);
        var snaps = QuerySnaps(document, crossing);
        var intersectionSnaps = snaps.Where(snap => snap.Kind == SnapKind.Intersection).ToList();

        Assert.Single(intersectionSnaps);
    }

    [Fact]
    public void AxisTouch_DoesNotCreateTopology()
    {
        var document = new CadDocument();
        var vertexCountBefore = document.Vertices.Count;
        var edgeCountBefore = document.Edges.Count;

        AxisService.Create(document, new PointF(0, 0), new PointF(100, 0), GeometryTolerance);
        AxisService.Create(document, new PointF(50, 0), new PointF(50, 80), GeometryTolerance);

        _ = new SnapService().FindBestSnap(document, new PointF(50, 0), SnapTolerance);

        Assert.Equal(vertexCountBefore, document.Vertices.Count);
        Assert.Equal(edgeCountBefore, document.Edges.Count);
        Assert.Empty(document.Edges);
    }

    [Fact]
    public void OffsetEdgeTouchingAxis_ProducesSnap()
    {
        var document = new CadDocument();
        AxisService.Create(document, new PointF(0, 0), new PointF(100, 0), GeometryTolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(50, 50), GeometryTolerance);

        var touch = new PointF(50, 0);
        var snap = FindBest(document, touch);

        Assert.True(snap.HasSnap);
    }

    [Fact]
    public void OffsetEdgeTouchingAnotherEdge_ProducesSnap()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), GeometryTolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(50, 50), GeometryTolerance);

        var touch = new PointF(50, 0);
        var snap = FindBest(document, touch);

        Assert.True(snap.HasSnap);
    }

    [Fact]
    public void ExactInputLineEndingOnExistingGeometry_ProducesSnap()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, GeometryTolerance);
        EdgeOperations.AddSegment(document, new PointF(100, 0), new PointF(100, 50), template, GeometryTolerance);

        var joint = new PointF(100, 0);
        var snap = FindBest(document, joint);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Endpoint, snap.Snap!.Value.Kind);
    }

    [Fact]
    public void TouchCandidates_AreBuiltDuringCacheRebuild_NotPerMouseMove()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 50), new PointF(100, 50), GeometryTolerance);

        var service = new SnapService();
        var crossing = new PointF(80, 50);

        var beforeSecondEdge = service.GetVisibleSnaps(document, crossing, SnapTolerance);
        Assert.Empty(beforeSecondEdge);

        TestDocumentHelpers.AddEdge(document, new PointF(80, 0), new PointF(80, 100), GeometryTolerance);

        var afterSecondEdge = service.GetVisibleSnaps(document, crossing, SnapTolerance);
        var intersectionSnaps = afterSecondEdge.Where(snap => snap.Kind == SnapKind.Intersection).ToList();
        Assert.Single(intersectionSnaps);
    }

    [Fact]
    public void PointOnLineSnap_StillWorksNearNonTouchSegment()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, GeometryTolerance);

        var cursor = new PointF(25, 3);
        var snap = new SnapService().FindBestSnap(document, cursor, SnapTolerance, includeOnEdge: true);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.OnEdge, snap.Snap!.Value.Kind);
    }

    [Fact]
    public void TouchPoint_HasPriorityOverOnEdgeNearSameLocation()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 50), new PointF(100, 50), GeometryTolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(50, 100), GeometryTolerance);

        var cursor = new PointF(50, 50);
        var snap = new SnapService().FindBestSnap(document, cursor, SnapTolerance, includeOnEdge: true);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Intersection, snap.Snap!.Value.Kind);
    }

    private static SnapResult FindBest(CadDocument document, PointF point)
        => new SnapService().FindBestSnap(document, point, SnapTolerance);

    private static IReadOnlyList<SnapPoint> QuerySnaps(CadDocument document, PointF point)
        => new SnapService().GetVisibleSnaps(document, point, SnapTolerance);

    private static CadDocument CreateCollinearJointWithoutSharedVertex()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        var startA = new Vertex(new PointF(0, 0));
        var endA = new Vertex(new PointF(100, 0));
        var startB = new Vertex(new PointF(100, 0));
        var endB = new Vertex(new PointF(200, 0));
        document.Vertices.AddRange([startA, endA, startB, endB]);

        TopologyService.CreateEdge(document, startA.Id, endA.Id, template);
        TopologyService.CreateEdge(document, startB.Id, endB.Id, template);
        return document;
    }
}
