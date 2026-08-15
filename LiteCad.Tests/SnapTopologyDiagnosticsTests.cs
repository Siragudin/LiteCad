using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Services;
using System.Text;
using System.Windows;
using Xunit;
using Xunit.Abstractions;

namespace LiteCad.Tests;

public class SnapTopologyDiagnosticsTests
{
    private const double UiTolerance = 12.0;

    private readonly ITestOutputHelper _output;

    public SnapTopologyDiagnosticsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Square_DoesNotCreateDuplicateVertexPositionsWithinTolerance()
    {
        var document = CreateSquareViaLineToolFlow(exactCoordinates: true);
        DiagnoseSquareTopology(document, UiTolerance, "Exact LineTool square");
    }

    [Fact]
    public void Square_ImpreciseCornerEndpoints_PreservesDistinctTopologyVertices()
    {
        var document = CreateSquareViaLineToolFlow(exactCoordinates: false);
        DiagnoseSquareTopology(document, UiTolerance, "Imprecise corner square");
    }

    [Fact]
    public void Square_ScreenSpaceCoordinates_ReportsTopology()
    {
        var camera = new Camera();
        var viewport = new Size(800, 600);
        var tolerance = MathUtils.SnapToleranceWorld(camera.Zoom);
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        var a = camera.ScreenToWorld(new Point(300, 200), viewport);
        var b = camera.ScreenToWorld(new Point(500, 200), viewport);
        var c = camera.ScreenToWorld(new Point(500, 400), viewport);
        var d = camera.ScreenToWorld(new Point(300, 400), viewport);

        AddSegment(document, template, a, b, tolerance);
        AddSegment(document, template, b, c, tolerance);
        AddSegment(document, template, c, d, tolerance);
        AddSegment(document, template, d, a, tolerance);

        DiagnoseSquareTopology(document, tolerance, "Screen-space square");
    }

    private void DiagnoseSquareTopology(CadDocument document, double tolerance, string label)
    {
        var report = new StringBuilder();
        report.AppendLine($"=== {label} (tolerance={tolerance}) ===");
        report.AppendLine($"Edge count: {document.Edges.Count}");
        report.AppendLine($"Vertex count: {document.Vertices.Count}");
        report.AppendLine();

        report.AppendLine("Canonical vertices:");
        foreach (var vertex in document.Vertices)
        {
            report.AppendLine($"  {vertex.Id}: ({vertex.Position.X:G17}, {vertex.Position.Y:G17})");
        }

        report.AppendLine();
        report.AppendLine("All Edge endpoints:");
        foreach (var edge in document.Edges)
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            report.AppendLine(
                $"  {edge.Id}: Start=({start.X:G17}, {start.Y:G17}) " +
                $"End=({end.X:G17}, {end.Y:G17})");
        }

        report.AppendLine();
        var cornerPositions = InferCornerPositions(document, tolerance);
        report.AppendLine($"Expected corners: {cornerPositions.Count}");

        foreach (var corner in cornerPositions)
        {
            var snapService = new SnapService();
            var visibleSnaps = snapService.GetVisibleSnaps(document, corner, tolerance);
            var vertexSnaps = visibleSnaps.Where(snap => snap.Kind == SnapKind.Endpoint).ToList();

            report.AppendLine(
                $"  Corner ({corner.X:G9}, {corner.Y:G9}): " +
                $"visible-snaps={visibleSnaps.Count}, vertex-snaps={vertexSnaps.Count}");
        }

        _output.WriteLine(report.ToString());

        if (label.Contains("Imprecise", StringComparison.Ordinal))
        {
            Assert.Equal(5, document.Edges.Count);
            Assert.Equal(6, document.Vertices.Count);
            return;
        }

        Assert.Equal(4, document.Edges.Count);
        Assert.Equal(4, document.Vertices.Count);
        Assert.Equal(4, cornerPositions.Count);

        foreach (var corner in cornerPositions)
        {
            var snapService = new SnapService();
            var visibleSnaps = snapService.GetVisibleSnaps(document, corner, tolerance);
            var vertexSnaps = visibleSnaps.Where(snap => snap.Kind == SnapKind.Endpoint).ToList();

            Assert.Single(vertexSnaps);
        }
    }

    private static CadDocument CreateSquareViaLineToolFlow(bool exactCoordinates)
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        if (exactCoordinates)
        {
            AddSegment(document, template, new PointF(0, 0), new PointF(100, 0), UiTolerance);
            AddSegment(document, template, new PointF(100, 0), new PointF(100, 100), UiTolerance);
            AddSegment(document, template, new PointF(100, 100), new PointF(0, 100), UiTolerance);
            AddSegment(document, template, new PointF(0, 100), new PointF(0, 0), UiTolerance);
            return document;
        }

        AddSegment(document, template, new PointF(0, 0), new PointF(100, 0), UiTolerance);
        AddSegment(document, template, new PointF(100, 0.001f), new PointF(100, 100), UiTolerance);
        AddSegment(document, template, new PointF(100, 100), new PointF(0, 99.999f), UiTolerance);
        AddSegment(document, template, new PointF(0, 100), new PointF(0.0001f, 0), UiTolerance);
        return document;
    }

    private static void AddSegment(
        CadDocument document,
        Edge template,
        PointF start,
        PointF end,
        double tolerance)
    {
        EdgeOperations.AddSegment(document, start, end, template, tolerance);
        PolygonBuilder.SyncFaces(document, tolerance);
    }

    [Fact]
    public void Square_RealisticClickJitter_ProducesOneVertexSnapAtCorner()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        const double tolerance = 12.0;

        AddSegment(document, template, new PointF(0, 0), new PointF(100, 0), tolerance);
        AddSegment(document, template, new PointF(100, 0), new PointF(100, 100), tolerance);
        AddSegment(document, template, new PointF(100, 100), new PointF(0, 100), tolerance);
        AddSegment(document, template, new PointF(0, 100), new PointF(1, 0), tolerance);

        var corner = new PointF(0, 0);
        var visibleSnaps = new SnapService().GetVisibleSnaps(document, corner, tolerance);
        var vertexSnaps = visibleSnaps.Where(snap => snap.Kind == SnapKind.Endpoint).ToList();

        _output.WriteLine($"Realistic jitter at (0,0):");
        _output.WriteLine($"  canonical vertices: {document.Vertices.Count}");
        _output.WriteLine($"  visible snaps: {visibleSnaps.Count}");
        _output.WriteLine($"  vertex snaps: {vertexSnaps.Count}");

        Assert.Equal(5, document.Vertices.Count);
        Assert.Single(vertexSnaps);
    }

    private static List<PointF> InferCornerPositions(CadDocument document, double tolerance)
    {
        return document.Vertices
            .Select(vertex => vertex.Position)
            .OrderBy(point => point.X)
            .ThenBy(point => point.Y)
            .ToList();
    }
}
