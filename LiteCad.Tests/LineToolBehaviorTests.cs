using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class LineToolBehaviorTests
{
    private const double Tolerance = 1e-4;

    [Fact]
    public void RightClick_BeforeFirstClick_ClearsSelection()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);

            harness.Tool.OnMouseDown(CreateMouseButton(MouseButton.Right), new PointF(10, 10));

            Assert.True(harness.Session.Selection.IsEmpty);
            Assert.Same(harness.Tool, harness.Session.ToolService.ActiveTool);
            Assert.Equal(1, harness.Session.Document.Edges.Count);
        });
    }

    [Fact]
    public void RightClick_AfterFirstClick_KeepsLineToolActive()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();

            harness.Tool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(0, 0));
            harness.Tool.OnMouseDown(CreateMouseButton(MouseButton.Right), new PointF(50, 0));

            Assert.Same(harness.Tool, harness.Session.ToolService.ActiveTool);
        });
    }

    [Fact]
    public void RightClick_AfterFirstClick_CancelsPreviewWithoutCreatingLine()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.Tool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(0, 0));
            harness.Tool.OnMouseMove(CreateMouseMove(), new PointF(50, 0));
            harness.Tool.OnMouseDown(CreateMouseButton(MouseButton.Right), new PointF(50, 0));

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
            Assert.Equal(1, harness.Session.Document.Edges.Count);
            Assert.True(harness.Session.Selection.IsEmpty);
        });
    }

    [Fact]
    public void RightClick_AfterFirstClick_DoesNotCreateHistoryEntry()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.Session.History.Record(harness.Session.Document);
            var undoDepth = harness.Session.History.CanUndo;

            harness.Tool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(0, 0));
            harness.Tool.OnMouseDown(CreateMouseButton(MouseButton.Right), new PointF(50, 0));

            Assert.Equal(undoDepth, harness.Session.History.CanUndo);
        });
    }

    [Fact]
    public void RightClick_DuringPreview_ClearsExistingSelection()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);

            harness.Tool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(0, 0));
            harness.Tool.OnMouseDown(CreateMouseButton(MouseButton.Right), new PointF(50, 0));

            Assert.True(harness.Session.Selection.IsEmpty);
        });
    }

    [Fact]
    public void Escape_AfterFirstClick_CancelsPreviewWithoutCreatingLine()
    {
        RunSta(() =>
        {
            var window = WpfTestUtilities.CreateHiddenWindow();
            var harness = CreateHarness();
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.Tool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(0, 0));
            harness.Tool.OnKeyDown(WpfTestUtilities.CreateKeyDown(window, Key.Escape));

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
            Assert.Equal(1, harness.Session.Document.Edges.Count);

            window.Close();
        });
    }

    private static void RunSta(Action action) => WpfTestUtilities.RunSta(action);

    private static LineToolHarness CreateHarness()
    {
        var harness = new LineToolHarness();
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

    private sealed class LineToolHarness
    {
        public LineToolHarness()
        {
            Session = new CadSession();
            Tool = new LineTool();

            var context = new ToolContext(
                Session,
                () => new Size(800, 600),
                screen => Session.Camera.ScreenToWorld(screen, new Size(800, 600)),
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                setLengthInputEnabled: _ => { },
                resetLengthInput: _ => { },
                processLengthKey: _ => false,
                setLineInputModeEnabled: (_, _) => { },
                recordUndo: () => Session.History.Record(Session.Document));

            Session.ToolService.Initialize(context);
            Session.ToolService.ActivateTool(Tool);
        }

        public CadSession Session { get; }

        public LineTool Tool { get; }
    }
}
