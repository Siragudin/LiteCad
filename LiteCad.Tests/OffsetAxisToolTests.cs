using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using System.Reflection;
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

    [Fact]
    public void AxisOffset_MouseMove_IncludesEdgeEndpointSnap()
    {
        RunSta(() =>
        {
            using var harness = CreateAxisHarness();
            SetPreciseOffsetZoom(harness);
            harness.Session.OffsetToolOptions.IsAxisOffset = true;
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 0));
            harness.OffsetTool.OnMouseMove(CreateMouseEvent(), new PointF(100, 50.05));

            var visibleSnaps = GetVisibleSnaps(harness.OffsetTool);
            Assert.Contains(visibleSnaps, snap => snap.Kind == SnapKind.Endpoint);
            Assert.Contains(
                visibleSnaps,
                snap => MathUtils.ArePointsEqual(snap.Position, new PointF(100, 50), Tol));
        });
    }

    [Fact]
    public void AxisOffset_MouseMove_UsesStandardSnapResolverForDistance()
    {
        RunSta(() =>
        {
            using var harness = CreateAxisHarness();
            SetPreciseOffsetZoom(harness);
            harness.Session.OffsetToolOptions.IsAxisOffset = true;
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 0));
            harness.OffsetTool.OnMouseMove(CreateMouseEvent(), new PointF(100, 50.05));

            Assert.Equal(50, harness.OffsetTool.PreviewSignedDistance, 1);
        });
    }

    [Fact]
    public void AxisOffset_MouseMove_IncludesTouchPointAtAxisEndpoint()
    {
        RunSta(() =>
        {
            using var harness = CreateAxisHarness();
            TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(100, 0), new PointF(100, 50), Tol);
            SetPreciseOffsetZoom(harness);
            harness.Session.OffsetToolOptions.IsAxisOffset = true;
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 0));
            harness.OffsetTool.OnMouseMove(CreateMouseEvent(), new PointF(100.05f, 0.05f));

            var snapTolerance = MathUtils.SnapToleranceWorld(harness.Session.Camera.Zoom);
            var snap = harness.Session.SnapService.FindBestSnap(
                harness.Session.Document,
                new PointF(100.05f, 0.05f),
                snapTolerance,
                includeOnEdge: true);
            Assert.True(snap.HasSnap);
            Assert.True(
                snap.Snap!.Value.Kind is SnapKind.Endpoint or SnapKind.Intersection,
                $"Expected endpoint or intersection snap, got {snap.Snap.Value.Kind}");
        });
    }

    [Fact]
    public void AxisOffset_Commit_InvalidatesSnapCache()
    {
        RunSta(() =>
        {
            using var harness = CreateAxisHarness();
            SetPreciseOffsetZoom(harness);
            harness.Session.OffsetToolOptions.IsAxisOffset = true;
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.Session.SnapService.GetVisibleSnaps(
                harness.Session.Document,
                new PointF(50, 0),
                MathUtils.SnapToleranceWorld(harness.Session.Camera.Zoom));
            Assert.NotNull(GetSnapCache(harness.Session.SnapService));

            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 0));
            harness.OffsetTool.OnMouseMove(CreateMouseEvent(), new PointF(50, 20));
            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 20));

            Assert.Null(GetSnapCache(harness.Session.SnapService));
        });
    }

    [Fact]
    public void AxisOffset_CommitCreatedEdge_SupportsEndpointSnap()
    {
        RunSta(() =>
        {
            using var harness = CreateAxisHarness();
            SetPreciseOffsetZoom(harness);
            harness.Session.OffsetToolOptions.IsAxisOffset = true;
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 0));
            harness.OffsetTool.OnMouseMove(CreateMouseEvent(), new PointF(50, 20));
            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 20));

            var touch = new PointF(100, 20);
            var snap = harness.Session.SnapService.FindBestSnap(
                harness.Session.Document,
                touch,
                MathUtils.SnapToleranceWorld(harness.Session.Camera.Zoom));

            Assert.True(snap.HasSnap);
            Assert.Equal(SnapKind.Endpoint, snap.Snap!.Value.Kind);
            Assert.True(MathUtils.ArePointsEqual(snap.Snap.Value.Position, touch, 1));
        });
    }

    [Fact]
    public void AxisOffset_CommitCreatedEdge_SupportsDimensionAnchor()
    {
        RunSta(() =>
        {
            using var harness = CreateAxisHarness();
            SetPreciseOffsetZoom(harness);
            harness.Session.OffsetToolOptions.IsAxisOffset = true;
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(50, 0));
            harness.OffsetTool.TryApplyLengthInput("20");

            var endpoint = new PointF(0, 20);
            var snap = harness.Session.SnapService.FindBestSnap(
                harness.Session.Document,
                endpoint,
                MathUtils.SnapToleranceWorld(harness.Session.Camera.Zoom));

            Assert.True(snap.HasSnap);
            Assert.True(SnapService.TryResolveMeasurementAnchor(
                harness.Session.Document,
                snap.Snap!.Value,
                Tol,
                out var vertexId,
                out var anchor));
            Assert.NotEqual(Guid.Empty, vertexId);
            Assert.True(MathUtils.ArePointsEqual(anchor, endpoint, Tol));
        });
    }

    private static IReadOnlyList<SnapPoint> GetVisibleSnaps(OffsetTool offsetTool)
    {
        var field = typeof(OffsetTool).GetField("_visibleSnaps", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return ((IEnumerable<SnapPoint>)field.GetValue(offsetTool)!).ToList();
    }

    private static object? GetSnapCache(SnapService snapService)
    {
        var field = typeof(SnapService).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return field.GetValue(snapService);
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
