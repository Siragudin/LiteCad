using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class DimensionToolBehaviorTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void RightClick_AfterFirstPoint_CancelsPendingOperation()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.LeftClick(new PointF(0, 0));

            Assert.True(harness.Tool.HasPendingOperation);

            harness.RightClick();

            Assert.False(harness.Tool.HasPendingOperation);
            Assert.False(harness.Tool.HasOffsetPhase);
        });
    }

    [Fact]
    public void RightClick_AfterFirstPoint_KeepsDimensionToolActive()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.LeftClick(new PointF(0, 0));
            harness.RightClick();

            Assert.Same(harness.Tool, harness.Session.ToolService.ActiveTool);
            Assert.False(harness.SelectionActivated);
        });
    }

    [Fact]
    public void RightClick_AfterFirstPoint_DoesNotMutateDocument()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.LeftClick(new PointF(0, 0));
            harness.RightClick();

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
            Assert.Empty(harness.Session.Document.Dimensions);
        });
    }

    [Fact]
    public void RightClick_AfterFirstPoint_DoesNotCreateHistoryEntry()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.Session.History.Record(harness.Session.Document);
            var undoDepth = harness.Session.History.CanUndo;

            harness.LeftClick(new PointF(0, 0));
            harness.RightClick();

            Assert.Equal(undoDepth, harness.Session.History.CanUndo);
        });
    }

    [Fact]
    public void RightClick_AfterFirstPoint_PreservesToolOptions()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.Session.DimensionToolOptions.TextSize = 42;
            harness.Session.DimensionToolOptions.ExtensionStyle = DimensionExtensionStyle.Short;
            harness.Session.DimensionToolOptions.OrthoEnabled = true;

            harness.LeftClick(new PointF(0, 0));
            harness.RightClick();

            Assert.Equal(42, harness.Session.DimensionToolOptions.TextSize);
            Assert.Equal(DimensionExtensionStyle.Short, harness.Session.DimensionToolOptions.ExtensionStyle);
            Assert.True(harness.Session.DimensionToolOptions.OrthoEnabled);
        });
    }

    [Fact]
    public void RightClick_AfterCancel_CanCreateNewDimension()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.LeftClick(new PointF(0, 0));
            harness.RightClick();

            harness.LeftClick(new PointF(0, 0));
            harness.LeftClick(new PointF(100, 0));
            harness.Tool.TryApplyLength(50);

            Assert.Single(harness.Session.Document.Dimensions);
            Assert.Same(harness.Tool, harness.Session.ToolService.ActiveTool);
        });
    }

    [Fact]
    public void RightClick_InOffsetPhase_CancelsPendingOperation()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.LeftClick(new PointF(0, 0));
            harness.LeftClick(new PointF(100, 0));

            Assert.True(harness.Tool.HasOffsetPhase);

            harness.RightClick();

            Assert.False(harness.Tool.HasPendingOperation);
            Assert.False(harness.Tool.HasOffsetPhase);
            Assert.False(harness.LineInputEnabled);
            Assert.Same(harness.Tool, harness.Session.ToolService.ActiveTool);
        });
    }

    [Fact]
    public void RightClick_Idle_ClearsSelectionWithoutChangingTool()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.Session.Selection.SelectedEdgeIds.Add(harness.Edge.Id);

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
            harness.LeftClick(new PointF(0, 0));

            harness.Tool.OnKeyDown(WpfTestUtilities.CreateKeyDown(window, Key.Escape));

            Assert.True(harness.SelectionActivated);
            window.Close();
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
            .Select(dimension => dimension.Id.ToString());

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
                setLineInputModeEnabled: (enabled, _) => LineInputEnabled = enabled,
                recordUndo: () => Session.History.Record(Session.Document),
                activateSelectionTool: () => SelectionActivated = true);

            Session.ToolService.Initialize(context);
            Session.ToolService.ActivateTool(Tool);
        }

        public CadSession Session { get; }

        public DimensionTool Tool { get; }

        public Edge Edge { get; set; } = null!;

        public bool SelectionActivated { get; private set; }

        public bool LineInputEnabled { get; private set; }

        public void LeftClick(PointF world)
            => Tool.OnMouseDown(CreateMouseButton(MouseButton.Left), world);

        public void RightClick()
            => Tool.OnMouseDown(CreateMouseButton(MouseButton.Right), PointF.Zero);

        public void Dispose()
        {
        }
    }
}
