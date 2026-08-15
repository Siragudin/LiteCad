using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using Xunit;

namespace LiteCad.Tests;

public class TopologyTests
{
    private const double Tolerance = 1e-4;
    private const double UiTolerance = 12.0;

    [Fact]
    public void Square_CreatesFourCanonicalVertices()
    {
        var document = CreateSquare(UiTolerance);

        Assert.Equal(4, document.Vertices.Count);
        Assert.Equal(4, document.Edges.Count);
        Assert.Single(document.Polygons);
        TopologyValidator.AssertValid(document, UiTolerance);
    }

    [Fact]
    public void ConnectedEdgesShareCanonicalVertex()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(1, 0), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(1, 0), new PointF(2, 0), template, Tolerance);

        Assert.Equal(3, document.Vertices.Count);
        Assert.Equal(2, document.Edges.Count);

        var edges = document.Edges.ToList();
        Assert.Equal(edges[0].EndVertexId, edges[1].StartVertexId);
        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void NearCoincidentPointsReuseVertex()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        const double delta = 1e-5;

        EdgeOperations.AddSegment(document, new PointF(100, 100), new PointF(200, 100), template, Tolerance);
        EdgeOperations.AddSegment(
            document,
            new PointF(200 + (float)delta, 100 + (float)delta),
            new PointF(300, 100),
            template,
            Tolerance);

        Assert.Equal(3, document.Vertices.Count);
        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void OutsideToleranceCreatesDifferentVertex()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        EdgeOperations.AddSegment(document, new PointF(100, 100), new PointF(200, 100), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(200.1f, 100.1f), new PointF(300, 100), template, Tolerance);

        Assert.Equal(4, document.Vertices.Count);
        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void SquareCornerProducesOneVertexSnap()
    {
        var document = CreateSquare(UiTolerance);
        var corner = document.Vertices.OrderBy(vertex => vertex.Position.X).First().Position;

        var snaps = new SnapService().GetVisibleSnaps(document, corner, UiTolerance);
        var vertexSnaps = snaps.Where(snap => snap.Kind == SnapKind.Endpoint).ToList();

        Assert.Single(vertexSnaps);
    }

    [Fact]
    public void DiagonalSplitsSquareEdges()
    {
        var document = CreateSquare(UiTolerance);
        var edgeCountBefore = document.Edges.Count;

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 100), TestDocumentHelpers.CreateTemplate(), UiTolerance);

        Assert.True(document.Edges.Count >= edgeCountBefore + 1);
        Assert.True(document.Vertices.Count >= 4);
        TopologyValidator.AssertValid(document, UiTolerance);
    }

    [Fact]
    public void DiagonalCreatesTwoFaces()
    {
        var document = CreateSquare(UiTolerance);
        Assert.Single(document.Polygons);

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 100), TestDocumentHelpers.CreateTemplate(), UiTolerance);
        PolygonBuilder.SyncFaces(document, UiTolerance);

        Assert.Equal(2, document.Polygons.Count);
        TopologyValidator.AssertValid(document, UiTolerance);
    }

    [Fact]
    public void OverlappingEdgeDoesNotDuplicate()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(1, 0), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(1, 0), template, Tolerance);

        Assert.Single(document.Edges);
        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void ReverseOverlappingEdgeDoesNotDuplicate()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(1, 0), template, Tolerance);
        EdgeOperations.AddSegment(document, new PointF(1, 0), new PointF(0, 0), template, Tolerance);

        Assert.Single(document.Edges);
        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void DeleteEdgeRemovesAffectedFace()
    {
        var document = CreateSquare(Tolerance);
        var edge = document.Edges[0];

        TopologyService.DeleteEdge(document, edge);
        TopologyService.PruneUnusedVertices(document);
        PolygonBuilder.SyncFaces(document, Tolerance);

        Assert.Empty(document.Polygons);
        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void RecreateEdgeRestoresFace()
    {
        var document = CreateSquare(Tolerance);
        var edge = document.Edges[0];
        var start = TopologyService.GetEdgeStartPoint(document, edge);
        var end = TopologyService.GetEdgeEndPoint(document, edge);

        TopologyService.DeleteEdge(document, edge);
        TopologyService.PruneUnusedVertices(document);
        PolygonBuilder.SyncFaces(document, Tolerance);
        Assert.Empty(document.Polygons);

        EdgeOperations.AddSegment(document, start, end, TestDocumentHelpers.CreateTemplate(), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);

        Assert.Single(document.Polygons);
        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void ExplicitFaceSuppressionStillWorks()
    {
        var document = CreateSquare(Tolerance);
        var face = Assert.Single(document.Polygons);
        PolygonBuilder.SuppressFaceGeometry(document, face, Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);

        Assert.Empty(document.Polygons);
        Assert.NotEmpty(document.SuppressedFaceGeometryKeys);
    }

    [Fact]
    public void TopologyChangeInvalidatesSuppression()
    {
        var document = CreateSquare(Tolerance);
        var face = Assert.Single(document.Polygons);
        PolygonBuilder.SuppressFaceGeometry(document, face, Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        Assert.Empty(document.Polygons);

        TopologyService.DeleteEdge(document, document.Edges[0]);
        PolygonBuilder.InvalidateSuppressionOnEdgeTopologyChange(document);

        Assert.Empty(document.SuppressedFaceGeometryKeys);
    }

    [Fact]
    public void NewDocumentClearsTopologyAndSuppression()
    {
        var session = new CadSession();
        CreateSquareOn(session.Document, UiTolerance);
        var face = Assert.Single(session.Document.Polygons);
        PolygonBuilder.SuppressFaceGeometry(session.Document, face, UiTolerance);

        session.NewDocument();

        Assert.Empty(session.Document.Vertices);
        Assert.Empty(session.Document.Edges);
        Assert.Empty(session.Document.Polygons);
        Assert.Empty(session.Document.SuppressedFaceGeometryKeys);
    }

    [Fact]
    public void MoveVertexUpdatesConnectedEdges()
    {
        var document = CreateSquare(Tolerance);
        var vertex = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tolerance));
        var connectedEdges = document.Edges
            .Where(edge => edge.StartVertexId == vertex.Id || edge.EndVertexId == vertex.Id)
            .ToList();

        TopologyService.MoveVertex(document, vertex.Id, new PointF(10, 10));

        foreach (var edge in connectedEdges)
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            Assert.True(
                MathUtils.ArePointsEqual(start, new PointF(10, 10), Tolerance) ||
                MathUtils.ArePointsEqual(end, new PointF(10, 10), Tolerance));
        }

        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void NoDuplicateVerticesAtIntersection()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, UiTolerance);
        EdgeOperations.AddSegment(document, new PointF(50, -50), new PointF(50, 50), template, UiTolerance);

        var intersectionVertices = document.Vertices
            .Where(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(50, 0), UiTolerance))
            .ToList();

        Assert.Single(intersectionVertices);
        TopologyValidator.AssertValid(document, UiTolerance);
    }

    private static CadDocument CreateSquare(double tolerance)
    {
        var document = new CadDocument();
        CreateSquareOn(document, tolerance);
        return document;
    }

    private static void CreateSquareOn(CadDocument document, double tolerance)
    {
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, tolerance);
        EdgeOperations.AddSegment(document, new PointF(100, 0), new PointF(100, 100), template, tolerance);
        EdgeOperations.AddSegment(document, new PointF(100, 100), new PointF(0, 100), template, tolerance);
        EdgeOperations.AddSegment(document, new PointF(0, 100), new PointF(0, 0), template, tolerance);
        PolygonBuilder.SyncFaces(document, tolerance);
    }
}
