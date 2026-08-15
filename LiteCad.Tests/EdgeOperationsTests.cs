using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class EdgeOperationsTests
{
    private const double Tolerance = 1e-4;

    [Fact]
    public void SquareThenInteriorToInteriorLine_CreatesValidSplitTopology()
    {
        const double tolerance = 12.0;
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, tolerance);
        EdgeOperations.AddSegment(document, new PointF(100, 0), new PointF(100, 100), template, tolerance);
        EdgeOperations.AddSegment(document, new PointF(100, 100), new PointF(0, 100), template, tolerance);
        EdgeOperations.AddSegment(document, new PointF(0, 100), new PointF(0, 0), template, tolerance);

        EdgeOperations.AddSegment(document, new PointF(50, 0), new PointF(50, 100), template, tolerance);
        PolygonBuilder.SyncFaces(document, tolerance);

        TopologyValidator.AssertValid(document, tolerance);
        Assert.Equal(6, document.Vertices.Count);
        Assert.Equal(7, document.Edges.Count);
        Assert.Equal(2, document.Polygons.Count(p => p.Type == PolygonType.Face));

        var canonicalPairs = document.Edges
            .Select(edge => edge.StartVertexId.CompareTo(edge.EndVertexId) <= 0
                ? (edge.StartVertexId, edge.EndVertexId)
                : (edge.EndVertexId, edge.StartVertexId))
            .ToList();
        Assert.Equal(canonicalPairs.Count, canonicalPairs.Distinct().Count());

        Assert.DoesNotContain(
            TopologyValidator.Validate(document, tolerance),
            error => error.Contains("Duplicate edge topology", StringComparison.Ordinal));

        foreach (var face in document.Polygons.Where(polygon => polygon.Type == PolygonType.Face))
        {
            Assert.Equal(4, face.OuterLoop.Edges.Count);
            foreach (var reference in face.OuterLoop.Edges)
            {
                Assert.Contains(document.Edges, edge => edge.Id == reference.EdgeId);
            }
        }
    }

    [Fact]
    public void AddSegment_OnCoincidentEdge_ReplacesInsteadOfDuplicating()
    {
        var document = new CadDocument();
        var template = Edge.CreateStyleTemplate();
        template.Color = Colors.Red;
        template.Thickness = 3;
        var existing = TopologyService.CreateEdgeFromPoints(
            document,
            new PointF(0, 0),
            new PointF(1, 0),
            template,
            Tolerance);

        var replaceTemplate = Edge.CreateStyleTemplate();
        replaceTemplate.Color = Colors.Blue;
        replaceTemplate.Thickness = 1;

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(1, 0), replaceTemplate, Tolerance);

        var edge = Assert.Single(document.Edges);
        Assert.Equal(existing.Id, edge.Id);
        Assert.Equal(Colors.Blue, edge.Color);
        Assert.Equal(1, edge.Thickness);
    }

    [Fact]
    public void AddSegment_OnPartialOverlap_TrimsExistingAndAddsNewSegment()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(2, 0));

        var template = Edge.CreateStyleTemplate();
        EdgeOperations.AddSegment(document, new PointF(0.5, 0), new PointF(1.5, 0), template, Tolerance);

        Assert.Equal(3, document.Edges.Count);
        Assert.Contains(document.Edges, edge =>
            MathUtils.Distance(TopologyService.GetEdgeStartPoint(document, edge), new PointF(0, 0)) <= Tolerance &&
            MathUtils.Distance(TopologyService.GetEdgeEndPoint(document, edge), new PointF(0.5, 0)) <= Tolerance);
        Assert.Contains(document.Edges, edge =>
            MathUtils.Distance(TopologyService.GetEdgeStartPoint(document, edge), new PointF(0.5, 0)) <= Tolerance &&
            MathUtils.Distance(TopologyService.GetEdgeEndPoint(document, edge), new PointF(1.5, 0)) <= Tolerance);
        Assert.Contains(document.Edges, edge =>
            MathUtils.Distance(TopologyService.GetEdgeStartPoint(document, edge), new PointF(1.5, 0)) <= Tolerance &&
            MathUtils.Distance(TopologyService.GetEdgeEndPoint(document, edge), new PointF(2, 0)) <= Tolerance);
    }

    [Fact]
    public void AddSegment_AndSyncFaces_SquareThenDiagonal_StillCreatesTwoFaces()
    {
        const double tolerance = 12.0;
        var document = new CadDocument();

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), TestDocumentHelpers.CreateTemplate(), tolerance);
        EdgeOperations.AddSegment(document, new PointF(100, 0), new PointF(100, 100), TestDocumentHelpers.CreateTemplate(), tolerance);
        EdgeOperations.AddSegment(document, new PointF(100, 100), new PointF(0, 100), TestDocumentHelpers.CreateTemplate(), tolerance);
        EdgeOperations.AddSegment(document, new PointF(0, 100), new PointF(0, 0), TestDocumentHelpers.CreateTemplate(), tolerance);
        PolygonBuilder.SyncFaces(document, tolerance);

        Assert.Single(document.Polygons);

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 100), TestDocumentHelpers.CreateTemplate(), tolerance);
        PolygonBuilder.SyncFaces(document, tolerance);

        Assert.Equal(2, document.Polygons.Count);
    }

    [Fact]
    public void AddSegment_NearCoincidentOverlap_DoesNotBreakSquareFace()
    {
        const double tolerance = 12.0;
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(100, 100), tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 100), new PointF(0, 100), tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 100), new PointF(0, 0), tolerance);

        EdgeOperations.AddSegment(
            document,
            new PointF(0, 0),
            new PointF(100, 0),
            TestDocumentHelpers.CreateTemplate(),
            tolerance);

        PolygonBuilder.SyncFaces(document, tolerance);

        Assert.Single(document.Polygons);
        Assert.Equal(4, document.Edges.Count);
    }
}
