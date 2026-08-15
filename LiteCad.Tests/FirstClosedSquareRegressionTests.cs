using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Services;
using System.Windows;
using Xunit;
using Xunit.Abstractions;

namespace LiteCad.Tests;

public class FirstClosedSquareRegressionTests
{
    private const double Tolerance = 1e-4;
    private const double UiTolerance = 12.0;

    private readonly ITestOutputHelper _output;

    public FirstClosedSquareRegressionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void NewDocument_FirstClosedSquare_CreatesFace()
    {
        var document = new CadDocument();
        AddSquareEdges(document);

        PolygonBuilder.SyncFaces(document, Tolerance);

        AssertDiagnosticState(document, Tolerance, expectedFaces: 1, expectedEdges: 4);
        Assert.Single(document.Polygons);
    }

    [Fact]
    public void FirstAndSecondClosedSquares_BothCreateFaces()
    {
        var document = new CadDocument();

        AddSquareEdges(document, originX: 0, originY: 0, size: 1);
        PolygonBuilder.SyncFaces(document, Tolerance);
        AssertDiagnosticState(document, Tolerance, expectedFaces: 1, expectedEdges: 4);

        AddSquareEdges(document, originX: 2, originY: 0, size: 1);
        PolygonBuilder.SyncFaces(document, Tolerance);
        AssertDiagnosticState(document, Tolerance, expectedFaces: 2, expectedEdges: 8);
    }

    [Fact]
    public void FirstSquare_WithDiagonal_CreatesTwoFaces()
    {
        var document = new CadDocument();
        AddSquareEdges(document);
        PolygonBuilder.SyncFaces(document, Tolerance);
        AssertDiagnosticState(document, Tolerance, expectedFaces: 1, expectedEdges: 4);

        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(1, 1), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        AssertDiagnosticState(document, Tolerance, expectedFaces: 2, expectedEdges: 5);
    }

    [Fact]
    public void LineToolFlow_FirstClosedSquare_CreatesFaceAfterEachSegment()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        AddSegmentAndSync(document, template, new PointF(0, 0), new PointF(100, 0), UiTolerance);
        AssertDiagnosticState(document, UiTolerance, expectedFaces: 0, expectedEdges: 1);

        AddSegmentAndSync(document, template, new PointF(100, 0), new PointF(100, 100), UiTolerance);
        AssertDiagnosticState(document, UiTolerance, expectedFaces: 0, expectedEdges: 2);

        AddSegmentAndSync(document, template, new PointF(100, 100), new PointF(0, 100), UiTolerance);
        AssertDiagnosticState(document, UiTolerance, expectedFaces: 0, expectedEdges: 3);

