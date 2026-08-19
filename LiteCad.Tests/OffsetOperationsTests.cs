using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Services;
using Xunit;

namespace LiteCad.Tests;

public class OffsetOperationsTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void ComputeOffsetLoop_OffsetsSquareOutward()
    {
        var boundary = new[]
        {
            new PointF(0, 0),
            new PointF(4, 0),
            new PointF(4, 4),
            new PointF(0, 4)
        };

        var result = OffsetOperations.ComputeOffsetLoop(boundary, 1, Tol);

        Assert.Equal(OffsetValidationResult.Valid, result.Validation);
        Assert.Equal(4, result.Points.Count);
        Assert.True(MathUtils.ArePointsEqual(result.Points[0], new PointF(-1, -1), Tol));
        Assert.True(MathUtils.ArePointsEqual(result.Points[1], new PointF(5, -1), Tol));
        Assert.True(MathUtils.ArePointsEqual(result.Points[2], new PointF(5, 5), Tol));
        Assert.True(MathUtils.ArePointsEqual(result.Points[3], new PointF(-1, 5), Tol));
    }

    [Fact]
    public void ComputeOffsetLoop_OffsetsSquareInward()
    {
        var boundary = new[]
        {
            new PointF(0, 0),
            new PointF(4, 0),
            new PointF(4, 4),
            new PointF(0, 4)
        };

        var result = OffsetOperations.ComputeOffsetLoop(boundary, -1, Tol);

        Assert.Equal(OffsetValidationResult.Valid, result.Validation);
        Assert.Equal(4, result.Points.Count);
        Assert.True(MathUtils.ArePointsEqual(result.Points[0], new PointF(1, 1), Tol));
        Assert.True(MathUtils.ArePointsEqual(result.Points[3], new PointF(1, 3), Tol));
    }

    [Fact]
    public void ComputeOffsetLoop_RejectsZeroDistance()
    {
        var boundary = new[] { new PointF(0, 0), new PointF(1, 0), new PointF(0, 1) };

        var result = OffsetOperations.ComputeOffsetLoop(boundary, 0, Tol);

        Assert.Equal(OffsetValidationResult.ZeroDistance, result.Validation);
    }

    [Fact]
    public void ComputeOffsetLoop_RejectsExcessiveInwardOffset()
    {
        var boundary = new[]
        {
            new PointF(0, 0),
            new PointF(4, 0),
            new PointF(4, 4),
            new PointF(0, 4)
        };

        var result = OffsetOperations.ComputeOffsetLoop(boundary, -2, Tol);

        Assert.Equal(OffsetValidationResult.Degenerate, result.Validation);
    }

    [Fact]
    public void ComputeSignedDistance_UsesInsideOutsideSign()
    {
        var document = CreateSquareFaceDocument(out var face);

        var outside = OffsetOperations.ComputeSignedDistance(document, face, new PointF(2, -1), Tol);
        var inside = OffsetOperations.ComputeSignedDistance(document, face, new PointF(2, 0.5), Tol);

        Assert.True(outside > 0);
        Assert.True(inside < 0);
        Assert.True(Math.Abs(outside - 1) < 0.01);
        Assert.True(Math.Abs(inside + 0.5) < 0.01);
    }

    [Fact]
    public void ExecuteObjectOffset_PreservesOriginalFace()
    {
        var document = CreateSquareFaceDocument(out var face);
        var originalEdgeIds = face.OuterLoop.Edges.Select(reference => reference.EdgeId).ToHashSet();
        var selection = new Selection();

        var result = OffsetOperations.ExecuteObjectOffset(document, selection, face, 1, Tol);

        Assert.NotNull(result);
        Assert.Equal(8, document.Edges.Count);
        Assert.All(originalEdgeIds, edgeId => Assert.Contains(edgeId, document.Edges.Select(edge => edge.Id)));
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void ExecuteObjectOffset_CreatesNewFaceAndSelectsIt()
    {
        var document = CreateSquareFaceDocument(out var face);
        var selection = new Selection();

        var newFaceId = OffsetOperations.ExecuteObjectOffset(document, selection, face, 1, Tol);

        Assert.NotNull(newFaceId);
        Assert.NotEqual(face.Id, newFaceId);
        Assert.Single(selection.SelectedPolygonIds);
        Assert.Equal(newFaceId, selection.SelectedPolygonIds.Single());
        Assert.Equal(4, selection.SelectedEdgeIds.Count);

        var newFace = document.Polygons.Single(polygon => polygon.Id == newFaceId);
        Assert.Equal(36, PolygonGeometry.GetArea(document, newFace, Tol), 2);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void ExecuteObjectOffset_RejectsUnsupportedFaceWithHole()
    {
        var document = CreateSquareFaceDocument(out var face);
        face.InnerLoops.Add(new Loop());
        var selection = new Selection();

        var result = OffsetOperations.ExecuteObjectOffset(document, selection, face, 1, Tol);

        Assert.Null(result);
    }

    [Fact]
    public void FacePickOperations_PicksSmallestFace()
    {
        var document = CreateTwoSeparateFacesDocument();
        Assert.True(FacePickOperations.TryPickFaceAt(document, new PointF(0.5, 0.5), Tol, out var face));
        Assert.Equal(1, PolygonGeometry.GetArea(document, face, Tol), 2);
    }

    private static CadDocument CreateSquareFaceDocument(out Polygon face)
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(4, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(4, 0), new PointF(4, 4), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(4, 4), new PointF(0, 4), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 4), new PointF(0, 0), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        face = document.Polygons.Single();
        return document;
    }

    private static CadDocument CreateTwoSeparateFacesDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(1, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(1, 0), new PointF(1, 1), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(1, 1), new PointF(0, 1), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 1), new PointF(0, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(5, 5), new PointF(9, 5), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(9, 5), new PointF(9, 9), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(9, 9), new PointF(5, 9), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(5, 9), new PointF(5, 5), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        return document;
    }
}
