using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Services;
using Xunit;

namespace LiteCad.Tests;

public class CadDocumentIndexTests
{
    private const double Tolerance = 1e-4;
    private const double UiTolerance = 12.0;

    [Fact]
    public void FindVertex_ReturnsSameVertexAsListOrderWhenMultipleWithinTolerance()
    {
        var document = new CadDocument();
        var first = new Vertex(new PointF(10, 10));
        var second = new Vertex(new PointF(10.00005, 10.00005));
        document.AddVertices([first, second]);

        var found = TopologyService.FindVertex(document, new PointF(10, 10), Tolerance);

        Assert.NotNull(found);
        Assert.Equal(first.Id, found!.Id);
    }

    [Fact]
    public void TryGetVertexAndEdge_AreAvailableAfterBulkLoad()
    {
        var source = CreateSquareDocument();
        var json = ProjectDocumentSerializer.Serialize(source, LinearDisplayUnit.Millimeters);
        var dto = ProjectDocumentSerializer.Deserialize(json);

        var loaded = new CadDocument();
        ProjectDocumentSerializer.Apply(loaded, dto);

        Assert.True(loaded.Index.TryGetVertex(loaded.Vertices[0].Id, out _));
        Assert.True(loaded.Index.TryGetEdge(loaded.Edges[0].Id, out _));
        TopologyValidator.AssertValid(loaded, UiTolerance);
    }

    [Fact]
    public void MoveVertex_UpdatesSpatialLookup()
    {
        var document = new CadDocument();
        var vertex = new Vertex(new PointF(0, 0));
        document.AddVertex(vertex);

        TopologyService.MoveVertex(document, vertex.Id, new PointF(100, 0));

        Assert.Null(TopologyService.FindVertex(document, new PointF(0, 0), Tolerance));
        Assert.NotNull(TopologyService.FindVertex(document, new PointF(100, 0), Tolerance));
    }

    [Fact]
    public void RemoveEdgesAndPrune_SyncsVertexIndex()
    {
        var document = CreateSquareDocument();
        var edge = document.Edges[0];

        document.RemoveEdge(edge);
        TopologyService.PruneUnusedVertices(document);

        Assert.Equal(3, document.Edges.Count);
        Assert.Equal(4, document.Vertices.Count);
        Assert.True(document.Index.TryGetVertex(document.Vertices[0].Id, out _));
        Assert.False(document.Index.TryGetEdge(edge.Id, out _));
    }

    [Fact]
    public void HistoryRestore_RebuildsIndex()
    {
        var session = new Infrastructure.CadSession();
        var document = session.Document;
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);

        session.History.Record(document);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(100, 100), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);

        Assert.True(session.History.Undo(document, UiTolerance));
        TopologyValidator.AssertValid(document, UiTolerance);
        Assert.True(document.Index.TryGetEdge(document.Edges[0].Id, out _));
    }

    [Fact]
    public void SnapService_AfterSyncFaces_UsesVertexSnapAtCrossing()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 50), new PointF(100, 50), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(50, 0), new PointF(50, 100), template, Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);

        var centerVertex = document.Vertices
            .Single(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(50, 50), Tolerance));

        var snap = new SnapService().FindBestSnap(document, new PointF(50, 50), UiTolerance, includeOnEdge: true);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Endpoint, snap.Snap!.Value.Kind);
        Assert.Equal(centerVertex.Id, snap.Snap.Value.VertexId);
    }

    private static CadDocument CreateSquareDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(1, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(1, 0), new PointF(1, 1), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(1, 1), new PointF(0, 1), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 1), new PointF(0, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }
}
