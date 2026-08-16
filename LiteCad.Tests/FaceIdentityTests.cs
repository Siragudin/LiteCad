using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using Xunit;

namespace LiteCad.Tests;

public class FaceIdentityTests
{
    private const double Tolerance = 1e-4;

    [Fact]
    public void CreateLoop_SameLoopWithCyclicallyShiftedStart_ProducesSameIdentity()
    {
        var document = CreateSquareDocument();
        var face = Assert.Single(document.Polygons);

        var originalKey = FaceIdentity.CreateLoop(document, face.OuterLoop);
        var shifted = RotateLoop(face.OuterLoop, shift: 2);

        Assert.Equal(originalKey, FaceIdentity.CreateLoop(document, shifted));
    }

    [Fact]
    public void CreateLoop_RecreatedEdgeIdsWithSameVertices_ProducesSameIdentity()
    {
        var document = CreateSquareDocument();
        var face = Assert.Single(document.Polygons);
        var originalKey = FaceIdentity.CreateLoop(document, face.OuterLoop);

        var removed = document.Edges[0];
        var start = TopologyService.GetEdgeStartPoint(document, removed);
        var end = TopologyService.GetEdgeEndPoint(document, removed);
        TopologyService.DeleteEdge(document, removed);
        TopologyService.PruneUnusedVertices(document);
        EdgeOperations.AddSegment(document, start, end, TestDocumentHelpers.CreateTemplate(), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);

        var rebuiltFace = Assert.Single(document.Polygons);
        var rebuiltKey = FaceIdentity.CreateLoop(document, rebuiltFace.OuterLoop);

        Assert.Equal(originalKey, rebuiltKey);
    }

    [Fact]
    public void ExplicitFaceSuppression_SyncFacesSkipsSuppressedOuterLoop()
    {
        var document = CreateSquareDocument();
        var face = Assert.Single(document.Polygons);

        PolygonBuilder.SuppressFaceGeometry(document, face, Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);

        Assert.Empty(document.Polygons);
        Assert.NotEmpty(document.SuppressedFaceGeometryKeys);
    }

    [Fact]
    public void TopologyChange_InvalidatesSuppressionKeys()
    {
        var document = CreateSquareDocument();
        var face = Assert.Single(document.Polygons);

        PolygonBuilder.SuppressFaceGeometry(document, face, Tolerance);
        PolygonBuilder.InvalidateSuppressionOnEdgeTopologyChange(document);

        Assert.Empty(document.SuppressedFaceGeometryKeys);
    }

    [Fact]
    public void CreateLoop_DifferentBoundaries_ProduceDifferentIdentities()
    {
        var square = CreateSquareDocument();
        var squareFace = Assert.Single(square.Polygons);
        var squareKey = FaceIdentity.CreateLoop(square, squareFace.OuterLoop);

        var triangle = new CadDocument();
        EdgeOperations.AddSegment(triangle, new PointF(0, 0), new PointF(2, 0), TestDocumentHelpers.CreateTemplate(), Tolerance);
        EdgeOperations.AddSegment(triangle, new PointF(2, 0), new PointF(1, 1), TestDocumentHelpers.CreateTemplate(), Tolerance);
        EdgeOperations.AddSegment(triangle, new PointF(1, 1), new PointF(0, 0), TestDocumentHelpers.CreateTemplate(), Tolerance);
        PolygonBuilder.SyncFaces(triangle, Tolerance);

        var triangleFace = Assert.Single(triangle.Polygons);
        var triangleKey = FaceIdentity.CreateLoop(triangle, triangleFace.OuterLoop);

        Assert.NotEqual(squareKey, triangleKey);
    }

    [Fact]
    public void CreateLoop_OuterAndInnerContours_AreDistinctIdentities()
    {
        var document = CreateSquareWithHoleDocument();

        Assert.Equal(2, document.Polygons.Count);
        var outerFace = document.Polygons.MaxBy(face => PolygonGeometry.GetArea(document, face, Tolerance))!;
        var innerFace = document.Polygons.MinBy(face => PolygonGeometry.GetArea(document, face, Tolerance))!;

        var outerKey = FaceIdentity.CreateLoop(document, outerFace.OuterLoop);
        var innerKey = FaceIdentity.CreateLoop(document, innerFace.OuterLoop);

        Assert.NotEqual(outerKey, innerKey);
        Assert.Equal(outerKey, FaceIdentity.Create(document, outerFace));
        Assert.Equal(innerKey, FaceIdentity.Create(document, innerFace));
    }

