using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using Xunit;

namespace LiteCad.Tests;

public class DocumentHistoryTests
{
    private const double Tolerance = 12.0;

    [Fact]
    public void SquareThenDiagonal_Undo_RestoresOneValidFace()
    {
        var session = CreateSquareSession(out _);
        var document = session.Document;

        session.History.Record(document);
        AddDiagonal(document);

        Assert.True(session.History.Undo(document, Tolerance));

        Assert.Equal(4, document.Vertices.Count);
        Assert.Equal(4, document.Edges.Count);
        Assert.Single(document.Polygons);
        Assert.Equal(PolygonType.Face, document.Polygons[0].Type);
        Assert.Equal(4, document.Polygons[0].OuterLoop.Edges.Count);
        AssertNoDanglingLoopReferences(document);
        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void SquareThenDiagonal_UndoThenRedo_RestoresTwoValidFaces()
    {
        var session = CreateSquareSession(out _);
        var document = session.Document;

        session.History.Record(document);
        AddDiagonal(document);
        Assert.True(session.History.Undo(document, Tolerance));
        Assert.True(session.History.Redo(document, Tolerance));

        Assert.Equal(2, document.Polygons.Count(polygon => polygon.Type == PolygonType.Face));
        AssertNoDanglingLoopReferences(document);
        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void SquareThenDeleteEdge_Undo_RestoresFourEdgesAndOneFace()
    {
        var session = CreateSquareSession(out var edgeToDelete);
        var document = session.Document;

        session.History.Record(document);
        TopologyService.DeleteEdge(document, edgeToDelete);
        TopologyService.PruneUnusedVertices(document);
        PolygonBuilder.InvalidateSuppressionOnEdgeTopologyChange(document);
        PolygonBuilder.SyncFaces(document, Tolerance);

        Assert.Equal(3, document.Edges.Count);
        Assert.Empty(document.Polygons);

        Assert.True(session.History.Undo(document, Tolerance));

        Assert.Equal(4, document.Edges.Count);
        Assert.Single(document.Polygons);
        AssertNoDanglingLoopReferences(document);
        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void SplitTopology_Undo_HasNoDanglingLoopEdgeIds()
    {
        var session = CreateSquareSession(out _);
        var document = session.Document;
        var template = TestDocumentHelpers.CreateTemplate();

        session.History.Record(document);
        EdgeOperations.AddSegment(document, new PointF(50, 0), new PointF(50, 100), template, Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);

        Assert.Equal(7, document.Edges.Count);
        Assert.Equal(2, document.Polygons.Count);

        Assert.True(session.History.Undo(document, Tolerance));

        Assert.Equal(4, document.Edges.Count);
        Assert.Single(document.Polygons);
        AssertNoDanglingLoopReferences(document);
        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void Undo_RestoresSnapServiceCornerVertexId()
    {
        var session = CreateSquareSession(out _);
        var document = session.Document;
        var corner = document.Vertices
            .Single(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), MathUtils.DefaultTolerance));
        var cornerId = corner.Id;

        session.History.Record(document);
        AddDiagonal(document);
        Assert.True(session.History.Undo(document, Tolerance));

        var snap = new SnapService().FindBestSnap(document, corner.Position, Tolerance);
        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Endpoint, snap.Snap!.Value.Kind);
        Assert.Equal(cornerId, snap.Snap.Value.VertexId);
    }

    [Fact]
    public void UndoRedo_RebuildsFacesWithoutDuplicateOrDanglingReferences()
    {
        var session = CreateSquareSession(out _);
        var document = session.Document;

        var identityBefore = FaceIdentity.Create(document, document.Polygons[0]);

        session.History.Record(document);
        AddDiagonal(document);

        var identitiesWithDiagonal = document.Polygons
            .Where(polygon => polygon.Type == PolygonType.Face)
            .Select(polygon => FaceIdentity.Create(document, polygon))
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToArray();

        Assert.True(session.History.Undo(document, Tolerance));
        AssertNoDanglingLoopReferences(document);
        TopologyValidator.AssertValid(document, Tolerance);
        Assert.Equal(identityBefore, FaceIdentity.Create(document, document.Polygons[0]));

        Assert.True(session.History.Redo(document, Tolerance));
        AssertNoDanglingLoopReferences(document);
        TopologyValidator.AssertValid(document, Tolerance);

        var identitiesAfterRedo = document.Polygons
            .Where(polygon => polygon.Type == PolygonType.Face)
            .Select(polygon => FaceIdentity.Create(document, polygon))
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(identitiesWithDiagonal, identitiesAfterRedo);
        Assert.Equal(2, document.Polygons.Count(polygon => polygon.Type == PolygonType.Face));
    }

    [Fact]
    public void DerivedFaceIdsAreNotStableAcrossUndoRebuild()
    {
        var session = CreateSquareSession(out _);
        var document = session.Document;
        var faceIdBeforeUndo = document.Polygons[0].Id;

        session.History.Record(document);
        AddDiagonal(document);
        Assert.True(session.History.Undo(document, Tolerance));

        Assert.Single(document.Polygons);
        Assert.NotEqual(faceIdBeforeUndo, document.Polygons[0].Id);
        TopologyValidator.AssertValid(document, Tolerance);
    }

    private static CadSession CreateSquareSession(out Edge edgeToDelete)
    {
        var session = new CadSession();
        var document = session.Document;
        var template = TestDocumentHelpers.CreateTemplate();

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(100, 0), new PointF(100, 100), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(100, 100), new PointF(0, 100), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(0, 100), new PointF(0, 0), template, Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);

        edgeToDelete = document.Edges.Single(edge =>
            MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(document, edge), new PointF(0, 100), MathUtils.DefaultTolerance) &&
            MathUtils.ArePointsEqual(TopologyService.GetEdgeEndPoint(document, edge), new PointF(0, 0), MathUtils.DefaultTolerance));

        return session;
    }

    private static void AddDiagonal(CadDocument document)
    {
        EdgeOperations.AddSegment(
            document,
            new PointF(0, 0),
            new PointF(100, 100),
            TestDocumentHelpers.CreateTemplate(),
            Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
    }

    private static void AssertNoDanglingLoopReferences(CadDocument document)
    {
        var edgeIds = document.Edges.Select(edge => edge.Id).ToHashSet();
        foreach (var polygon in document.Polygons)
        {
            AssertLoopReferences(polygon.OuterLoop, edgeIds);
            foreach (var innerLoop in polygon.InnerLoops)
            {
                AssertLoopReferences(innerLoop, edgeIds);
            }
        }
    }

    private static void AssertLoopReferences(Loop loop, HashSet<Guid> edgeIds)
    {
        foreach (var reference in loop.Edges)
        {
            Assert.Contains(reference.EdgeId, edgeIds);
        }
    }
}
