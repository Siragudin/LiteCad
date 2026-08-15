using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using Xunit;

namespace LiteCad.Tests;

public class FaceRestorationTests
{
    private const double Tolerance = 1e-4;

    [Fact]
    public void Test1_CreateSquare_FaceCountIsOne()
    {
        var document = SquareBuilder.CreateClosedSquare(out _);

        Assert.Equal(1, document.Polygons.Count);
        Assert.Equal(4, document.Edges.Count);
        Assert.Equal(4, document.Polygons[0].EdgeIds.Count);
    }

    [Fact]
    public void Test2_DeleteOneEdge_FaceCountIsZero()
    {
        var document = SquareBuilder.CreateClosedSquare(out var removed);
        SquareBuilder.DeleteEdge(document, removed, Tolerance);

        Assert.Equal(0, document.Polygons.Count);
        Assert.Equal(3, document.Edges.Count);
    }

    [Fact]
    public void Test3_DeleteEdgeRecreateSameEdge_FaceRestored()
    {
        var document = SquareBuilder.CreateClosedSquare(out var removed);
        var start = TopologyService.GetEdgeStartPoint(document, removed);
        var end = TopologyService.GetEdgeEndPoint(document, removed);

        SquareBuilder.DeleteEdge(document, removed, Tolerance);
        Assert.Equal(0, document.Polygons.Count);

        var recreated = SquareBuilder.RecreateEdge(document, start, end, Tolerance);

        Assert.Equal(1, document.Polygons.Count);
        Assert.Equal(4, document.Edges.Count);
        Assert.Equal(4, document.Polygons[0].EdgeIds.Count);
        Assert.Contains(document.Polygons[0].EdgeIds, edgeId => edgeId == recreated.Id);
    }

    [Fact]
    public void Test4_RecreateSameEdgeMultipleTimes_RemainsStable()
    {
        var document = SquareBuilder.CreateClosedSquare(out var removed);
        var start = TopologyService.GetEdgeStartPoint(document, removed);
        var end = TopologyService.GetEdgeEndPoint(document, removed);

        SquareBuilder.DeleteEdge(document, removed, Tolerance);

        Edge? lastEdge = null;
        for (var i = 0; i < 3; i++)
        {
            lastEdge = SquareBuilder.RecreateEdge(document, start, end, Tolerance);
        }

        Assert.Equal(1, document.Polygons.Count);
        Assert.Equal(4, document.Edges.Count);
        Assert.Equal(4, document.Polygons[0].EdgeIds.Count);
        Assert.NotNull(lastEdge);
        Assert.Contains(document.Polygons[0].EdgeIds, edgeId => edgeId == lastEdge.Id);
        Assert.Equal(4, document.Edges.Select(edge => edge.Id).Distinct().Count());
    }

    [Fact]
    public void Test5_SquareWithDiagonal_TwoFacesShareDiagonal()
    {
        var document = SquareBuilder.CreateClosedSquare(out _);
        var diagonal = SquareBuilder.AddDiagonal(document, Tolerance);

        Assert.Equal(2, document.Polygons.Count);
        Assert.All(document.Polygons, face => Assert.Equal(3, face.EdgeIds.Count));
        Assert.Equal(2, document.Polygons.Count(face => face.EdgeIds.Contains(diagonal.Id)));
        Assert.Single(document.Edges, edge => edge.Id == diagonal.Id);
    }

    [Fact]
    public void Test6_DeleteDiagonal_OneFaceRemains()
    {
        var document = SquareBuilder.CreateClosedSquare(out _);
        var diagonal = SquareBuilder.AddDiagonal(document, Tolerance);

        SquareBuilder.DeleteEdge(document, diagonal, Tolerance);

        Assert.Equal(1, document.Polygons.Count);
        Assert.Equal(4, document.Polygons[0].EdgeIds.Count);
        Assert.Equal(4, document.Edges.Count);
    }