        AddSegmentAndSync(document, template, new PointF(0, 100), new PointF(0, 0), UiTolerance);
        AssertDiagnosticState(document, UiTolerance, expectedFaces: 1, expectedEdges: 4);
    }

    [Fact]
    public void LineToolFlow_FirstThenSecondClosedSquare_BothCreateFaces()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        DrawClosedSquareWithLineToolFlow(document, template, originX: 0, originY: 0, size: 100, UiTolerance);
        AssertDiagnosticState(document, UiTolerance, expectedFaces: 1, expectedEdges: 4);

        DrawClosedSquareWithLineToolFlow(document, template, originX: 200, originY: 0, size: 100, UiTolerance);
        AssertDiagnosticState(document, UiTolerance, expectedFaces: 2, expectedEdges: 8);
    }

    [Fact]
    public void LineToolFlow_ScreenSpaceSquare_CreatesFace()
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

        AddSegmentAndSync(document, template, a, b, tolerance);
        AddSegmentAndSync(document, template, b, c, tolerance);
        AddSegmentAndSync(document, template, c, d, tolerance);
        AddSegmentAndSync(document, template, d, a, tolerance);

        _output.WriteLine($"Screen square: edges={document.Edges.Count}, faces={document.Polygons.Count}, tol={tolerance}");
        AssertDiagnosticState(document, tolerance, expectedFaces: 1, expectedEdges: 4);
    }

    [Fact]
    public void AfterExplicitFaceDelete_FirstLocationBlocked_SecondLocationCreatesFace()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();

        DrawClosedSquareWithLineToolFlow(document, template, 0, 0, 100, UiTolerance);
        var face = Assert.Single(document.Polygons);
        PolygonBuilder.SuppressFaceGeometry(document, face, UiTolerance);
        PolygonBuilder.SyncFaces(document, UiTolerance);

        Assert.Equal(4, document.Edges.Count);
        Assert.Empty(document.Polygons);
        Assert.NotEmpty(document.SuppressedFaceGeometryKeys);

        DrawClosedSquareWithLineToolFlow(document, template, 200, 0, 100, UiTolerance);

        Assert.Equal(8, document.Edges.Count);
        Assert.Single(document.Polygons);
    }

    [Fact]
    public void NewDocument_ClearsSuppression_AllowsFirstSquareFace()
    {
        var session = new CadSession();
        session.Document.SuppressedFaceGeometryKeys.Add("stale-key");
        session.NewDocument();

        Assert.Empty(session.Document.SuppressedFaceGeometryKeys);
        Assert.Empty(session.Document.Edges);
        Assert.Empty(session.Document.Vertices);

        DrawClosedSquareWithLineToolFlow(
            session.Document,
            TestDocumentHelpers.CreateTemplate(),
            0,
            0,
            100,
            UiTolerance);

        Assert.Single(session.Document.Polygons);
    }

    private void AssertDiagnosticState(
        CadDocument document,
        double tolerance,
        int expectedFaces,
        int expectedEdges)
    {
        _output.WriteLine(
            $"edges={document.Edges.Count}, vertices={document.Vertices.Count}, faces={document.Polygons.Count}, " +
            $"suppressed={document.SuppressedFaceGeometryKeys.Count}");

        foreach (var edge in document.Edges)
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            _output.WriteLine($"  edge {edge.Id}: ({start.X},{start.Y}) -> ({end.X},{end.Y})");
        }

        foreach (var key in document.SuppressedFaceGeometryKeys)
        {
            _output.WriteLine($"  suppressed key: '{key}'");
        }

        Assert.Equal(expectedEdges, document.Edges.Count);
        Assert.Equal(expectedFaces, document.Polygons.Count);
    }

    private static void AddSquareEdges(
        CadDocument document,
        float originX = 0,
        float originY = 0,
        float size = 1)
    {
        TestDocumentHelpers.AddEdge(document, new PointF(originX, originY), new PointF(originX + size, originY), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(originX + size, originY), new PointF(originX + size, originY + size), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(originX + size, originY + size), new PointF(originX, originY + size), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(originX, originY + size), new PointF(originX, originY), Tolerance);
    }

    private static void AddSegmentAndSync(
        CadDocument document,
        Edge template,
        PointF start,
        PointF end,
        double tolerance)
    {
        EdgeOperations.AddSegment(document, start, end, template, tolerance);
        PolygonBuilder.SyncFaces(document, tolerance);
    }

    private static void DrawClosedSquareWithLineToolFlow(
        CadDocument document,
        Edge template,
        float originX,
        float originY,
        float size,
        double tolerance)
    {
        AddSegmentAndSync(document, template, new PointF(originX, originY), new PointF(originX + size, originY), tolerance);
        AddSegmentAndSync(document, template, new PointF(originX + size, originY), new PointF(originX + size, originY + size), tolerance);
        AddSegmentAndSync(document, template, new PointF(originX + size, originY + size), new PointF(originX, originY + size), tolerance);
        AddSegmentAndSync(document, template, new PointF(originX, originY + size), new PointF(originX, originY), tolerance);
    }
}
