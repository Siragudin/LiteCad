using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class EraserToolTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void Eraser_ClickEdge_DeletesEdge()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquare();
            SetPreciseEraserZoom(harness);
            harness.ToolService.ActivateTool(harness.EraserTool);

            var edgeCountBefore = harness.Session.Document.Edges.Count;
            var topEdge = FindHorizontalEdgeAt(harness.Session.Document, y: 0);

            harness.EraserTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(2, 0));

            Assert.Equal(edgeCountBefore - 1, harness.Session.Document.Edges.Count);
            Assert.DoesNotContain(topEdge.Id, harness.Session.Document.Edges.Select(edge => edge.Id));
            Assert.Empty(harness.Session.Document.Polygons);
            Assert.True(harness.Session.ToolService.ActiveTool is EraserTool);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void Eraser_ClickVertex_DeletesAccordingToExistingDeleteSemantics()
    {
        RunSta(() =>
        {
            var referenceSession = CreateSquareSession();
            var corner = referenceSession.Document.Vertices
                .Single(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tol));
            ApplyExistingDeleteSemantics(referenceSession, corner.Id, SelectionPickKind.Vertex);

            var expectedEdgeCount = referenceSession.Document.Edges.Count;
            var expectedPolygonCount = referenceSession.Document.Polygons.Count;
            var expectedSuppressedCount = referenceSession.Document.SuppressedFaceGeometryKeys.Count;

            var harness = CreateHarnessWithSquare();
            SetPreciseEraserZoom(harness);
            harness.ToolService.ActivateTool(harness.EraserTool);
            harness.EraserTool.OnMouseDown(
                CreateMouseButtonEvent(MouseButton.Left),
                corner.Position);

            Assert.Equal(expectedEdgeCount, harness.Session.Document.Edges.Count);
            Assert.Equal(expectedPolygonCount, harness.Session.Document.Polygons.Count);
            Assert.Equal(expectedSuppressedCount, harness.Session.Document.SuppressedFaceGeometryKeys.Count);
            Assert.True(harness.Session.History.CanUndo);
        });
    }

    [Fact]
    public void Eraser_ClickPolygon_DeletesAccordingToExistingDeleteSemantics()
    {
        RunSta(() =>
        {
            var referenceSession = CreateNestedSession(2);
            var innerFace = PickSmallestFace(referenceSession.Document, 2f, 2f);
            Assert.NotNull(innerFace);
            ApplyExistingDeleteSemantics(referenceSession, innerFace!.Id, SelectionPickKind.Polygon);

            var expectedEdgeCount = referenceSession.Document.Edges.Count;
            var expectedPolygonCount = referenceSession.Document.Polygons.Count;
            var expectedSuppressedCount = referenceSession.Document.SuppressedFaceGeometryKeys.Count;

            var harness = CreateHarnessWithNestedDocument(2);
            SetPreciseEraserZoom(harness);
            harness.ToolService.ActivateTool(harness.EraserTool);
            harness.EraserTool.OnMouseDown(
                CreateMouseButtonEvent(MouseButton.Left),
                new PointF(2f, 2f));

            Assert.Equal(expectedEdgeCount, harness.Session.Document.Edges.Count);
            Assert.Equal(expectedPolygonCount, harness.Session.Document.Polygons.Count);
            Assert.Equal(expectedSuppressedCount, harness.Session.Document.SuppressedFaceGeometryKeys.Count);
        });
    }

    [Fact]
    public void Eraser_OneClick_OneObject()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithNestedDocument(2);
            SetPreciseEraserZoom(harness);
            harness.ToolService.ActivateTool(harness.EraserTool);

            var document = harness.Session.Document;
            var polygonCountBefore = document.Polygons.Count;
            var edgeCountBefore = document.Edges.Count;

            harness.EraserTool.OnMouseDown(
                CreateMouseButtonEvent(MouseButton.Left),
                new PointF(2f, 2f));

            Assert.Equal(polygonCountBefore - 1, document.Polygons.Count);
            Assert.Equal(edgeCountBefore, document.Edges.Count);
            Assert.NotNull(FindFaceByArea(document, 16));
            Assert.Null(FindFaceByArea(document, 4));
        });
    }

    [Fact]
    public void Eraser_RightClick()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquare();
            SetPreciseEraserZoom(harness);
            harness.ToolService.ActivateTool(harness.EraserTool);

            harness.EraserTool.OnMouseMove(CreateMouseEvent(), new PointF(2, 0));
            Assert.NotNull(harness.EraserTool.HoveredTarget);

            harness.EraserTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Right), new PointF(2, 0));

            Assert.Null(harness.EraserTool.HoveredTarget);
            Assert.True(harness.Session.ToolService.ActiveTool is EraserTool);
            Assert.Equal(4, harness.Session.Document.Edges.Count);
        });
    }

    [Fact]
    public void Eraser_UndoRedo()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquare();
            SetPreciseEraserZoom(harness);
            harness.ToolService.ActivateTool(harness.EraserTool);

            harness.EraserTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(2, 0));

            Assert.Equal(3, harness.Session.Document.Edges.Count);
            Assert.Empty(harness.Session.Document.Polygons);

            Assert.True(harness.Session.History.Undo(harness.Session.Document, Tol));
            Assert.Equal(4, harness.Session.Document.Edges.Count);
            Assert.Single(harness.Session.Document.Polygons);

            Assert.True(harness.Session.History.Redo(harness.Session.Document, Tol));
            Assert.Equal(3, harness.Session.Document.Edges.Count);
            Assert.Empty(harness.Session.Document.Polygons);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void Eraser_DoesNotRequireSelection()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquare();
            SetPreciseEraserZoom(harness);
            harness.ToolService.ActivateTool(harness.EraserTool);

            Assert.True(harness.Session.Selection.IsEmpty);

            harness.EraserTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(2, 0));

            Assert.Equal(3, harness.Session.Document.Edges.Count);
            Assert.True(harness.Session.Selection.IsEmpty);
            Assert.True(harness.Session.ToolService.ActiveTool is EraserTool);
        });
    }

    private static void ApplyExistingDeleteSemantics(CadSession session, Guid objectId, SelectionPickKind kind)
    {
        session.Selection.Clear();

        switch (kind)
        {
            case SelectionPickKind.Vertex:
                session.Selection.SelectedVertexIds.Add(objectId);
                break;
            case SelectionPickKind.Edge:
                session.Selection.SelectedEdgeIds.Add(objectId);
                break;
            case SelectionPickKind.Polygon:
                session.Selection.SelectedPolygonIds.Add(objectId);
                break;
        }

        Assert.True(session.Edit.Delete(session));
    }

    private static CadSession CreateSquareSession()
    {
        var session = new CadSession();
        AddSquare(session.Document, 0, 0, 4);
        PolygonBuilder.SyncFaces(session.Document, Tol);
        return session;
    }

    private static CadSession CreateNestedSession(int levels)
    {
        var session = new CadSession();
        PopulateNestedDocument(session.Document, levels);
        return session;
    }

    private static void PopulateNestedDocument(CadDocument document, int levels)
    {
        var origin = 0f;
        var size = 4f;

        for (var level = 0; level < levels; level++)
        {
            AddSquare(document, origin, origin, size);
            origin += size * 0.25f;
            size *= 0.5f;
        }

        PolygonBuilder.SyncFaces(document, Tol);
    }

    private static Edge FindHorizontalEdgeAt(CadDocument document, float y)
        => document.Edges.Single(edge =>
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            return Math.Abs(start.Y - y) < Tol
                && Math.Abs(end.Y - y) < Tol
                && Math.Abs(start.Y - end.Y) < Tol;
        });

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

    private static string DocumentFingerprint(CadDocument document)
    {
        var vertices = document.Vertices
            .OrderBy(vertex => vertex.Id)
            .Select(vertex => $"{vertex.Id}:{vertex.Position.X},{vertex.Position.Y}");
        var edges = document.Edges
            .OrderBy(edge => edge.Id)
            .Select(edge => $"{edge.Id}:{edge.StartVertexId}-{edge.EndVertexId}");
        var polygons = document.Polygons
            .OrderBy(polygon => polygon.Id)
            .Select(polygon => $"{polygon.Id}:{polygon.Type}:{polygon.OuterLoop.Edges.Count}");
        var suppressed = document.SuppressedFaceGeometryKeys
            .OrderBy(key => key.ToString());

        return string.Join(
            "|",
            document.Vertices.Count,
            document.Edges.Count,
            document.Polygons.Count,
            document.SuppressedFaceGeometryKeys.Count,
            string.Join(";", vertices),
            string.Join(";", edges),
            string.Join(";", polygons),
            string.Join(";", suppressed));
    }

    private static void SetPreciseEraserZoom(EraserToolHarness harness)
        => harness.Session.Camera.ZoomAt(new Point(400, 300), 100, harness.ViewportSize);

    private static EraserToolHarness CreateHarnessWithSquare()
    {
        var harness = new EraserToolHarness();
        AddSquare(harness.Session.Document, 0, 0, 4);
        PolygonBuilder.SyncFaces(harness.Session.Document, Tol);
        return harness;
    }

    private static EraserToolHarness CreateHarnessWithNestedDocument(int levels)
    {
        var harness = new EraserToolHarness();
        PopulateNestedDocument(harness.Session.Document, levels);
        return harness;
    }

    private static void RunSta(Action action) => WpfTestUtilities.RunSta(action);

    private static MouseButtonEventArgs CreateMouseButtonEvent(MouseButton button)
        => new(Mouse.PrimaryDevice, 0, button)
        {
            RoutedEvent = button == MouseButton.Left
                ? UIElement.MouseLeftButtonDownEvent
                : UIElement.MouseRightButtonDownEvent
        };

    private static MouseEventArgs CreateMouseEvent()
        => new(Mouse.PrimaryDevice, 0)
        {
            RoutedEvent = UIElement.MouseMoveEvent
        };

    private sealed class EraserToolHarness
    {
        public EraserToolHarness()
        {
            Session = new CadSession();
            ToolService = Session.ToolService;
            EraserTool = new EraserTool();

            var context = new ToolContext(
                Session,
                () => ViewportSize,
                screen => Session.Camera.ScreenToWorld(screen, ViewportSize),
                _ => new Point(),
                () => RedrawRequested = true,
                () => { },
                () => { },
                () => { });

            ToolService.Initialize(context);
        }

        public CadSession Session { get; }

        public ToolService ToolService { get; }

        public EraserTool EraserTool { get; }

        public Size ViewportSize { get; } = new(800, 600);

        public bool RedrawRequested { get; private set; }
    }
}
