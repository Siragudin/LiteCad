using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Services;
using LiteCad.Tools;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class DimensionToolPhase3PreviewTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void Phase3_MouseMove_DoesNotCreateDimensionInDocument()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            EnterOffsetPhase(harness);

            harness.MoveMouse(new PointF(100, 80));
            harness.MoveMouse(new PointF(100, 120));

            Assert.Empty(harness.Session.Document.Dimensions);
        });
    }

    [Fact]
    public void Phase3_MouseMove_DoesNotCreateUndoEntry()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.Session.History.Record(harness.Session.Document);
            var undoDepth = harness.Session.History.CanUndo;

            EnterOffsetPhase(harness);
            harness.MoveMouse(new PointF(100, 80));
            harness.MoveMouse(new PointF(100, 120));

            Assert.Equal(undoDepth, harness.Session.History.CanUndo);
        });
    }

    [Fact]
    public void Phase3_MouseMove_DoesNotMutateDocument()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            EnterOffsetPhase(harness);
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.MoveMouse(new PointF(100, 80));
            harness.MoveMouse(new PointF(100, 120));

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
        });
    }

    [Fact]
    public void Phase3_MouseMove_PreservesExistingDimensions()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            var existing = DimensionService.Create(
                harness.Session.Document,
                harness.Edge.StartVertexId,
                harness.Edge.EndVertexId,
                25,
                Tol);
            existing.TextSize = 33;
            existing.ExtensionStyle = DimensionExtensionStyle.Short;

            EnterOffsetPhase(harness);
            harness.MoveMouse(new PointF(50, 120));
            harness.MoveMouse(new PointF(50, 140));

            Assert.Single(harness.Session.Document.Dimensions);
            var unchanged = harness.Session.Document.Dimensions.Single();
            Assert.Equal(existing.Id, unchanged.Id);
            Assert.Equal(25, unchanged.Offset, 3);
            Assert.Equal(33, unchanged.TextSize, 3);
            Assert.Equal(DimensionExtensionStyle.Short, unchanged.ExtensionStyle);
        });
    }

    [Fact]
    public void FormattedTextCache_ReusesWhenDistanceTextUnchanged()
    {
        RunSta(() =>
        {
            var cache = new DimensionFormattedTextCache();
            var pixelsPerDip = DimensionFormattedTextCache.ResolvePixelsPerDip();

            var first = cache.GetOrCreate("125.0", 12, Colors.Blue, pixelsPerDip);
            var second = cache.GetOrCreate("125.0", 12, Colors.Blue, pixelsPerDip);

            Assert.Same(first, second);
            Assert.Equal(1, cache.CreateCount);
            Assert.Equal(1, cache.ReuseCount);
        });
    }

    [Fact]
    public void FormattedTextCache_RecreatesWhenDistanceTextChanges()
    {
        RunSta(() =>
        {
            var cache = new DimensionFormattedTextCache();
            var pixelsPerDip = DimensionFormattedTextCache.ResolvePixelsPerDip();

            var first = cache.GetOrCreate("125.0", 12, Colors.Blue, pixelsPerDip);
            var second = cache.GetOrCreate("126.0", 12, Colors.Blue, pixelsPerDip);

            Assert.NotSame(first, second);
            Assert.Equal(2, cache.CreateCount);
            Assert.Equal(0, cache.ReuseCount);
        });
    }

    [Fact]
    public void PreviewRenderer_ReusesTextWhenOnlyPositionChanges()
    {
        RunSta(() =>
        {
            var renderer = new DimensionPreviewRenderer();
            var camera = new Camera();
            var viewport = new Size(800, 600);
            var pixelsPerDip = DimensionFormattedTextCache.ResolvePixelsPerDip();

            Assert.True(DimensionGeometry.TryCreateLayout(
                new PointF(0, 0),
                new PointF(100, 0),
                50,
                Tol,
                out var layoutNear,
                isOrthogonal: false,
                orthogonalIsHorizontal: false));
            Assert.True(DimensionGeometry.TryCreateLayout(
                new PointF(0, 0),
                new PointF(100, 0),
                55,
                Tol,
                out var layoutFar,
                isOrthogonal: false,
                orthogonalIsHorizontal: false));

            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                renderer.Draw(
                    context,
                    layoutNear,
                    camera.Zoom,
                    CanvasTheme.Preview,
                    camera,
                    viewport,
                    "100.0",
                    DimensionExtensionStyle.Full,
                    Dimension.DefaultTextSize);
            }

            using (var context = visual.RenderOpen())
            {
                renderer.Draw(
                    context,
                    layoutFar,
                    camera.Zoom,
                    CanvasTheme.Preview,
                    camera,
                    viewport,
                    "100.0",
                    DimensionExtensionStyle.Full,
                    Dimension.DefaultTextSize);
            }

            Assert.Equal(1, renderer.TextCache.CreateCount);
            Assert.Equal(1, renderer.TextCache.ReuseCount);
            Assert.Equal(pixelsPerDip, DimensionFormattedTextCache.ResolvePixelsPerDip(), 3);
        });
    }

    private static void RunSta(Action action) => WpfTestUtilities.RunSta(action);

    private static DimensionToolHarness CreateHarness()
    {
        var harness = new DimensionToolHarness();
        harness.Edge = TestDocumentHelpers.AddEdge(
            harness.Session.Document,
            new PointF(0, 0),
            new PointF(100, 0),
            Tol);
        return harness;
    }

    private static void EnterOffsetPhase(
        DimensionToolHarness harness,
        PointF? first = null,
        PointF? second = null)
    {
        harness.LeftClick(first ?? new PointF(0, 0));
        harness.LeftClick(second ?? new PointF(100, 0));
        Assert.True(harness.Tool.HasOffsetPhase);
    }

    private static string DocumentFingerprint(CadDocument document)
    {
        var vertices = document.Vertices
            .OrderBy(vertex => vertex.Id)
            .Select(vertex => $"{vertex.Id}:{vertex.Position.X},{vertex.Position.Y}");
        var edges = document.Edges
            .OrderBy(edge => edge.Id)
            .Select(edge => $"{edge.Id}:{edge.StartVertexId}-{edge.EndVertexId}");
        var dimensions = document.Dimensions
            .OrderBy(dimension => dimension.Id)
            .Select(dimension =>
                $"{dimension.Id}:{dimension.FirstVertexId}:{dimension.SecondVertexId}:{dimension.Offset}:{dimension.TextSize}:{dimension.ExtensionStyle}");

        return string.Join(
            "|",
            document.Vertices.Count,
            document.Edges.Count,
            document.Dimensions.Count,
            string.Join(";", vertices),
            string.Join(";", edges),
            string.Join(";", dimensions));
    }

    private static MouseButtonEventArgs CreateMouseButton(MouseButton button)
        => new(Mouse.PrimaryDevice, 0, button)
        {
            RoutedEvent = button == MouseButton.Left
                ? UIElement.MouseLeftButtonDownEvent
                : UIElement.MouseRightButtonDownEvent
        };

    private static MouseEventArgs CreateMouseMove()
        => new(Mouse.PrimaryDevice, 0)
        {
            RoutedEvent = UIElement.MouseMoveEvent
        };

    private sealed class DimensionToolHarness : IDisposable
    {
        public DimensionToolHarness()
        {
            Session = new CadSession();
            Tool = new DimensionTool();

            var context = new ToolContext(
                Session,
                () => new Size(800, 600),
                screen => Session.Camera.ScreenToWorld(screen, new Size(800, 600)),
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                () => { },
                setLineInputModeEnabled: (_, _) => { },
                recordUndo: () => Session.History.Record(Session.Document),
                activateSelectionTool: () => { });

            Session.ToolService.Initialize(context);
            Session.ToolService.ActivateTool(Tool);
        }

        public CadSession Session { get; }

        public DimensionTool Tool { get; }

        public Edge Edge { get; set; } = null!;

        public void LeftClick(PointF world)
            => Tool.OnMouseDown(CreateMouseButton(MouseButton.Left), world);

        public void MoveMouse(PointF world)
            => Tool.OnMouseMove(CreateMouseMove(), world);

        public void Dispose()
        {
        }
    }
}