    [Fact]
    public void Test7_DeleteEdgeRecreateThenDiagonal_TwoFaces()
    {
        var document = SquareBuilder.CreateClosedSquare(out var removed);
        var start = TopologyService.GetEdgeStartPoint(document, removed);
        var end = TopologyService.GetEdgeEndPoint(document, removed);

        SquareBuilder.DeleteEdge(document, removed, Tolerance);
        SquareBuilder.RecreateEdge(document, start, end, Tolerance);
        var diagonal = SquareBuilder.AddDiagonal(document, Tolerance);

        Assert.Equal(2, document.Polygons.Count);
        Assert.All(document.Polygons, face => Assert.Equal(3, face.EdgeIds.Count));
        Assert.Equal(2, document.Polygons.Count(face => face.EdgeIds.Contains(diagonal.Id)));
    }

    [Fact]
    public void ExplicitFaceDelete_WithUnchangedEdges_StaysSuppressedUntilEdgeTopologyChanges()
    {
        var document = SquareBuilder.CreateClosedSquare(out var removed);
        var face = Assert.Single(document.Polygons);
        document.SuppressedFaceGeometryKeys.Add(FaceIdentity.CreateLoop(document, face.OuterLoop));
        PolygonBuilder.SyncFaces(document, Tolerance);

        Assert.Equal(0, document.Polygons.Count);
        Assert.Equal(4, document.Edges.Count);

        SquareBuilder.DeleteEdge(document, removed, Tolerance);
        SquareBuilder.RecreateEdge(
            document,
            TopologyService.GetEdgeStartPoint(document, removed),
            TopologyService.GetEdgeEndPoint(document, removed),
            Tolerance);

        Assert.Equal(1, document.Polygons.Count);
    }
}

internal static class SquareBuilder
{
    public static CadDocument CreateClosedSquare(out Edge lastEdge)
    {
        var document = new CadDocument();
        AddEdge(document, new PointF(0, 0), new PointF(1, 0));
        AddEdge(document, new PointF(1, 0), new PointF(1, 1));
        AddEdge(document, new PointF(1, 1), new PointF(0, 1));
        lastEdge = AddEdge(document, new PointF(0, 1), new PointF(0, 0));
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }

    public static Edge AddDiagonal(CadDocument document, double tolerance)
    {
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(1, 1), template, tolerance);
        PolygonBuilder.SyncFaces(document, tolerance);
        return document.Edges.Single(edge =>
            Geometry2D.ArePointsSame(TopologyService.GetEdgeStartPoint(document, edge), new PointF(0, 0), tolerance) &&
            Geometry2D.ArePointsSame(TopologyService.GetEdgeEndPoint(document, edge), new PointF(1, 1), tolerance));
    }

    public static void DeleteEdge(CadDocument document, Edge edge, double tolerance)
    {
        TopologyService.DeleteEdge(document, edge);
        TopologyService.PruneUnusedVertices(document);
        PolygonBuilder.InvalidateSuppressionOnEdgeTopologyChange(document);
        PolygonBuilder.SyncFaces(document, tolerance);
    }

    public static Edge RecreateEdge(CadDocument document, PointF start, PointF end, double tolerance)
    {
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, start, end, template, tolerance);
        PolygonBuilder.SyncFaces(document, tolerance);
        return document.Edges.Single(candidate =>
            (Geometry2D.ArePointsSame(TopologyService.GetEdgeStartPoint(document, candidate), start, tolerance) &&
             Geometry2D.ArePointsSame(TopologyService.GetEdgeEndPoint(document, candidate), end, tolerance)) ||
            (Geometry2D.ArePointsSame(TopologyService.GetEdgeStartPoint(document, candidate), end, tolerance) &&
             Geometry2D.ArePointsSame(TopologyService.GetEdgeEndPoint(document, candidate), start, tolerance)));
    }

    private static Edge AddEdge(CadDocument document, PointF start, PointF end)
        => TestDocumentHelpers.AddEdge(document, start, end, Tolerance);

    private const double Tolerance = 1e-4;
}
