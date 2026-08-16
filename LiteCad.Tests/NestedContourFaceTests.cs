using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using Xunit;

namespace LiteCad.Tests;

public class NestedContourFaceTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void A_SingleContour_OneSolidFace()
    {
        var document = CreateNestedDocument(1);

        Assert.Single(document.Polygons);
        var face = document.Polygons[0];
        Assert.Empty(face.InnerLoops);
        Assert.Equal(16.0, PolygonGeometry.GetArea(document, face, Tol), 3);
    }

    [Fact]
    public void B_NestedTwoContours_TwoSolidFaces()
    {
        var document = CreateNestedDocument(2);

        Assert.Equal(2, document.Polygons.Count);
        Assert.All(document.Polygons, face => Assert.Empty(face.InnerLoops));
        Assert.NotNull(FindFaceByArea(document, 16));
        Assert.NotNull(FindFaceByArea(document, 4));
    }

    [Fact]
    public void C_NestedThreeContours_ThreeSolidFaces()
    {
        var document = CreateNestedDocument(3);

        Assert.Equal(3, document.Polygons.Count);
        Assert.All(document.Polygons, face => Assert.Empty(face.InnerLoops));
        Assert.NotNull(FindFaceByArea(document, 16));
        Assert.NotNull(FindFaceByArea(document, 4));
        Assert.NotNull(FindFaceByArea(document, 1));
    }

    [Fact]
    public void D_NestedFourContours_FourSolidFaces()
    {
        var document = CreateNestedDocument(4);

        Assert.Equal(4, document.Polygons.Count);
        Assert.Equal(1, CountFacesByArea(document, 16));
        Assert.Equal(1, CountFacesByArea(document, 4));
        Assert.Equal(1, CountFacesByArea(document, 1));
        Assert.Equal(1, CountFacesByArea(document, 0.25));
    }

    [Fact]
    public void E_AddingB_DoesNotChangeFaceIdentityA()
    {
        var document = new CadDocument();
        AddSquare(document, 0, 0, 4);
        PolygonBuilder.SyncFaces(document, Tol);

        var faceA = Assert.Single(document.Polygons);
        var identityA = FaceIdentity.Create(document, faceA);

        AddSquare(document, 1, 1, 2);
        PolygonBuilder.SyncFaces(document, Tol);

        var faceAAfter = FindFaceByArea(document, 16);
        Assert.NotNull(faceAAfter);
        Assert.Equal(identityA, FaceIdentity.Create(document, faceAAfter!));
        Assert.Equal(2, document.Polygons.Count);
    }

    [Fact]
    public void F_AddingC_DoesNotChangeFaceIdentitiesAOrB()
    {
        var document = new CadDocument();
        AddSquare(document, 0, 0, 4);
        AddSquare(document, 1, 1, 2);
        PolygonBuilder.SyncFaces(document, Tol);

        var identityA = FaceIdentity.Create(document, FindFaceByArea(document, 16)!);
        var identityB = FaceIdentity.Create(document, FindFaceByArea(document, 4)!);

        AddSquare(document, 1.5f, 1.5f, 1);
        PolygonBuilder.SyncFaces(document, Tol);

        Assert.Equal(identityA, FaceIdentity.Create(document, FindFaceByArea(document, 16)!));
        Assert.Equal(identityB, FaceIdentity.Create(document, FindFaceByArea(document, 4)!));
        Assert.Equal(3, document.Polygons.Count);
    }

    [Fact]
    public void G_FaceAAreaRemains16_AfterBAndCAdded()
    {
        var document = new CadDocument();
        AddSquare(document, 0, 0, 4);
        PolygonBuilder.SyncFaces(document, Tol);
        Assert.Equal(16.0, PolygonGeometry.GetArea(document, FindFaceByArea(document, 16)!, Tol), 3);

        AddSquare(document, 1, 1, 2);
        PolygonBuilder.SyncFaces(document, Tol);
        Assert.Equal(16.0, PolygonGeometry.GetArea(document, FindFaceByArea(document, 16)!, Tol), 3);

        AddSquare(document, 1.5f, 1.5f, 1);
        PolygonBuilder.SyncFaces(document, Tol);
        Assert.Equal(16.0, PolygonGeometry.GetArea(document, FindFaceByArea(document, 16)!, Tol), 3);
    }

    [Fact]
    public void H_FaceBAreaRemains4_AfterCAdded()
    {
        var document = CreateNestedDocument(2);
        Assert.Equal(4.0, PolygonGeometry.GetArea(document, FindFaceByArea(document, 4)!, Tol), 3);

        AddSquare(document, 1.5f, 1.5f, 1);
        PolygonBuilder.SyncFaces(document, Tol);

        Assert.Equal(4.0, PolygonGeometry.GetArea(document, FindFaceByArea(document, 4)!, Tol), 3);
    }

    [Fact]
    public void I_SelectAAndBIndependently()
    {
        var document = CreateNestedDocument(2);

        var hitA = PickSmallestFace(document, 0.5f, 0.5f);
        var hitB = PickSmallestFace(document, 2f, 2f);

        Assert.NotNull(hitA);
        Assert.NotNull(hitB);
        Assert.NotEqual(hitA!.Id, hitB!.Id);
        Assert.Equal(16.0, PolygonGeometry.GetArea(document, hitA, Tol), 3);
        Assert.Equal(4.0, PolygonGeometry.GetArea(document, hitB, Tol), 3);
    }

    [Fact]
    public void J_DeleteB_DoesNotDeleteA()
    {
        var document = CreateNestedDocument(2);
        var faceB = FindFaceByArea(document, 4)!;

        PolygonBuilder.SuppressFaceGeometry(document, faceB, Tol);
        PolygonBuilder.SyncFaces(document, Tol);

        Assert.Single(document.Polygons);
        Assert.NotNull(FindFaceByArea(document, 16));
        Assert.Null(FindFaceByArea(document, 4));
    }

    [Fact]
    public void K_DeleteA_DoesNotDeleteB()
    {
        var document = CreateNestedDocument(2);
        var faceA = FindFaceByArea(document, 16)!;

        PolygonBuilder.SuppressFaceGeometry(document, faceA, Tol);
        PolygonBuilder.SyncFaces(document, Tol);

        Assert.Single(document.Polygons);
        Assert.NotNull(FindFaceByArea(document, 4));
        Assert.Null(FindFaceByArea(document, 16));
    }

    [Fact]
    public void L_RepeatedSync_PreservesFaceIdentitySet()
    {
        var document = CreateNestedDocument(3);
        var identities = document.Polygons
            .Select(face => FaceIdentity.Create(document, face))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        PolygonBuilder.SyncFaces(document, Tol);
        PolygonBuilder.SyncFaces(document, Tol);

        var resynced = document.Polygons
            .Select(face => FaceIdentity.Create(document, face))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(identities, resynced);
    }

    [Fact]
    public void M_UndoRedoNestedContours_PreservesFaceCountAndIdentities()
    {
        var session = new CadSession();
        var document = session.Document;

        AddSquare(document, 0, 0, 4);
        PolygonBuilder.SyncFaces(document, Tol);
        var identityA = FaceIdentity.Create(document, FindFaceByArea(document, 16)!);

        session.History.Record(document);
        AddSquare(document, 1, 1, 2);
        PolygonBuilder.SyncFaces(document, Tol);

        Assert.Equal(2, document.Polygons.Count);
        Assert.True(session.History.Undo(document, Tol));
        Assert.Single(document.Polygons);
        Assert.Equal(identityA, FaceIdentity.Create(document, document.Polygons[0]));

        Assert.True(session.History.Redo(document, Tol));
        Assert.Equal(2, document.Polygons.Count);
        Assert.Equal(identityA, FaceIdentity.Create(document, FindFaceByArea(document, 16)!));
    }

    [Fact]
    public void ContainmentTree_IsDerivedAndMatchesNestedStructure()
    {
        var document = CreateNestedDocument(3);
        var parentMap = ContourContainment.BuildParentMap(document, Tol);

        var identityA = FaceIdentity.Create(document, FindFaceByArea(document, 16)!);
        var identityB = FaceIdentity.Create(document, FindFaceByArea(document, 4)!);
        var identityC = FaceIdentity.Create(document, FindFaceByArea(document, 1)!);

        Assert.Null(parentMap[identityA]);
        Assert.Equal(identityA, parentMap[identityB]);
        Assert.Equal(identityB, parentMap[identityC]);
    }

    [Fact]
    public void TwoIndependentOuterContours_TwoSolidFaces()
    {
        var document = new CadDocument();
        AddSquare(document, 0, 0, 4);
        AddSquare(document, 10, 0, 2);
        PolygonBuilder.SyncFaces(document, Tol);

        Assert.Equal(2, document.Polygons.Count);
        Assert.All(document.Polygons, face => Assert.Empty(face.InnerLoops));
    }

    private static CadDocument CreateNestedDocument(int levels)
    {
        var document = new CadDocument();
        var origin = 0f;
        var size = 4f;

        for (var level = 0; level < levels; level++)
        {
            AddSquare(document, origin, origin, size);
            origin += size * 0.25f;
            size *= 0.5f;
        }

        PolygonBuilder.SyncFaces(document, Tol);
        return document;
    }

    private static void AddSquare(CadDocument document, float originX, float originY, float size)
    {
        TestDocumentHelpers.AddEdge(document, new PointF(originX, originY), new PointF(originX + size, originY), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(originX + size, originY), new PointF(originX + size, originY + size), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(originX + size, originY + size), new PointF(originX, originY + size), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(originX, originY + size), new PointF(originX, originY), Tol);
    }

    private static Polygon? FindFaceByArea(CadDocument document, double outerArea)
        => document.Polygons.FirstOrDefault(face =>
            Math.Abs(Math.Abs(PolygonGeometry.GetSignedArea(document, face.OuterLoop)) - outerArea) < 0.01);

    private static int CountFacesByArea(CadDocument document, double outerArea)
        => document.Polygons.Count(face =>
            Math.Abs(Math.Abs(PolygonGeometry.GetSignedArea(document, face.OuterLoop)) - outerArea) < 0.01);

    private static Polygon? PickSmallestFace(CadDocument document, float x, float y)
    {
        var point = new PointF(x, y);
        Polygon? closest = null;
        var closestArea = double.MaxValue;

        foreach (var polygon in document.Polygons)
        {
            if (polygon.OuterLoop.Edges.Count < 3 ||
                !PolygonGeometry.ContainsPoint(document, polygon, point, Tol))
            {
                continue;
            }

            var area = PolygonGeometry.GetArea(document, polygon, Tol);
            if (area >= closestArea)
            {
                continue;
            }

            closest = polygon;
            closestArea = area;
        }

        return closest;
    }
}
