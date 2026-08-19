using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Services;
using Xunit;

namespace LiteCad.Tests;

public class OffsetTopologyTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void ScenarioA_NestedSquares_CreateIndependentFacesWithRingHole()
    {
        var document = new CadDocument();
        AddSquare(document, 0, 0, 4);
        AddSquare(document, 1, 1, 2);
        PolygonBuilder.SyncFaces(document, Tol);

        Assert.Equal(2, document.Polygons.Count);

        var outerFace = FindFaceByOuterArea(document, 16)!;
        var innerFace = FindFaceByOuterArea(document, 4)!;

        Assert.Empty(innerFace.InnerLoops);
        Assert.Single(outerFace.InnerLoops);
        Assert.Equal(
            FaceIdentity.CreateLoop(document, innerFace.OuterLoop),
            FaceIdentity.CreateLoop(document, outerFace.InnerLoops[0]));

        Assert.Equal(4.0, PolygonGeometry.GetArea(document, innerFace, Tol), 3);
        Assert.Equal(12.0, PolygonGeometry.GetArea(document, outerFace, Tol), 3);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void ScenarioB_OffsetOutwardNearSeparateFace_PreservesNeighborFace()
    {
        var document = new CadDocument();
        AddSquare(document, 0, 0, 4);
        AddSquare(document, 10, 0, 4);
        PolygonBuilder.SyncFaces(document, Tol);

        var sourceFace = PickFaceContaining(document, 2, 2)!;
        var neighborIdentity = FaceIdentity.Create(document, PickFaceContaining(document, 12, 2)!);
        var selection = new Selection();

        var result = OffsetOperations.ExecuteObjectOffset(document, selection, sourceFace, 1, Tol);

        Assert.NotNull(result);
        Assert.Equal(3, document.Polygons.Count);
        Assert.Equal(neighborIdentity, FaceIdentity.Create(document, PickFaceContaining(document, 12, 2)!));
        Assert.Equal(16, PolygonGeometry.GetArea(document, PickFaceContaining(document, 2, 2)!, Tol), 2);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void ScenarioC_OffsetCrossingExistingEdge_NoDuplicateEdges()
    {
        var document = CreateAdjacentSquaresDocument(out var leftFace, out _);
        var edgeCountBefore = document.Edges.Count;
        var selection = new Selection();

        var result = OffsetOperations.ExecuteObjectOffset(document, selection, leftFace, 1, Tol);

        Assert.NotNull(result);
        Assert.True(document.Edges.Count > edgeCountBefore);
        AssertNoDuplicateEdgeTopology(document);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void ScenarioD_AdjacentSquaresOffset_DoesNotMergeIntoSingleFace()
    {
        var document = CreateAdjacentSquaresDocument(out var leftFace, out _);
        var selection = new Selection();

        var result = OffsetOperations.ExecuteObjectOffset(document, selection, leftFace, 1, Tol);

        Assert.NotNull(result);
        Assert.Contains(
            document.Polygons,
            polygon => Math.Abs(PolygonGeometry.GetArea(document, polygon, Tol) - 16) < 0.01
                && PolygonGeometry.ContainsPoint(document, polygon, new PointF(2, 2), Tol));
        Assert.DoesNotContain(
            document.Polygons,
            polygon => Math.Abs(PolygonGeometry.GetArea(document, polygon, Tol) - 32) < 0.01);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void ScenarioE_InwardOffsetNearExistingFace_PreservesExistingFaceIdentities()
    {
        var document = new CadDocument();
        AddSquare(document, 0, 0, 4);
        AddSquare(document, 1, 1, 2);
        PolygonBuilder.SyncFaces(document, Tol);

        var outerIdentity = FaceIdentity.Create(document, FindFaceByOuterArea(document, 16)!);
        var innerIdentity = FaceIdentity.Create(document, FindFaceByOuterArea(document, 4)!);
        var innerFace = FindFaceByOuterArea(document, 4)!;
        var selection = new Selection();

        var result = OffsetOperations.ExecuteObjectOffset(document, selection, innerFace, -0.5, Tol);

        Assert.NotNull(result);
        Assert.Equal(3, document.Polygons.Count);
        Assert.Equal(outerIdentity, FaceIdentity.Create(document, FindFaceByOuterArea(document, 16)!));
        Assert.Equal(innerIdentity, FaceIdentity.Create(document, FindFaceByOuterArea(document, 4)!));
        Assert.NotNull(FindFaceByOuterArea(document, 1));
        TopologyValidator.AssertValid(document, Tol);
    }

    private static void AssertNoDuplicateEdgeTopology(CadDocument document)
    {
        for (var i = 0; i < document.Edges.Count; i++)
        {
            for (var j = i + 1; j < document.Edges.Count; j++)
            {
                var aStart = TopologyService.GetEdgeStartPoint(document, document.Edges[i]);
                var aEnd = TopologyService.GetEdgeEndPoint(document, document.Edges[i]);
                var bStart = TopologyService.GetEdgeStartPoint(document, document.Edges[j]);
                var bEnd = TopologyService.GetEdgeEndPoint(document, document.Edges[j]);

                var sameForward = Geometry2D.ArePointsSame(aStart, bStart, Tol)
                    && Geometry2D.ArePointsSame(aEnd, bEnd, Tol);
                var sameReverse = Geometry2D.ArePointsSame(aStart, bEnd, Tol)
                    && Geometry2D.ArePointsSame(aEnd, bStart, Tol);

                Assert.False(sameForward || sameReverse);
            }
        }
    }

    private static CadDocument CreateAdjacentSquaresDocument(out Polygon leftFace, out Polygon rightFace)
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(4, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(4, 0), new PointF(4, 4), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(4, 4), new PointF(0, 4), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 4), new PointF(0, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(4, 0), new PointF(8, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(8, 0), new PointF(8, 4), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(8, 4), new PointF(4, 4), Tol);
        PolygonBuilder.SyncFaces(document, Tol);

        leftFace = PickFaceContaining(document, 2, 2)!;
        rightFace = PickFaceContaining(document, 6, 2)!;
        return document;
    }

    private static void AddSquare(CadDocument document, float originX, float originY, float size)
    {
        TestDocumentHelpers.AddEdge(document, new PointF(originX, originY), new PointF(originX + size, originY), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(originX + size, originY), new PointF(originX + size, originY + size), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(originX + size, originY + size), new PointF(originX, originY + size), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(originX, originY + size), new PointF(originX, originY), Tol);
    }

    private static Polygon? FindFaceByOuterArea(CadDocument document, double outerArea)
        => document.Polygons.FirstOrDefault(face =>
            Math.Abs(Math.Abs(PolygonGeometry.GetSignedArea(document, face.OuterLoop)) - outerArea) < 0.01);

    private static Polygon? PickFaceContaining(CadDocument document, float x, float y)
    {
        var point = new PointF(x, y);
        return document.Polygons
            .Where(polygon => PolygonGeometry.ContainsPoint(document, polygon, point, Tol))
            .MinBy(polygon => PolygonGeometry.GetArea(document, polygon, Tol));
    }
}
