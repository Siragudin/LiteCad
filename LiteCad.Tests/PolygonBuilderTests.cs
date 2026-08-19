using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using Xunit;

namespace LiteCad.Tests;

public class PolygonBuilderTests
{
    private const double Tolerance = 1e-4;

    [Fact]
    public void SyncFaces_SimpleSquare_CreatesOneFaceWithFourEdges()
    {
        var document = CreateSquare(out _, out _, out _, out _);

        PolygonBuilder.SyncFaces(document, Tolerance);

        var face = Assert.Single(document.Polygons);
        Assert.Equal(PolygonType.Face, face.Type);
        Assert.Equal(4, face.EdgeIds.Count);
        Assert.Empty(face.HoleEdgeIds);
        Assert.Equal(1.0, PolygonGeometry.GetArea(document, face, Tolerance), 3);
    }

    [Fact]
    public void SyncFaces_SquareWithDiagonal_CreatesTwoFacesSharingDiagonalEdge()
    {
        var document = CreateSquare(out var ab, out var bc, out _, out var da);
        var diagonal = AddEdge(document, TopologyService.GetEdgeEndPoint(document, da), TopologyService.GetEdgeEndPoint(document, bc));

        PolygonBuilder.SyncFaces(document, Tolerance);

        Assert.Equal(2, document.Polygons.Count);

        var faces = document.Polygons.OrderBy(face => PolygonGeometry.GetArea(document, face, Tolerance)).ToList();
        Assert.All(faces, face => Assert.Equal(3, face.EdgeIds.Count));
        Assert.All(faces, face => Assert.Equal(0.5, PolygonGeometry.GetArea(document, face, Tolerance), 3));

        Assert.Contains(faces[0].EdgeIds, edgeId => edgeId == diagonal.Id);
        Assert.Contains(faces[1].EdgeIds, edgeId => edgeId == diagonal.Id);
        Assert.Single(document.Edges, edge => edge.Id == diagonal.Id);

        Assert.DoesNotContain(
            document.Polygons,
            face => face.EdgeIds.Count == 4);
    }

    [Fact]
    public void SyncFaces_InteriorLineWithoutBoundary_DoesNotCreateExtraFace()
    {
        var document = CreateSquare(out _, out _, out _, out _);
        var interior = AddEdge(document, new PointF(0.25, 0.25), new PointF(0.75, 0.75));

        PolygonBuilder.SyncFaces(document, Tolerance);

        var face = Assert.Single(document.Polygons);
        Assert.Equal(4, face.EdgeIds.Count);
        Assert.DoesNotContain(face.EdgeIds, edgeId => edgeId == interior.Id);
    }

    [Fact]
    public void SyncFaces_TwoAdjacentSquares_CreatesTwoFacesWithSharedEdge()
    {
        var document = new CadDocument();

        var ab = AddEdge(document, new PointF(0, 0), new PointF(1, 0));
        var bc = AddEdge(document, new PointF(1, 0), new PointF(1, 1));
        AddEdge(document, new PointF(1, 1), new PointF(0, 1));
        AddEdge(document, new PointF(0, 1), new PointF(0, 0));

        AddEdge(document, new PointF(1, 0), new PointF(2, 0));
        AddEdge(document, new PointF(2, 0), new PointF(2, 1));
        AddEdge(document, new PointF(2, 1), new PointF(1, 1));

        PolygonBuilder.SyncFaces(document, Tolerance);

        Assert.Equal(2, document.Polygons.Count);
        Assert.All(document.Polygons, face => Assert.Equal(4, face.EdgeIds.Count));
        Assert.All(document.Polygons, face => Assert.Equal(1.0, PolygonGeometry.GetArea(document, face, Tolerance), 3));

        Assert.Equal(2, document.Polygons.Count(face => face.EdgeIds.Contains(bc.Id)));
        Assert.Single(document.Edges, edge => edge.Id == bc.Id);
    }

    [Fact]
    public void SyncFaces_SquareWithInnerSquare_CreatesNestedFaceWithHole()
    {
        var document = CreateSquareWithInnerSquareDocument();

        PolygonBuilder.SyncFaces(document, Tolerance);

        Assert.Equal(2, document.Polygons.Count);

        var smallFace = FindFaceByOuterArea(document, 4)!;
        var largeFace = FindFaceByOuterArea(document, 16)!;

        Assert.Empty(smallFace.InnerLoops);
        Assert.Single(largeFace.InnerLoops);
        Assert.Equal(
            FaceIdentity.CreateLoop(document, smallFace.OuterLoop),
            FaceIdentity.CreateLoop(document, largeFace.InnerLoops[0]));

        Assert.Equal(4.0, PolygonGeometry.GetArea(document, smallFace, Tolerance), 3);
        Assert.Equal(12.0, PolygonGeometry.GetArea(document, largeFace, Tolerance), 3);
        Assert.Equal(16.0, document.Polygons.Sum(face => PolygonGeometry.GetArea(document, face, Tolerance)), 3);
        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void SyncFaces_TwoTrianglesFromDiagonal_SumAreaEqualsSquareArea()
    {
        var document = CreateSquare(out _, out _, out _, out _);
        AddEdge(document, new PointF(0, 0), new PointF(1, 1));

        PolygonBuilder.SyncFaces(document, Tolerance);

        var totalArea = document.Polygons.Sum(face => PolygonGeometry.GetArea(document, face, Tolerance));
        Assert.Equal(1.0, totalArea, 3);
    }

    [Fact]
    public void SyncFaces_SuppressedFaceGeometryKey_IsNotRecreated()
    {
        var document = CreateSquare(out _, out _, out _, out _);
        PolygonBuilder.SyncFaces(document, Tolerance);

        var face = Assert.Single(document.Polygons);
        PolygonBuilder.SuppressFaceGeometry(document, face, Tolerance);

        PolygonBuilder.SyncFaces(document, Tolerance);

        Assert.Empty(document.Polygons);
        Assert.Equal(4, document.Edges.Count);
    }

    private static CadDocument CreateSquare(
        out Edge ab,
        out Edge bc,
        out Edge cd,
        out Edge da)
    {
        var document = new CadDocument();
        ab = AddEdge(document, new PointF(0, 0), new PointF(1, 0));
        bc = AddEdge(document, new PointF(1, 0), new PointF(1, 1));
        cd = AddEdge(document, new PointF(1, 1), new PointF(0, 1));
        da = AddEdge(document, new PointF(0, 1), new PointF(0, 0));
        return document;
    }

    private static Edge AddEdge(CadDocument document, PointF start, PointF end)
        => TestDocumentHelpers.AddEdge(document, start, end, Tolerance);

    private static CadDocument CreateSquareWithInnerSquareDocument()
    {
        var document = new CadDocument();

        AddEdge(document, new PointF(0, 0), new PointF(4, 0));
        AddEdge(document, new PointF(4, 0), new PointF(4, 4));
        AddEdge(document, new PointF(4, 4), new PointF(0, 4));
        AddEdge(document, new PointF(0, 4), new PointF(0, 0));

        AddEdge(document, new PointF(1, 1), new PointF(3, 1));
        AddEdge(document, new PointF(3, 1), new PointF(3, 3));
        AddEdge(document, new PointF(3, 3), new PointF(1, 3));
        AddEdge(document, new PointF(1, 3), new PointF(1, 1));

        return document;
    }

    private static Polygon? FindFaceByOuterArea(CadDocument document, double outerArea)
        => document.Polygons.FirstOrDefault(face =>
            Math.Abs(Math.Abs(PolygonGeometry.GetSignedArea(document, face.OuterLoop)) - outerArea) < 0.01);
}
