using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using Xunit;

namespace LiteCad.Tests;

public class ExtendToolTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void Extend_LineToIntersection_KeepsSingleEdgeWithNewLength()
    {
        var document = new CadDocument();
        var source = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(150, -10), new PointF(150, 10), Tol);
        PolygonBuilder.SyncFaces(document, Tol);

        Assert.True(ExtendOperations.TryBuildPlan(document, source.Id, new PointF(95, 0), Tol, out var plan));
        Assert.False(plan.ExtendStartVertex);
        Assert.True(ExtendOperations.ApplyExtend(document, plan));

        Assert.Single(document.Edges.Where(edge => edge.Id == source.Id));
        Assert.Equal(150, MathUtils.Distance(
            TopologyService.GetEdgeStartPoint(document, source),
            TopologyService.GetEdgeEndPoint(document, source)), 3);
        Assert.Equal(1, document.Edges.Count(edge =>
            Geometry2D.AreSegmentsCollinear(
                new PointF(0, 0),
                new PointF(150, 0),
                TopologyService.GetEdgeStartPoint(document, edge),
                TopologyService.GetEdgeEndPoint(document, edge),
                Tol)));
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void Extend_LeftEnd_ExtendsTowardNearestIntersection()
    {
        var document = new CadDocument();
        var source = TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(100, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, -10), new PointF(0, 10), Tol);

        Assert.True(ExtendOperations.TryBuildPlan(document, source.Id, new PointF(55, 0), Tol, out var plan));
        Assert.True(plan.ExtendStartVertex);
        Assert.True(ExtendOperations.ApplyExtend(document, plan));

        Assert.True(MathUtils.ArePointsEqual(
            TopologyService.GetEdgeStartPoint(document, source),
            new PointF(0, 0),
            Tol));
        Assert.True(MathUtils.ArePointsEqual(
            TopologyService.GetEdgeEndPoint(document, source),
            new PointF(100, 0),
            Tol));
    }

    [Fact]
    public void Extend_RightEnd_ExtendsTowardNearestIntersection()
    {
        var document = new CadDocument();
        var source = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(50, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(100, -10), new PointF(100, 10), Tol);

        Assert.True(ExtendOperations.TryBuildPlan(document, source.Id, new PointF(45, 0), Tol, out var plan));
        Assert.False(plan.ExtendStartVertex);
        Assert.True(ExtendOperations.ApplyExtend(document, plan));

        Assert.True(MathUtils.ArePointsEqual(
            TopologyService.GetEdgeEndPoint(document, source),
            new PointF(100, 0),
            Tol));
    }

    [Fact]
    public void Extend_BackwardIntersection_IsIgnored()
    {
        var document = new CadDocument();
        var source = TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(100, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, -10), new PointF(0, 10), Tol);

        Assert.False(ExtendOperations.TryBuildPlan(document, source.Id, new PointF(95, 0), Tol, out _));
    }

    [Fact]
    public void Extend_NoIntersection_LeavesGeometryUnchanged()
    {
        var document = new CadDocument();
        var source = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        var edgeCountBefore = document.Edges.Count;
        var startBefore = TopologyService.GetEdgeStartPoint(document, source);
        var endBefore = TopologyService.GetEdgeEndPoint(document, source);

        Assert.False(ExtendOperations.TryBuildPlan(document, source.Id, new PointF(95, 0), Tol, out _));

        Assert.Equal(edgeCountBefore, document.Edges.Count);
        Assert.True(MathUtils.ArePointsEqual(startBefore, TopologyService.GetEdgeStartPoint(document, source), Tol));
        Assert.True(MathUtils.ArePointsEqual(endBefore, TopologyService.GetEdgeEndPoint(document, source), Tol));
    }

    [Fact]
    public void Extend_PreservesEdgeId()
    {
        var document = new CadDocument();
        var source = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(150, -10), new PointF(150, 10), Tol);
        var originalId = source.Id;

        Assert.True(ExtendOperations.TryBuildPlan(document, source.Id, new PointF(95, 0), Tol, out var plan));
        Assert.True(ExtendOperations.ApplyExtend(document, plan));

        Assert.Contains(document.Edges, edge => edge.Id == originalId);
    }

    [Fact]
    public void Extend_DoesNotInsertIntermediateEdge()
    {
        var document = new CadDocument();
        var source = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(150, -10), new PointF(150, 10), Tol);

        Assert.True(ExtendOperations.TryBuildPlan(document, source.Id, new PointF(95, 0), Tol, out var plan));
        Assert.True(ExtendOperations.ApplyExtend(document, plan));

        Assert.Equal(3, document.Edges.Count);
        Assert.Single(document.Edges, edge =>
            edge.Id == source.Id
            && Geometry2D.AreSegmentsCollinear(
                new PointF(0, 0),
                new PointF(150, 0),
                TopologyService.GetEdgeStartPoint(document, edge),
                TopologyService.GetEdgeEndPoint(document, edge),
                Tol));
    }

    [Fact]
    public void Extend_UndoRestoresOriginalLength()
    {
        var session = new CadSession();
        var document = session.Document;
        var source = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(150, -10), new PointF(150, 10), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        var sourceId = source.Id;

        Assert.True(ExtendOperations.TryBuildPlan(document, sourceId, new PointF(95, 0), Tol, out var plan));
        session.History.Record(document);
        Assert.True(ExtendOperations.ApplyExtend(document, plan));
        var extended = document.Edges.First(edge => edge.Id == sourceId);
        Assert.Equal(150, MathUtils.Distance(
            TopologyService.GetEdgeStartPoint(document, extended),
            TopologyService.GetEdgeEndPoint(document, extended)), 3);

        Assert.True(session.History.Undo(document, Tol));
        var restored = document.Edges.First(edge => edge.Id == sourceId);
        Assert.Equal(100, MathUtils.Distance(
            TopologyService.GetEdgeStartPoint(document, restored),
            TopologyService.GetEdgeEndPoint(document, restored)), 3);
    }

    [Fact]
    public void Extend_RectangleEdge_PreservesTopology()
    {
        var document = CreateRectangle(0, 0, 4, 4);
        var bottom = FindHorizontalEdge(document, y: 0, minX: 0, maxX: 4);
        TestDocumentHelpers.AddEdge(document, new PointF(10, -5), new PointF(10, 5), Tol);
        PolygonBuilder.SyncFaces(document, Tol);

        Assert.True(ExtendOperations.TryBuildPlan(document, bottom.Id, new PointF(3.5f, 0), Tol, out var plan));
        Assert.False(plan.ExtendStartVertex);
        Assert.True(ExtendOperations.ApplyExtend(document, plan));

        Assert.True(MathUtils.ArePointsEqual(
            TopologyService.GetEdgeEndPoint(document, bottom),
            new PointF(10, 0),
            Tol));
        Assert.Equal(6, document.Edges.Count);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void Extend_KeepsExistingFaceValid()
    {
        var document = CreateRectangle(0, 0, 10, 10);
        var freeLine = TestDocumentHelpers.AddEdge(document, new PointF(0, -20), new PointF(50, -20), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(100, -30), new PointF(100, -10), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        Assert.Single(document.Polygons, polygon => polygon.Type == PolygonType.Face);

        Assert.True(ExtendOperations.TryBuildPlan(document, freeLine.Id, new PointF(45, -20), Tol, out var plan));
        Assert.True(ExtendOperations.ApplyExtend(document, plan));

        Assert.Single(document.Polygons, polygon => polygon.Type == PolygonType.Face);
        TopologyValidator.AssertValid(document, Tol);
    }

    private static CadDocument CreateRectangle(float minX, float minY, float maxX, float maxY)
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(minX, minY), new PointF(maxX, minY), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(maxX, minY), new PointF(maxX, maxY), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(maxX, maxY), new PointF(minX, maxY), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(minX, maxY), new PointF(minX, minY), Tol);
        return document;
    }

    private static Edge FindHorizontalEdge(CadDocument document, float y, float minX, float maxX)
        => document.Edges.First(edge =>
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            return Math.Abs(start.Y - y) < 0.01
                && Math.Abs(end.Y - y) < 0.01
                && Math.Abs(Math.Min(start.X, end.X) - minX) < 0.01
                && Math.Abs(Math.Max(start.X, end.X) - maxX) < 0.01;
        });
}
