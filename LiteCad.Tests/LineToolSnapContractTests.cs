using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Services;
using Xunit;

namespace LiteCad.Tests;

public class LineToolSnapContractTests
{
    private const double Tolerance = 1e-4;
    private const double UiTolerance = 12.0;

    [Fact]
    public void SecondPoint_FarFromEdge_DoesNotSnapToOnEdge()
    {
        var document = CreateHorizontalEdge();
        var cursor = new PointF(60, 20);

        var snap = LineSnap(document, cursor);

        Assert.False(snap.HasSnap);
        Assert.Equal(cursor.X, snap.Resolve(cursor).X, 3);
        Assert.Equal(cursor.Y, snap.Resolve(cursor).Y, 3);
    }

    [Fact]
    public void SecondPoint_NearEdgeInterior_SnapsToOnEdgeProjection()
    {
        var document = CreateHorizontalEdge();
        var cursor = new PointF(35, 4);

        var snap = LineSnap(document, cursor);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.OnEdge, snap.Snap!.Value.Kind);
        Assert.Equal(35, snap.Snap.Value.Position.X, 3);
        Assert.Equal(0, snap.Snap.Value.Position.Y, 3);
    }

    [Fact]
    public void SecondPoint_NearStartEndpoint_SnapsToEndpoint()
    {
        var document = CreateHorizontalEdge();
        var start = TopologyService.GetEdgeStartPoint(document, document.Edges[0]);
        var cursor = new PointF(start.X + 2, start.Y + 2);

        var snap = LineSnap(document, cursor);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Endpoint, snap.Snap!.Value.Kind);
        Assert.Equal(start.X, snap.Snap.Value.Position.X, 3);
        Assert.Equal(start.Y, snap.Snap.Value.Position.Y, 3);
    }

    [Fact]
    public void SecondPoint_NearEndEndpoint_SnapsToEndpoint()
    {
        var document = CreateHorizontalEdge();
        var end = TopologyService.GetEdgeEndPoint(document, document.Edges[0]);
        var cursor = new PointF(end.X - 2, end.Y + 2);

        var snap = LineSnap(document, cursor);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Endpoint, snap.Snap!.Value.Kind);
        Assert.Equal(end.X, snap.Snap.Value.Position.X, 3);
        Assert.Equal(end.Y, snap.Snap.Value.Position.Y, 3);
    }

    [Fact]
    public void SecondPoint_AtMidpoint_UsesMidpointNotOnEdge()
    {
        var document = CreateHorizontalEdge();
        var midpoint = new PointF(50, 0);

        var snap = LineSnap(document, midpoint);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Midpoint, snap.Snap!.Value.Kind);
        Assert.Equal(midpoint.X, snap.Snap.Value.Position.X, 3);
        Assert.Equal(midpoint.Y, snap.Snap.Value.Position.Y, 3);
    }

    [Fact]
    public void SecondPoint_AtArbitraryInteriorPoint_SnapsToOnEdge()
    {
        var document = CreateHorizontalEdge();

        foreach (var x in new[] { 15f, 25f, 78f })
        {
            var cursor = new PointF(x, 3);
            var snap = LineSnap(document, cursor);

            Assert.True(snap.HasSnap);
            Assert.Equal(SnapKind.OnEdge, snap.Snap!.Value.Kind);
            Assert.Equal(x, snap.Snap.Value.Position.X, 3);
            Assert.Equal(0, snap.Snap.Value.Position.Y, 3);
        }
    }

    [Fact]
    public void SecondPoint_VertexPriorityWinsOverOnEdge()
    {
        var document = CreateSquare();
        var corner = document.Vertices
            .Single(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tolerance));
        var cursor = new PointF(corner.Position.X + 2, corner.Position.Y + 2);

        var snap = LineSnap(document, cursor);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Endpoint, snap.Snap!.Value.Kind);
        Assert.Equal(corner.Id, snap.Snap.Value.VertexId);
    }

    [Fact]
    public void FirstPoint_OnEdgeInterior_AllowsOnEdgeSnap()
    {
        var document = CreateHorizontalEdge();
        var cursor = new PointF(25, 0);

        var snap = LineSnap(document, cursor);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.OnEdge, snap.Snap!.Value.Kind);
        Assert.Equal(cursor.X, snap.Snap.Value.Position.X, 3);
        Assert.Equal(cursor.Y, snap.Snap.Value.Position.Y, 3);
    }

    [Fact]
    public void SecondPoint_NearSquareCorner_StillSnapsToEndpoint()
    {
        var document = CreateSquare();
        var corner = document.Vertices
            .Single(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tolerance));
        var cursor = new PointF(corner.Position.X + 2, corner.Position.Y + 2);

        var snap = LineSnap(document, cursor);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Endpoint, snap.Snap!.Value.Kind);
        Assert.Equal(corner.Id, snap.Snap.Value.VertexId);
    }

    [Fact]
    public void SecondPoint_NearIntersection_StillSnapsToIntersection()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 50), new PointF(100, 50), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(50, 100), Tolerance);

        var crossing = new PointF(50, 50);
        var cursor = new PointF(crossing.X + 1, crossing.Y + 1);

        var snap = LineSnap(document, cursor);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Intersection, snap.Snap!.Value.Kind);
        Assert.Equal(crossing.X, snap.Snap.Value.Position.X, 3);
        Assert.Equal(crossing.Y, snap.Snap.Value.Position.Y, 3);
    }

    [Fact]
    public void SecondPoint_AlignmentSnap_StillWorks()
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
    public void OnEdgeSnap_DoesNotCreateVertex()
    {
        var document = CreateHorizontalEdge();
        var vertexCountBefore = document.Vertices.Count;
        var cursor = new PointF(20, 3);

        var snap = LineSnap(document, cursor);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.OnEdge, snap.Snap!.Value.Kind);
        Assert.Equal(vertexCountBefore, document.Vertices.Count);
    }

    private static SnapResult LineSnap(CadDocument document, PointF cursor)
        => new SnapService().FindBestSnap(document, cursor, UiTolerance, includeOnEdge: true);

    private static CadDocument CreateHorizontalEdge()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, Tolerance);
        return document;
    }

    private static CadDocument CreateSquare()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(100, 0), new PointF(100, 100), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(100, 100), new PointF(0, 100), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(0, 100), new PointF(0, 0), template, Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }
}
