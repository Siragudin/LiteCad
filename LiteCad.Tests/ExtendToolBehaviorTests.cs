using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class ExtendToolBehaviorTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void RightClick_WithHoverPreview_CancelsPreview()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.MoveMouse(new PointF(95, 0));

            Assert.True(harness.Tool.HasHoverPreview);

            harness.RightClick();

            Assert.False(harness.Tool.HasHoverPreview);
            Assert.False(harness.Tool.HasExtendPreview);
        });
    }

    [Fact]
    public void RightClick_WithHoverPreview_KeepsExtendToolActive()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.MoveMouse(new PointF(95, 0));
            harness.RightClick();

            Assert.Same(harness.Tool, harness.Session.ToolService.ActiveTool);
            Assert.False(harness.SelectionActivated);
        });
    }

    [Fact]
    public void RightClick_WithHoverPreview_DoesNotMutateDocument()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.MoveMouse(new PointF(95, 0));
            harness.RightClick();

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
        });
    }

    [Fact]
    public void RightClick_WithHoverPreview_DoesNotCreateHistoryEntry()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.Session.History.Record(harness.Session.Document);
            var undoDepth = harness.Session.History.CanUndo;

            harness.MoveMouse(new PointF(95, 0));
            harness.RightClick();

            Assert.Equal(undoDepth, harness.Session.History.CanUndo);
        });
    }

    [Fact]
    public void RightClick_AfterCancel_CanStartNewExtendOperation()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            var source = harness.SourceEdge;

            harness.MoveMouse(new PointF(95, 0));
            harness.RightClick();
            harness.MoveMouse(new PointF(95, 0));
            harness.LeftClick(new PointF(95, 0));

            Assert.Equal(150, MathUtils.Distance(
                TopologyService.GetEdgeStartPoint(harness.Session.Document, source),
                TopologyService.GetEdgeEndPoint(harness.Session.Document, source)), 3);
            Assert.Same(harness.Tool, harness.Session.ToolService.ActiveTool);
        });
    }

    [Fact]
    public void RightClick_Idle_ClearsSelectionWithoutChangingTool()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.Session.Selection.SelectedEdgeIds.Add(harness.SourceEdge.Id);

            harness.RightClick();

            Assert.True(harness.Session.Selection.IsEmpty);
            Assert.Same(harness.Tool, harness.Session.ToolService.ActiveTool);
            Assert.False(harness.SelectionActivated);
        });
    }

    [Fact]
    public void Escape_ExitsToSelectionTool()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            var window = WpfTestUtilities.CreateHiddenWindow();
            harness.MoveMouse(new PointF(95, 0));

            harness.Tool.OnKeyDown(WpfTestUtilities.CreateKeyDown(window, Key.Escape));

            Assert.True(harness.SelectionActivated);
            window.Close();
        });
    }

    private static void RunSta(Action action) => WpfTestUtilities.RunSta(action);

    private static ExtendToolHarness CreateHarness()
    {
        var harness = new ExtendToolHarness();
        harness.SourceEdge = TestDocumentHelpers.AddEdge(
            harness.Session.Document,
            new PointF(0, 0),
            new PointF(100, 0),
            Tol);
        TestDocumentHelpers.AddEdge(
            harness.Session.Document,
            new PointF(150, -10),
            new PointF(150, 10),
            Tol);
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

        return string.Join(
            "|",
            document.Vertices.Count,
            document.Edges.Count,
            string.Join(";", vertices),
            string.Join(";", edges));
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

    private sealed class ExtendToolHarness : IDisposable
    {
        public ExtendToolHarness()
        {
            Session = new CadSession();
            Tool = new ExtendTool();

            var context = new ToolContext(
                Session,
                () => new Size(800, 600),
                screen => Session.Camera.ScreenToWorld(screen, new Size(800, 600)),
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                () => { },
                recordUndo: () => Session.History.Record(Session.Document),
                activateSelectionTool: () => SelectionActivated = true);

            Session.ToolService.Initialize(context);
            Session.ToolService.ActivateTool(Tool);
        }

        public CadSession Session { get; }

        public ExtendTool Tool { get; }

        public Edge SourceEdge { get; set; } = null!;

        public bool SelectionActivated { get; private set; }

        public void MoveMouse(PointF world)
            => Tool.OnMouseMove(CreateMouseMove(), world);

        public void LeftClick(PointF world)
            => Tool.OnMouseDown(CreateMouseButton(MouseButton.Left), world);

        public void RightClick()
            => Tool.OnMouseDown(CreateMouseButton(MouseButton.Right), PointF.Zero);

        public void Dispose()
        {
        }
    }
}
