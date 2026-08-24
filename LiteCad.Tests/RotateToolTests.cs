using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class RotateToolTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void RotateOperations_RotatesEdge90DegreesAroundOrigin()
    {
        var document = CreateUnitSquare();
        var selection = new Selection();
        foreach (var edge in document.Edges)
        {
            selection.SelectedEdgeIds.Add(edge.Id);
        }

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        var pivot = new PointF(0, 0);
        var angle = Math.PI / 2.0;

        RotateOperations.ExecuteObjectRotate(document, selection, snapshot, pivot, angle);

        var topEdge = document.Edges.Single(edge =>
            MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(document, edge), new PointF(0, 0), Tol));
        var end = TopologyService.GetEdgeEndPoint(document, topEdge);
        Assert.True(MathUtils.ArePointsEqual(end, new PointF(0, 4), Tol));
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void RotateTool_PreviewDoesNotModifyDocument()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquareSelection();
            SetPreciseRotateZoom(harness);
            harness.ToolService.ActivateTool(harness.RotateTool);
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.RotateTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.RotateTool.OnMouseMove(CreateMouseEvent(), new PointF(4, 0));

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
            Assert.True(harness.RotateTool.HasPivot);
        });
    }

    [Fact]
    public void RotateTool_CommitRotatesSelection()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquareSelection();
            SetPreciseRotateZoom(harness);
            harness.ToolService.ActivateTool(harness.RotateTool);

            harness.RotateTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.RotateTool.OnMouseMove(CreateMouseEvent(), new PointF(0, 4));
            harness.RotateTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 4));

            var rotatedEdge = harness.Session.Document.Edges.Single(edge =>
                MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(harness.Session.Document, edge), new PointF(0, 0), Tol));
            var end = TopologyService.GetEdgeEndPoint(harness.Session.Document, rotatedEdge);
            Assert.True(MathUtils.ArePointsEqual(end, new PointF(0, 4), Tol));
            Assert.False(harness.RotateTool.HasPivot);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void RotateTool_UndoRedo()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquareSelection();
            SetPreciseRotateZoom(harness);
            harness.ToolService.ActivateTool(harness.RotateTool);

            harness.RotateTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.RotateTool.OnMouseMove(CreateMouseEvent(), new PointF(0, 4));
            harness.RotateTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 4));

            Assert.Equal(4, harness.Session.Document.Edges.Count);

            Assert.True(harness.Session.History.Undo(harness.Session.Document, Tol));
            var restoredEdge = harness.Session.Document.Edges.Single(edge =>
                MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(harness.Session.Document, edge), new PointF(0, 0), Tol));
            var restoredEnd = TopologyService.GetEdgeEndPoint(harness.Session.Document, restoredEdge);
            Assert.True(MathUtils.ArePointsEqual(restoredEnd, new PointF(4, 0), Tol));

            Assert.True(harness.Session.History.Redo(harness.Session.Document, Tol));
            var redoneEnd = TopologyService.GetEdgeEndPoint(
                harness.Session.Document,
                harness.Session.Document.Edges.Single(edge =>
                    MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(harness.Session.Document, edge), new PointF(0, 0), Tol)));
            Assert.True(MathUtils.ArePointsEqual(redoneEnd, new PointF(0, 4), Tol));
        });
    }

    [Fact]
    public void RotateTool_RequiresSelection()
    {
        RunSta(() =>
        {
            var harness = new RotateToolHarness();
            SetPreciseRotateZoom(harness);
            harness.Session.Selection.Clear();
            harness.ToolService.ActivateTool(harness.RotateTool);

            harness.RotateTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));

            Assert.False(harness.RotateTool.HasPivot);
            Assert.Equal(0, harness.Session.Document.Edges.Count);
        });
    }

    [Fact]
    public void RotateTool_RightClickCancelsPreview()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquareSelection();
            SetPreciseRotateZoom(harness);
            harness.ToolService.ActivateTool(harness.RotateTool);
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.RotateTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            Assert.True(harness.RotateTool.HasPivot);

            harness.RotateTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Right), new PointF(0, 0));

            Assert.False(harness.RotateTool.HasPivot);
            Assert.True(harness.Session.Selection.IsEmpty);
            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
            Assert.True(harness.Session.ToolService.ActiveTool is RotateTool);
        });
    }

    private static CadDocument CreateUnitSquare()
    {
        var document = new CadDocument();
        AddSquare(document, 0, 0, 4);
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

    private static string DocumentFingerprint(CadDocument document)
    {
        var edges = document.Edges
            .OrderBy(edge => edge.Id)
            .Select(edge =>
            {
                var start = TopologyService.GetEdgeStartPoint(document, edge);
                var end = TopologyService.GetEdgeEndPoint(document, edge);
                return $"{edge.Id}:{start.X},{start.Y}-{end.X},{end.Y}";
            });

        return string.Join("|", document.Edges.Count, string.Join(";", edges));
    }

    private static RotateToolHarness CreateHarnessWithSquareSelection()
    {
        var harness = new RotateToolHarness();
        AddSquare(harness.Session.Document, 0, 0, 4);
        PolygonBuilder.SyncFaces(harness.Session.Document, Tol);
        foreach (var edge in harness.Session.Document.Edges)
        {
            harness.Session.Selection.SelectedEdgeIds.Add(edge.Id);
        }

        return harness;
    }

    private static void SetPreciseRotateZoom(RotateToolHarness harness)
        => harness.Session.Camera.ZoomAt(new Point(400, 300), 100, harness.ViewportSize);

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

    private sealed class RotateToolHarness
    {
        public RotateToolHarness()
        {
            Session = new CadSession();
            ToolService = Session.ToolService;
            RotateTool = new RotateTool();

            var context = new ToolContext(
                Session,
                () => ViewportSize,
                screen => Session.Camera.ScreenToWorld(screen, ViewportSize),
                _ => new Point(),
                () => RedrawRequested = true,
                () => { },
                () => { },
                () => { },
                recordUndo: () => Session.History.Record(Session.Document));

            ToolService.Initialize(context);
        }

        public CadSession Session { get; }

        public ToolService ToolService { get; }

        public RotateTool RotateTool { get; }

        public Size ViewportSize { get; } = new(800, 600);

        public bool RedrawRequested { get; private set; }
    }
}
