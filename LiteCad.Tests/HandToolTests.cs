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

public class HandToolTests
{
    private const double Tolerance = 1e-4;

    [Fact]
    public void Pan_UpdatesPanOffsetByScreenDelta()
    {
        RunSta(() =>
        {
            var harness = new HandToolHarness();
            var initial = harness.Session.Camera.PanOffset;

            harness.DragPan(fromX: 100, fromY: 200, toX: 150, toY: 260);

            var pan = harness.Session.Camera.PanOffset;
            Assert.Equal(initial.X + 50, pan.X, 3);
            Assert.Equal(initial.Y + 60, pan.Y, 3);
            Assert.True(harness.RedrawRequested);
        });
    }

    [Fact]
    public void Pan_DoesNotModifyDocumentTopology()
    {
        RunSta(() =>
        {
            var harness = CreateDocumentHarness();
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.DragPan(fromX: 0, fromY: 0, toX: 120, toY: -80);

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
        });
    }

    [Fact]
    public void Pan_DoesNotRecordUndo()
    {
        RunSta(() =>
        {
            var harness = CreateDocumentHarness();
            harness.Session.History.Record(harness.Session.Document);
            var canUndoBefore = harness.Session.History.CanUndo;
            var canRedoBefore = harness.Session.History.CanRedo;

            harness.DragPan(fromX: 10, fromY: 10, toX: 40, toY: 55);

            Assert.Equal(canUndoBefore, harness.Session.History.CanUndo);
            Assert.Equal(canRedoBefore, harness.Session.History.CanRedo);
        });
    }

    [Fact]
    public void Pan_ScreenDeltaIsIndependentOfZoom()
    {
        RunSta(() =>
        {
            var harness = new HandToolHarness();
            harness.Session.Camera.ZoomAt(new Point(400, 300), 2.0, harness.ViewportSize);
            var initial = harness.Session.Camera.PanOffset;

            harness.DragPan(fromX: 200, fromY: 200, toX: 230, toY: 215);

            var pan = harness.Session.Camera.PanOffset;
            Assert.Equal(initial.X + 30, pan.X, 3);
            Assert.Equal(initial.Y + 15, pan.Y, 3);
        });
    }

    [Fact]
    public void AfterHandTool_DeactivatedLineToolStillCreatesEdge()
    {
        RunSta(() =>
        {
            var harness = CreateDocumentHarness();
            var lineTool = new LineTool();

            harness.ToolService.ActivateTool(harness.HandTool);
            harness.DragPan(fromX: 50, fromY: 50, toX: 90, toY: 70);
            harness.ToolService.ActivateTool(lineTool);

            var mouseDown = CreateMouseButtonEvent(MouseButton.Left);
            lineTool.OnMouseDown(mouseDown, new PointF(0, 50));
            lineTool.OnMouseDown(mouseDown, new PointF(100, 50));

            Assert.Equal(2, harness.Session.Document.Edges.Count);
            Assert.Equal(4, harness.Session.Document.Vertices.Count);
            TopologyValidator.AssertValid(harness.Session.Document, Tolerance);
        });
    }

    private static void RunSta(Action action) => WpfTestUtilities.RunSta(action);

    private static HandToolHarness CreateDocumentHarness()
    {
        var harness = new HandToolHarness();
        TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(0, 0), new PointF(100, 0), Tolerance);
        return harness;
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

        return string.Join(
            "|",
            document.Vertices.Count,
            document.Edges.Count,
            document.Polygons.Count,
            string.Join(";", vertices),
            string.Join(";", edges),
            string.Join(";", polygons));
    }

    private static MouseButtonEventArgs CreateMouseButtonEvent(MouseButton button)
        => new(Mouse.PrimaryDevice, 0, button)
        {
            RoutedEvent = button == MouseButton.Left ? UIElement.MouseLeftButtonDownEvent : UIElement.MouseDownEvent
        };

    private static MouseEventArgs CreateMouseEvent()
        => new(Mouse.PrimaryDevice, 0)
        {
            RoutedEvent = UIElement.MouseMoveEvent
        };

    private sealed class HandToolHarness
    {
        private Point _screenPosition;

        public HandToolHarness()
        {
            Session = new CadSession();
            ToolService = Session.ToolService;
            HandTool = new HandTool();

            var context = new ToolContext(
                Session,
                () => ViewportSize,
                screen => Session.Camera.ScreenToWorld(screen, ViewportSize),
                _ => _screenPosition,
                () => RedrawRequested = true,
                () => { },
                () => { },
                () => { });

            Session.Camera.Changed += () => RedrawRequested = true;

            ToolService.Initialize(context);
        }

        public CadSession Session { get; }

        public ToolService ToolService { get; }

        public HandTool HandTool { get; }

        public Size ViewportSize { get; } = new(800, 600);

        public bool RedrawRequested { get; private set; }

        public void DragPan(double fromX, double fromY, double toX, double toY)
        {
            ToolService.ActivateTool(HandTool);
            RedrawRequested = false;

            SetScreen(fromX, fromY);
            HandTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), PointF.Zero);

            SetScreen(toX, toY);
            HandTool.OnMouseMove(CreateMouseEvent(), PointF.Zero);

            HandTool.OnMouseUp(CreateMouseButtonEvent(MouseButton.Left), PointF.Zero);
        }

        private void SetScreen(double x, double y) => _screenPosition = new Point(x, y);
    }
}
