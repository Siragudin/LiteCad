using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class SelectionEdgePickTests
{
    private const double Tol = 1e-4;

    [Theory]
    [InlineData(1.0)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    public void TryPickEdge_SelectsShortHorizontalEdgeAtMidpoint(double lengthMm)
    {
        var document = new CadDocument();
        var edge = TestDocumentHelpers.AddEdge(document, new PointF(100, 100), new PointF(100 + (float)lengthMm, 100), Tol);
        var tolerance = MathUtils.SelectionPickToleranceWorld(zoom: 1);

        Assert.True(
            SelectionPickOperations.TryPickEdge(
                document,
                new PointF(100 + (float)(lengthMm / 2), 100),
                tolerance,
                out var pickedId));
        Assert.Equal(edge.Id, pickedId);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(1.0)]
    [InlineData(10.0)]
    [InlineData(50.0)]
    public void SelectionPickToleranceWorld_ScalesWithZoom(double zoom)
    {
        var tolerance = MathUtils.SelectionPickToleranceWorld(zoom);
        Assert.Equal(MathUtils.SelectionPickPixels / zoom, tolerance, precision: 6);
    }

    [Fact]
    public void SnapToleranceWorld_IsUnchanged_At12Pixels()
    {
        Assert.Equal(12.0, MathUtils.SnapTolerancePixels);
        Assert.Equal(12.0 / 3.0, MathUtils.SnapToleranceWorld(3.0), precision: 6);
        Assert.Equal(8.0, MathUtils.SelectionPickPixels);
        Assert.NotEqual(MathUtils.SnapToleranceWorld(1), MathUtils.SelectionPickToleranceWorld(1));
    }

    [Fact]
    public void TryHitTestSegment_InsideTolerance_Hits()
    {
        var tolerance = MathUtils.SelectionPickToleranceWorld(1);
        Assert.True(
            Geometry2D.TryHitTestSegment(
                new PointF(50, tolerance * 0.5),
                new PointF(0, 0),
                new PointF(100, 0),
                tolerance,
                out var distance));
        Assert.True(distance <= tolerance);
    }

    [Fact]
    public void TryHitTestSegment_OutsideTolerance_Misses()
    {
        var tolerance = MathUtils.SelectionPickToleranceWorld(1);
        Assert.False(
            Geometry2D.TryHitTestSegment(
                new PointF(50, tolerance * 2),
                new PointF(0, 0),
                new PointF(100, 0),
                tolerance,
                out _));
    }

    [Fact]
    public void TryHitTestSegment_EndpointCap_HitsJustBeyondEnd()
    {
        var tolerance = MathUtils.SelectionPickToleranceWorld(1);
        Assert.True(
            Geometry2D.TryHitTestSegment(
                new PointF(100 + tolerance * 0.5, 0),
                new PointF(0, 0),
                new PointF(100, 0),
                tolerance,
                out _));
    }

    [Fact]
    public void TryHitTestSegment_EndpointCap_MissesFarBeyondEnd()
    {
        var tolerance = MathUtils.SelectionPickToleranceWorld(1);
        Assert.False(
            Geometry2D.TryHitTestSegment(
                new PointF(100 + tolerance * 2, 0),
                new PointF(0, 0),
                new PointF(100, 0),
                tolerance,
                out _));
    }

    [Fact]
    public void TryHitTestSegment_DegenerateEdge_IsSelectable()
    {
        var tolerance = MathUtils.SelectionPickToleranceWorld(1);
        Assert.True(
            Geometry2D.TryHitTestSegment(
                new PointF(10, 10.5),
                new PointF(10, 10),
                new PointF(10, 10),
                tolerance,
                out var distance));
        Assert.True(distance <= tolerance);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(1.0)]
    [InlineData(10.0)]
    [InlineData(50.0)]
    public void TryPickEdge_ShortEdge_SelectableAtAllZoomLevels(double zoom)
    {
        var document = new CadDocument();
        var edge = TestDocumentHelpers.AddEdge(document, new PointF(200, 200), new PointF(205, 200), Tol);
        var tolerance = MathUtils.SelectionPickToleranceWorld(zoom);

        Assert.True(
            SelectionPickOperations.TryPickEdge(document, new PointF(202.5, 200), tolerance, out var pickedId));
        Assert.Equal(edge.Id, pickedId);
    }

    [Fact]
    public void TryProjectPointOnSegment_StillRejectsShortSegments_ForSnapPaths()
    {
        var snapTolerance = MathUtils.SnapToleranceWorld(1);
        Assert.False(
            Geometry2D.TryProjectPointOnSegment(
                new PointF(0.5, 0),
                new PointF(0, 0),
                new PointF(1, 0),
                out _,
                out _,
                snapTolerance));
    }

    [Fact]
    public void TryPickEdge_LongEdge_StillSelectable()
    {
        var document = new CadDocument();
        var edge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(200, 0), Tol);
        var tolerance = MathUtils.SelectionPickToleranceWorld(1);

        Assert.True(
            SelectionPickOperations.TryPickEdge(document, new PointF(100, 2), tolerance, out var pickedId));
        Assert.Equal(edge.Id, pickedId);
    }

    [Fact]
    public void PickAt_PrefersVertexBeforeEdge_WhenBothInRange()
    {
        var document = new CadDocument();
        var edge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        var snapTolerance = MathUtils.SnapToleranceWorld(1);
        var pickTolerance = MathUtils.SelectionPickToleranceWorld(1);

        var pick = SelectionPickOperations.PickAt(document, new PointF(0, 0), snapTolerance, pickTolerance);

        Assert.NotNull(pick);
        Assert.Equal(SelectionPickKind.Vertex, pick.Value.Kind);
        Assert.NotEqual(edge.Id, pick.Value.Id);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    public void SelectionTool_SelectsShortEdgeAtMidpoint(double lengthMm)
    {
        WpfTestUtilities.RunSta(() =>
        {
            var session = new CadSession();
            session.Camera.Reset();
            var document = session.Document;
            var edge = TestDocumentHelpers.AddEdge(
                document,
                new PointF(300, 300),
                new PointF(300 + (float)lengthMm, 300),
                Tol);

            var harness = new SelectionToolHarness(session);
            harness.ToolService.ActivateTool(harness.SelectionTool);

            var pickPoint = new PointF(300 + (float)(lengthMm / 2), 300);
            harness.SelectionTool.OnMouseDown(CreateMouseButton(MouseButton.Left), pickPoint);
            harness.SelectionTool.OnMouseUp(CreateMouseButton(MouseButton.Left, isUp: true), pickPoint);

            Assert.Single(session.Selection.SelectedEdgeIds);
            Assert.Equal(edge.Id, session.Selection.SelectedEdgeIds.First());
            Assert.Empty(session.Selection.SelectedVertexIds);
        });
    }

    private static MouseButtonEventArgs CreateMouseButton(MouseButton button, bool isUp = false)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, button)
        {
            RoutedEvent = isUp ? UIElement.MouseLeftButtonUpEvent : UIElement.MouseLeftButtonDownEvent
        };
        return args;
    }

    private sealed class SelectionToolHarness
    {
        public SelectionToolHarness(CadSession session)
        {
            Session = session;
            ToolService = session.ToolService;
            SelectionTool = new SelectionTool();
            ToolService.Initialize(new ToolContext(
                Session,
                () => new Size(800, 600),
                _ => PointF.Zero,
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                () => { }));
        }

        public CadSession Session { get; }

        public ToolService ToolService { get; }

        public SelectionTool SelectionTool { get; }
    }
}