    [Fact]
    public void CreateRegion_InnerLoopOrder_DoesNotChangeRegionIdentity()
    {
        var document = CreateSquareWithTwoHolesDocument();
        Assert.Equal(3, document.Polygons.Count);

        var outerFace = document.Polygons.MaxBy(face => PolygonGeometry.GetArea(document, face, Tolerance))!;
        var holeFaces = document.Polygons
            .Where(face => !ReferenceEquals(face, outerFace))
            .OrderBy(face => FaceIdentity.CreateLoop(document, face.OuterLoop), StringComparer.Ordinal)
            .ToList();

        var identityA = FaceIdentity.CreateRegion(document, outerFace.OuterLoop, holeFaces.Select(face => face.OuterLoop));
        var identityB = FaceIdentity.CreateRegion(
            document,
            outerFace.OuterLoop,
            holeFaces.Select(face => face.OuterLoop).Reverse());

        Assert.Equal(identityA, identityB);
    }

    [Fact]
    public void SuppressThenDeleteRecreateEdge_RestoresFaceAfterTopologyInvalidation()
    {
        var document = CreateSquareDocument(out var removedEdge);
        var face = Assert.Single(document.Polygons);

        PolygonBuilder.SuppressFaceGeometry(document, face, Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        Assert.Empty(document.Polygons);

        var start = TopologyService.GetEdgeStartPoint(document, removedEdge);
        var end = TopologyService.GetEdgeEndPoint(document, removedEdge);
        TopologyService.DeleteEdge(document, removedEdge);
        TopologyService.PruneUnusedVertices(document);
        PolygonBuilder.InvalidateSuppressionOnEdgeTopologyChange(document);
        PolygonBuilder.SyncFaces(document, Tolerance);

        EdgeOperations.AddSegment(document, start, end, TestDocumentHelpers.CreateTemplate(), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);

        Assert.Single(document.Polygons);
    }

    private static CadDocument CreateSquareDocument()
        => CreateSquareDocument(out _);

    private static CadDocument CreateSquareDocument(out Edge lastEdge)
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(1, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(1, 0), new PointF(1, 1), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(1, 1), new PointF(0, 1), Tolerance);
        lastEdge = TestDocumentHelpers.AddEdge(document, new PointF(0, 1), new PointF(0, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }

    private static CadDocument CreateSquareWithHoleDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(4, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(4, 0), new PointF(4, 4), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(4, 4), new PointF(0, 4), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 4), new PointF(0, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(1, 1), new PointF(3, 1), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(3, 1), new PointF(3, 3), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(3, 3), new PointF(1, 3), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(1, 3), new PointF(1, 1), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }

    private static CadDocument CreateSquareWithTwoHolesDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(10, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(10, 0), new PointF(10, 10), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(10, 10), new PointF(0, 10), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 10), new PointF(0, 0), Tolerance);

        TestDocumentHelpers.AddEdge(document, new PointF(1, 1), new PointF(3, 1), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(3, 1), new PointF(3, 3), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(3, 3), new PointF(1, 3), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(1, 3), new PointF(1, 1), Tolerance);

        TestDocumentHelpers.AddEdge(document, new PointF(6, 6), new PointF(8, 6), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(8, 6), new PointF(8, 8), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(8, 8), new PointF(6, 8), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(6, 8), new PointF(6, 6), Tolerance);

        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }

    private static Loop RotateLoop(Loop source, int shift)
    {
        var rotated = new Loop();
        if (source.Edges.Count == 0)
        {
            return rotated;
        }

        shift %= source.Edges.Count;
        for (var i = 0; i < source.Edges.Count; i++)
        {
            rotated.Edges.Add(source.Edges[(i + shift) % source.Edges.Count]);
        }

        return rotated;
    }
}
