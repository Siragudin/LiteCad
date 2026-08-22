using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class OffsetAxisToolTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void AxisOffset_ClickAxis_SelectsAxis()
    {
        RunSta(() =>
        {
            using var harness = CreateAxisHarness();
            harness.Session.OffsetToolOptions.IsAxisOffset = true;
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 0));

            Assert.True(harness.OffsetTool.HasActiveAxisOffset);
            Assert.Single(harness.Session.Selection.SelectedAxisIds);
            Assert.Empty(harness.Session.Selection.SelectedEdgeIds);
        });
    }

    [Fact]
    public void AxisOffset_ClickEdge_IsIgnored()
    {
        RunSta(() =>
        {
            using var harness = CreateAxisHarness();
            harness.Session.OffsetToolOptions.IsAxisOffset = true;
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 50));

            Assert.False(harness.OffsetTool.HasActiveAxisOffset);
            Assert.True(harness.Session.Selection.IsEmpty);
        });
    }

    [Fact]
    public void AxisOffset_CommitCreatesEdgeAndPreservesAxis()
    {
        RunSta(() =>
        {
            using var harness = CreateAxisHarness();
            SetPreciseOffsetZoom(harness);
            harness.Session.OffsetToolOptions.IsAxisOffset = true;
            harness.ToolService.ActivateTool(harness.OffsetTool);
            var axis = harness.Session.Document.Axes.Single();
            var axisCountBefore = harness.Session.Document.Axes.Count;
            var edgeCountBefore = harness.Session.Document.Edges.Count;

            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 0));
            harness.OffsetTool.OnMouseMove(CreateMouseEvent(), new PointF(50, 20));
            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 20));

            Assert.Equal(axisCountBefore, harness.Session.Document.Axes.Count);
            Assert.Contains(axis.Id, harness.Session.Document.Axes.Select(item => item.Id));
            Assert.Equal(edgeCountBefore + 1, harness.Session.Document.Edges.Count);
            Assert.False(harness.OffsetTool.HasActiveAxisOffset);

            var edge = harness.Session.Document.Edges.Single(item =>
                MathUtils.ArePointsEqual(
                    TopologyService.GetEdgeStartPoint(harness.Session.Document, item),
                    new PointF(0, 20),
                    1)
                || MathUtils.ArePointsEqual(
                    TopologyService.GetEdgeEndPoint(harness.Session.Document, item),
                    new PointF(100, 20),
                    1));

            Assert.NotNull(edge);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void AxisOffset_RightClick_KeepsToolActiveAndCheckboxEnabled()
    {
        RunSta(() =>
        {
            using var harness = CreateAxisHarness();
            SetPreciseOffsetZoom(harness);
            harness.Session.OffsetToolOptions.IsAxisOffset = true;
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 0));
            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Right), new PointF(50, 0));

            Assert.False(harness.OffsetTool.HasActiveAxisOffset);
            Assert.Same(harness.OffsetTool, harness.Session.ToolService.ActiveTool);
            Assert.True(harness.Session.OffsetToolOptions.IsAxisOffset);
        });
    }

    [Fact]
    public void AxisOffset_RightClick_AllowsNewOperationImmediately()
    {
        RunSta(() =>
        {
            using var harness = CreateAxisHarness();
            SetPreciseOffsetZoom(harness);
            harness.Session.OffsetToolOptions.IsAxisOffset = true;
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 0));
            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Right), new PointF(50, 0));
            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 0));
            harness.OffsetTool.OnMouseMove(CreateMouseEvent(), new PointF(50, 15));
            harness.OffsetTool.TryApplyLengthInput("15");

            Assert.Equal(2, harness.Session.Document.Edges.Count);
            Assert.True(harness.Session.OffsetToolOptions.IsAxisOffset);
        });
    }

    [Fact]
    public void AxisOffset_ModeSwitch_CancelsPendingOperation()
    {
        RunSta(() =>
        {
            using var harness = CreateAxisHarness();
            SetPreciseOffsetZoom(harness);
            harness.Session.OffsetToolOptions.IsAxisOffset = true;
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 0));
            Assert.True(harness.OffsetTool.HasActiveAxisOffset);

            harness.Session.OffsetToolOptions.IsAxisOffset = false;
            harness.OffsetTool.CancelPendingOperation();

            Assert.False(harness.OffsetTool.HasActiveAxisOffset);
            Assert.Same(harness.OffsetTool, harness.Session.ToolService.ActiveTool);
        });
    }

    [Fact]
    public void NormalOffset_WithAxisModeDisabled_StillOffsetsFace()
    {
        RunSta(() =>
        {
            using var harness = CreateAxisHarness(withSquareFace: true);
            SetPreciseOffsetZoom(harness);
            harness.Session.OffsetToolOptions.IsAxisOffset = false;
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(2, 2));
            harness.OffsetTool.OnMouseMove(CreateMouseEvent(), new PointF(2, -1));
            harness.OffsetTool.TryApplyLengthInput("1");

            Assert.Equal(2, harness.Session.Document.Polygons.Count(polygon => polygon.Type == PolygonType.Face));
            Assert.False(harness.OffsetTool.HasActiveOffset);
        });
    }

    [Fact]
    public void AxisOffset_UndoRemovesCreatedEdgeOnly()
    {
        RunSta(() =>
        {
            using var harness = CreateAxisHarness();
            SetPreciseOffsetZoom(harness);
            harness.Session.OffsetToolOptions.IsAxisOffset = true;
            harness.ToolService.ActivateTool(harness.OffsetTool);
            var axisCountBefore = harness.Session.Document.Axes.Count;
            var edgeCountBefore = harness.Session.Document.Edges.Count;

            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 0));
            harness.OffsetTool.OnMouseMove(CreateMouseEvent(), new PointF(50, 20));
            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 20));

            Assert.True(harness.Session.History.Undo(harness.Session.Document, Tol));
            Assert.Equal(edgeCountBefore, harness.Session.Document.Edges.Count);
            Assert.Equal(axisCountBefore, harness.Session.Document.Axes.Count);
        });
    }

    private static OffsetToolHarness CreateAxisHarness(bool withSquareFace = false)
    {
        var harness = new OffsetToolHarness();
        AxisService.Create(harness.Session.Document, new PointF(0, 0), new PointF(100, 0), Tol);
        TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(0, 50), new PointF(100, 50), Tol);

        if (withSquareFace)
        {
            TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(0, 0), new PointF(4, 0), Tol);
            TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(4, 0), new PointF(4, 4), Tol);
            TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(4, 4), new PointF(0, 4), Tol);
            TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(0, 4), new PointF(0, 0), Tol);
            PolygonBuilder.SyncFaces(harness.Session.Document, Tol);
        }

        return harness;
    }

    private static void SetPreciseOffsetZoom(OffsetToolHarness harness)
        => harness.Session.Camera.ZoomAt(new Point(400, 300), 100, harness.ViewportSize);

    private static void RunSta(Action action) => WpfTestUtilities.RunSta(action);

    private static MouseButtonEventArgs CreateMouseButton(MouseButton button)
        => new(Mouse.PrimaryDevice, 0, button)
        {
            RoutedEvent = button == MouseButton.Left
                ? UIElement.MouseLeftButtonDownEvent
                : UIElement.MouseRightButtonDownEvent
        };

    private static MouseEventArgs CreateMouseEvent()
        => new(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseMoveEvent };

    private sealed class OffsetToolHarness : IDisposable
    {
        public OffsetToolHarness()
        {
            Session = new CadSession();
            ToolService = Session.ToolService;
            OffsetTool = new OffsetTool();

            var context = new ToolContext(
                Session,
                () => ViewportSize,
                screen => Session.Camera.ScreenToWorld(screen, ViewportSize),
                _ => new Point(),
                () => { },
                () => { },
                () => { },
                setLineInputModeEnabled: (_, _) => { },
                recordUndo: () => Session.History.Record(Session.Document));

            ToolService.Initialize(context);
        }

        public CadSession Session { get; }

        public ToolService ToolService { get; }

        public OffsetTool OffsetTool { get; }

        public Size ViewportSize { get; } = new(800, 600);

        public void Dispose()
        {
        }
    }
}
