using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Services;
using LiteCad.Tools;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class MoveToolTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void MoveVertex_UpdatesConnectedEdges()
    {
        var document = CreateUnitSquare();
        var movedVertex = document.Vertices.First(vertex =>
            MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tol));
        var movedVertexId = movedVertex.Id;
        var connectedEdges = document.Edges
            .Where(edge => edge.StartVertexId == movedVertexId || edge.EndVertexId == movedVertexId)
            .ToList();

        MoveOperations.MoveVertices(document, [movedVertexId], new PointF(1, 2));

        var moved = TopologyService.GetVertexPosition(document, movedVertexId);
        Assert.Equal(1, moved.X, 3);
        Assert.Equal(2, moved.Y, 3);

        foreach (var edge in connectedEdges)
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            Assert.True(
                MathUtils.ArePointsEqual(start, moved, Tol) ||
                MathUtils.ArePointsEqual(end, moved, Tol));
        }

        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MoveVertex_PreservesVertexId()
    {
        var document = CreateUnitSquare();
        var vertexId = document.Vertices.First(vertex =>
            MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tol)).Id;

        MoveOperations.MoveVertices(document, [vertexId], new PointF(2, 3));

        Assert.Contains(document.Vertices, vertex => vertex.Id == vertexId);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MoveVertex_DoesNotCreateVertex()
    {
        var document = CreateUnitSquare();
        var countBefore = document.Vertices.Count;
        var vertexId = document.Vertices.First(vertex =>
            MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tol)).Id;

        MoveOperations.MoveVertices(document, [vertexId], new PointF(0.5f, 0.5f));

        Assert.Equal(countBefore, document.Vertices.Count);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MoveVertex_RebuildsFace()
    {
        var document = CreateUnitSquare();
        var vertexId = document.Vertices.First(vertex =>
            MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tol)).Id;

        MoveOperations.MoveVertices(document, [vertexId], new PointF(0, 1));

        Assert.Single(document.Polygons);
        Assert.True(PolygonGeometry.GetArea(document, document.Polygons[0], Tol) > 0);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MoveVertex_UndoRedo()
    {
        var session = new CadSession();
        var document = CreateUnitSquare(session.Document);
        var vertexId = document.Vertices.First(vertex =>
            MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tol)).Id;
        var before = TopologyService.GetVertexPosition(document, vertexId);

        session.History.Record(document);
        MoveOperations.MoveVertices(document, [vertexId], new PointF(1, 0));
        var afterMove = TopologyService.GetVertexPosition(document, vertexId);

        Assert.True(session.History.Undo(document, Tol));
        Assert.Equal(before.X, TopologyService.GetVertexPosition(document, vertexId).X, 3);
        Assert.Equal(before.Y, TopologyService.GetVertexPosition(document, vertexId).Y, 3);

        Assert.True(session.History.Redo(document, Tol));
        Assert.Equal(afterMove.X, TopologyService.GetVertexPosition(document, vertexId).X, 3);
        Assert.Equal(afterMove.Y, TopologyService.GetVertexPosition(document, vertexId).Y, 3);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MoveSingleEdge_RecreatesGeometry()
    {
        var document = CreateHorizontalChain();
        var selected = document.Edges.First(edge =>
            MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(document, edge), new PointF(0, 0), Tol));
        var oldId = selected.Id;
        var selection = new Selection();
        selection.SelectedEdgeIds.Add(oldId);

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        sessionExecuteMove(document, selection, snapshot, new PointF(0, 5));

        Assert.DoesNotContain(document.Edges, edge => edge.Id == oldId);
        Assert.Equal(2, document.Edges.Count);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MoveSingleEdge_DetachesSharedVertex()
    {
        var document = CreateHorizontalChain();
        var ab = document.Edges.First(edge =>
            MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(document, edge), new PointF(0, 0), Tol));
        var bc = document.Edges.First(edge =>
            MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(document, edge), new PointF(1, 0), Tol));
        var sharedBefore = ab.EndVertexId;
        Assert.Equal(sharedBefore, bc.StartVertexId);

        var selection = new Selection();
        selection.SelectedEdgeIds.Add(ab.Id);
        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        sessionExecuteMove(document, selection, snapshot, new PointF(0, 2));

        var bcAfter = document.Edges.First(edge =>
            MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(document, edge), new PointF(1, 0), Tol));
        Assert.Equal(sharedBefore, bcAfter.StartVertexId);
        Assert.NotEqual(sharedBefore, document.Edges.Single(edge => edge.Id != bcAfter.Id).EndVertexId);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MoveSingleEdge_LeavesUnselectedGeometry()
    {
        var document = CreateHorizontalChain();
        var bc = document.Edges.First(edge =>
            MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(document, edge), new PointF(1, 0), Tol));
        var bcStartBefore = TopologyService.GetEdgeStartPoint(document, bc);
        var bcEndBefore = TopologyService.GetEdgeEndPoint(document, bc);

        var ab = document.Edges.First(edge =>
            MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(document, edge), new PointF(0, 0), Tol));
        var selection = new Selection();
        selection.SelectedEdgeIds.Add(ab.Id);
        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        sessionExecuteMove(document, selection, snapshot, new PointF(0, 2));

        var bcAfter = document.Edges.First(edge =>
            MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(document, edge), bcStartBefore, Tol));
        Assert.Equal(bcStartBefore, TopologyService.GetEdgeStartPoint(document, bcAfter));
        Assert.Equal(bcEndBefore, TopologyService.GetEdgeEndPoint(document, bcAfter));
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MoveMultipleEdges_AsOneSelection()
    {
        var document = CreateHorizontalChain();
        var selection = new Selection();
        foreach (var edge in document.Edges)
        {
            selection.SelectedEdgeIds.Add(edge.Id);
        }

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        sessionExecuteMove(document, selection, snapshot, new PointF(5, 0));

        Assert.Equal(2, document.Edges.Count);
        Assert.All(document.Edges, edge =>
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            Assert.True(start.X >= 5 - Tol);
        });
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MoveTwoRooms_PreservesTwoFaces()
    {
        var document = CreateTwoRooms();
        var selection = new Selection();
        foreach (var face in document.Polygons)
        {
            selection.SelectedPolygonIds.Add(face.Id);
        }

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        sessionExecuteMove(document, selection, snapshot, new PointF(10, 0));

        Assert.Equal(2, document.Polygons.Count);
        Assert.All(document.Polygons, face => Assert.Equal(16.0, PolygonGeometry.GetArea(document, face, Tol), 3));
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MoveOneRoom_DoesNotMoveOtherRoom()
    {
        var document = CreateTwoRooms();
        var leftFace = document.Polygons.MinBy(face => GetFaceMinX(document, face))!;
        var rightFace = document.Polygons.MaxBy(face => GetFaceMinX(document, face))!;
        var rightMinXBefore = GetFaceMinX(document, rightFace);

        var selection = new Selection();
        selection.SelectedPolygonIds.Add(leftFace.Id);
        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        sessionExecuteMove(document, selection, snapshot, new PointF(5, 0));

        var rightAfter = document.Polygons.MaxBy(face => GetFaceMinX(document, face))!;
        Assert.Equal(rightMinXBefore, GetFaceMinX(document, rightAfter), 3);
        Assert.Equal(2, document.Polygons.Count);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MoveClosedSelection_RebuildsFace()
    {
        var document = CreateUnitSquare(size: 4);
        var selection = new Selection();
        foreach (var edge in document.Edges)
        {
            selection.SelectedEdgeIds.Add(edge.Id);
        }

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        sessionExecuteMove(document, selection, snapshot, new PointF(10, 0));

        Assert.Single(document.Polygons);
        Assert.Equal(16.0, PolygonGeometry.GetArea(document, document.Polygons[0], Tol), 3);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MoveSelection_NoDanglingReferences()
    {
        var document = CreateUnitSquare(size: 4);
        var selection = new Selection();
        selection.SelectedEdgeIds.Add(document.Edges.First().Id);

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        sessionExecuteMove(document, selection, snapshot, new PointF(3, 0));

        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MoveSelection_UndoRedo()
    {
        var session = new CadSession();
        var document = CreateUnitSquare(session.Document, size: 4);
        var fingerprintBefore = EdgeFingerprint(document);

        var selection = new Selection();
        foreach (var edge in document.Edges)
        {
            selection.SelectedEdgeIds.Add(edge.Id);
        }

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        session.History.Record(document);
        sessionExecuteMove(document, selection, snapshot, new PointF(5, 0));
        var fingerprintMoved = EdgeFingerprint(document);

        Assert.True(session.History.Undo(document, Tol));
        Assert.Equal(fingerprintBefore, EdgeFingerprint(document));

        Assert.True(session.History.Redo(document, Tol));
        Assert.Equal(fingerprintMoved, EdgeFingerprint(document));
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MovePreview_DoesNotMutateDocument()
    {
        RunSta(() =>
        {
            var harness = CreateMoveHarnessWithSquare();
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);
            harness.ToolService.ActivateTool(harness.MoveTool);

            harness.MoveTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.MoveTool.OnMouseMove(CreateMouseEvent(), new PointF(5, 5));

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
        });
    }

    [Fact]
    public void ActivatingMove_ShowsVertexHandlesForAllVertices()
    {
        RunSta(() =>
        {
            var harness = CreateMoveHarnessWithSquare();
            harness.ToolService.ActivateTool(harness.MoveTool);

            Assert.True(harness.MoveTool.ShowsVertexHandles);
            Assert.Equal(4, harness.Session.Document.Vertices.Count);
            Assert.Equal(
                harness.Session.Document.Vertices.Count,
                harness.Session.Document.Vertices.Count(vertex =>
                    VertexHandleRenderer.TryPickVertex(
                        harness.Session.Document,
                        vertex.Position,
                        harness.Session.Camera.Zoom,
                        Tol,
                        out _)));
        });
    }

    [Fact]
    public void VertexHandle_UsesExistingVertexId()
    {
        var document = CreateUnitSquare();
        var expected = document.Vertices.First(vertex =>
            MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tol));

        Assert.True(
            VertexHandleRenderer.TryPickVertex(document, expected.Position, zoom: 1, Tol, out var pickedId));
        Assert.Equal(expected.Id, pickedId);
    }

    [Fact]
    public void ClickingVertex_EntersVertexMoveMode()
    {
        RunSta(() =>
        {
            var harness = CreateMoveHarnessWithSquare();
            harness.ToolService.ActivateTool(harness.MoveTool);

            harness.MoveTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));

            Assert.Single(harness.Session.Selection.SelectedVertexIds);
            Assert.True(harness.MoveTool.IsInVertexMoveMode);
        });
    }

    [Fact]
    public void MovingVertexViaTool_ChangesVertexPositionOnly()
    {
        RunSta(() =>
        {
            var harness = CreateMoveHarnessWithSquare();
            SetPreciseMoveZoom(harness);
            var document = harness.Session.Document;
            var vertexId = document.Vertices.First(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tol)).Id;
            var countBefore = document.Vertices.Count;

            harness.ToolService.ActivateTool(harness.MoveTool);
            harness.MoveTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.MoveTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.MoveTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(1, 0));

            Assert.Equal(countBefore, document.Vertices.Count);
            Assert.Equal(1, TopologyService.GetVertexPosition(document, vertexId).X, 3);
            Assert.Equal(0, TopologyService.GetVertexPosition(document, vertexId).Y, 3);
            TopologyValidator.AssertValid(document, Tol);
        });
    }

    [Fact]
    public void MovingVertexViaTool_ConnectedEdgesFollow()
    {
        RunSta(() =>
        {
            var harness = CreateMoveHarnessWithSquare();
            SetPreciseMoveZoom(harness);
            var document = harness.Session.Document;
            var vertexId = document.Vertices.First(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tol)).Id;

            harness.ToolService.ActivateTool(harness.MoveTool);
            harness.MoveTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.MoveTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.MoveTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(2, 0));

            var moved = TopologyService.GetVertexPosition(document, vertexId);
            Assert.Contains(
                document.Edges,
                edge =>
                    MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(document, edge), moved, Tol) ||
                    MathUtils.ArePointsEqual(TopologyService.GetEdgeEndPoint(document, edge), moved, Tol));
            TopologyValidator.AssertValid(document, Tol);
        });
    }

    [Fact]
    public void RightClick_ClearsSelectionWhileStayingInMoveTool()
    {
        RunSta(() =>
        {
            var harness = CreateMoveHarnessWithSquare();
            harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);
            harness.ToolService.ActivateTool(harness.MoveTool);

            harness.MoveTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Right), new PointF(0, 0));

            Assert.True(harness.Session.Selection.IsEmpty);
            Assert.Same(harness.MoveTool, harness.ToolService.ActiveTool);
        });
    }

    [Fact]
    public void RightClickDuringActiveMove_CancelsPreviewAndRestoresDocument()
    {
        RunSta(() =>
        {
            var harness = CreateMoveHarnessWithSquare();
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);
            harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);
            harness.ToolService.ActivateTool(harness.MoveTool);

            harness.MoveTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.MoveTool.OnMouseMove(CreateMouseEvent(), new PointF(5, 5));
            harness.MoveTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Right), new PointF(0, 0));

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
            Assert.True(harness.Session.Selection.IsEmpty);
        });
    }

    [Fact]
    public void RightClick_DoesNotCreateUndoEntry()
    {
        RunSta(() =>
        {
            var harness = CreateMoveHarnessWithSquare();
            harness.Session.History.Record(harness.Session.Document);
            var undoDepth = harness.Session.History.CanUndo;
            harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);
            harness.ToolService.ActivateTool(harness.MoveTool);

            harness.MoveTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Right), new PointF(0, 0));

            Assert.Equal(undoDepth, harness.Session.History.CanUndo);
        });
    }

    [Fact]
    public void ObjectMove_ContinuesToWorkAfterVertexHandleChanges()
    {
        var document = CreateHorizontalChain();
        var selection = new Selection();
        selection.SelectedEdgeIds.Add(document.Edges[0].Id);
        var snapshot = MoveOperations.CreateSnapshot(document, selection);

        sessionExecuteMove(document, selection, snapshot, new PointF(0, 2));

        Assert.DoesNotContain(document.Edges, edge =>
            MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(document, edge), new PointF(0, 0), Tol));
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void VertexHandles_RemainCorrectScreenSizeAfterZoomAndPan()
    {
        var document = CreateUnitSquare();
        var radiusZoom1 = VertexHandleRenderer.GetHandleWorldRadius(1);
        var radiusZoom2 = VertexHandleRenderer.GetHandleWorldRadius(2);

        Assert.Equal(radiusZoom1 / 2, radiusZoom2, 6);

        var camera = new Camera();
        camera.PanScreen(120, -80);
        Assert.True(
            VertexHandleRenderer.TryPickVertex(document, new PointF(0, 0), camera.Zoom, Tol, out _));
    }

    private static void sessionExecuteMove(
        CadDocument document,
        Selection selection,
        MoveObjectSnapshot snapshot,
        PointF delta)
    {
        MoveOperations.ExecuteObjectMove(document, selection, snapshot, delta);
    }

    private static CadDocument CreateUnitSquare(CadDocument? document = null, float size = 1)
    {
        document ??= new CadDocument();
        AddSquare(document, 0, 0, size);
        PolygonBuilder.SyncFaces(document, Tol);
        return document;
    }

    private static CadDocument CreateHorizontalChain()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(1, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(1, 0), new PointF(2, 0), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        return document;
    }

    private static CadDocument CreateTwoRooms()
    {
        var document = new CadDocument();
        AddSquare(document, 0, 0, 4);
        AddSquare(document, 10, 0, 4);
        PolygonBuilder.SyncFaces(document, Tol);
        return document;
    }

    private static void AddSquare(CadDocument document, float x, float y, float size)
    {
        TestDocumentHelpers.AddEdge(document, new PointF(x, y), new PointF(x + size, y), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(x + size, y), new PointF(x + size, y + size), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(x + size, y + size), new PointF(x, y + size), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(x, y + size), new PointF(x, y), Tol);
    }

    private static float GetFaceMinX(CadDocument document, Polygon face)
    {
        var points = PolygonGeometry.GetOuterBoundaryPoints(document, face, Tol);
        return points.Count == 0 ? float.MaxValue : (float)points.Min(point => point.X);
    }

    private static string EdgeFingerprint(CadDocument document)
        => string.Join(
            ";",
            document.Edges
                .OrderBy(edge => edge.Id)
                .Select(edge =>
                {
                    var start = TopologyService.GetEdgeStartPoint(document, edge);
                    var end = TopologyService.GetEdgeEndPoint(document, edge);
                    return $"{start.X},{start.Y}->{end.X},{end.Y}";
                }));

    private static string DocumentFingerprint(CadDocument document)
    {
        var vertices = document.Vertices
            .OrderBy(vertex => vertex.Id)
            .Select(vertex => $"{vertex.Id}:{vertex.Position.X},{vertex.Position.Y}");
        var edges = document.Edges
            .OrderBy(edge => edge.Id)
            .Select(edge => $"{edge.Id}:{edge.StartVertexId}-{edge.EndVertexId}");
        return string.Join("|", document.Vertices.Count, document.Edges.Count, string.Join(";", vertices), string.Join(";", edges));
    }

    private static MoveHarness CreateMoveHarnessWithSquare()
    {
        var harness = new MoveHarness();
        CreateUnitSquare(harness.Session.Document);
        return harness;
    }

    private static void SetPreciseMoveZoom(MoveHarness harness)
        => harness.Session.Camera.ZoomAt(new Point(400, 300), 100, harness.ViewportSize);

    private static void RunSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }

    private static MouseButtonEventArgs CreateMouseButtonEvent(MouseButton button)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, button);
        args.RoutedEvent = button switch
        {
            MouseButton.Left => UIElement.MouseLeftButtonDownEvent,
            MouseButton.Right => UIElement.MouseRightButtonDownEvent,
            _ => UIElement.MouseDownEvent
        };
        return args;
    }

    private static MouseEventArgs CreateMouseEvent()
        => new(Mouse.PrimaryDevice, 0)
        {
            RoutedEvent = UIElement.MouseMoveEvent
        };

    private sealed class MoveHarness
    {
        public MoveHarness()
        {
            Session = new CadSession();
            ToolService = Session.ToolService;
            MoveTool = new MoveTool();

            var context = new ToolContext(
                Session,
                () => ViewportSize,
                screen => Session.Camera.ScreenToWorld(screen, ViewportSize),
                _ => new Point(0, 0),
                () => RedrawRequested = true,
                () => { },
                () => Session.History.Record(Session.Document));

            ToolService.Initialize(context);
        }

        public CadSession Session { get; }

        public ToolService ToolService { get; }

        public MoveTool MoveTool { get; }

        public Size ViewportSize { get; } = new(800, 600);

        public bool RedrawRequested { get; private set; }
    }
}
