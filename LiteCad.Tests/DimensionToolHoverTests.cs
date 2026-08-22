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

public class DimensionToolHoverTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void Hover_DoesNotChangeVertexCount()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            var vertexCount = harness.Session.Document.Vertices.Count;

            harness.MoveMouse(new PointF(0, 0));
            harness.MoveMouse(new PointF(50, 0));
            harness.MoveMouse(new PointF(100, 0));

            Assert.Equal(vertexCount, harness.Session.Document.Vertices.Count);
        });
    }

    [Fact]
    public void Hover_DoesNotChangeEdgeCount()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            var edgeCount = harness.Session.Document.Edges.Count;

            harness.MoveMouse(new PointF(0, 0));
            harness.MoveMouse(new PointF(50, 0));
            harness.MoveMouse(new PointF(100, 0));

            Assert.Equal(edgeCount, harness.Session.Document.Edges.Count);
        });
    }

    [Fact]
    public void Hover_DoesNotChangeDimensionCount()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            DimensionService.Create(
                harness.Session.Document,
                harness.Edge.StartVertexId,
                harness.Edge.EndVertexId,
                25,
                Tol);

            harness.MoveMouse(new PointF(0, 0));
            harness.MoveMouse(new PointF(50, 0));
            harness.MoveMouse(new PointF(100, 0));

            Assert.Single(harness.Session.Document.Dimensions);
        });
    }

    [Fact]
    public void Hover_DoesNotCreateUndoEntry()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();
            harness.Session.History.Record(harness.Session.Document);
            var undoDepth = harness.Session.History.CanUndo;

            harness.MoveMouse(new PointF(0, 0));
            harness.MoveMouse(new PointF(50, 0));
            harness.MoveMouse(new PointF(100, 0));

            Assert.Equal(undoDepth, harness.Session.History.CanUndo);
        });
    }

    [Fact]
    public void Hover_FindsEndpointSnap()
    {
        RunSta(() =>
        {
            using var harness = CreateHarness();

            harness.MoveMouse(new PointF(0, 0));

            Assert.NotNull(harness.Tool.HoverMeasurementSnap);
            Assert.Equal(SnapKind.Endpoint, harness.Tool.HoverMeasurementSnap!.Value.Kind);
            Assert.True(MathUtils.ArePointsEqual(new PointF(0, 0), harness.Tool.HoverMeasurementSnap.Value.Position, Tol));
        });
    }

    [Fact]
    public void Hover_AtLineIntersection_DoesNotCreateVertex()
    {
        RunSta(() =>
        {
            using var harness = CreateLineIntersectionHarness();
            var vertexCountBefore = harness.Session.Document.Vertices.Count;

            harness.MoveMouse(new PointF(50, 50));

            Assert.Equal(vertexCountBefore, harness.Session.Document.Vertices.Count);
            Assert.NotNull(harness.Tool.HoverMeasurementSnap);
            Assert.Equal(SnapKind.Intersection, harness.Tool.HoverMeasurementSnap!.Value.Kind);
        });
    }

    [Fact]
    public void Commit_AtLineIntersection_CreatesRequiredVertex()
    {
        RunSta(() =>
        {
            using var harness = CreateLineIntersectionHarness();
            var vertexCountBefore = harness.Session.Document.Vertices.Count;

            harness.MoveMouse(new PointF(50, 50));
            Assert.Equal(vertexCountBefore, harness.Session.Document.Vertices.Count);

            harness.LeftClick(new PointF(50, 50));

            Assert.True(harness.Session.Document.Vertices.Count > vertexCountBefore);
            Assert.True(harness.Tool.HasPendingOperation);
        });
    }

    [Fact]
    public void Hover_OnLargeDocument_DoesNotMutateDocument()
    {
        RunSta(() =>
        {
            using var harness = CreateLargeDocumentHarness(40);
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            for (var index = 0; index < 20; index++)
            {
                harness.MoveMouse(new PointF(index * 5, 0));
                harness.MoveMouse(new PointF(index * 5, 50));
            }

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
        });
    }

    [Fact]
    public void LineTool_MouseMove_StillDoesNotMutateDocument()
    {
        RunSta(() =>
        {
            var harness = new LineToolHarness();
            TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(0, 0), new PointF(100, 0), Tol);
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.Tool.OnMouseMove(CreateMouseMove(), new PointF(50, 0));
            harness.Tool.OnMouseMove(CreateMouseMove(), new PointF(75, 0));

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
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

    private static DimensionToolHarness CreateLineIntersectionHarness()
    {
        var harness = new DimensionToolHarness();
        TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(0, 50), new PointF(100, 50), Tol);
        TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(50, 0), new PointF(50, 100), Tol);
        return harness;
    }

    private static DimensionToolHarness CreateLargeDocumentHarness(int edgeCount)
    {
        var harness = new DimensionToolHarness();
        Edge? lastEdge = null;
        for (var index = 0; index < edgeCount; index++)
        {
            var start = new PointF(index * 10, 0);
            var end = new PointF(index * 10 + 10, 0);
            lastEdge = TestDocumentHelpers.AddEdge(harness.Session.Document, start, end, Tol);
        }

        harness.Edge = lastEdge!;
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

    private sealed class LineToolHarness
    {
        public LineToolHarness()
        {
            Session = new CadSession();
            Tool = new LineTool();

            var context = new ToolContext(
                Session,
                () => new Size(800, 600),
                _ => PointF.Zero,
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                recordUndo: () => Session.History.Record(Session.Document));

            Session.ToolService.Initialize(context);
            Session.ToolService.ActivateTool(Tool);
        }

        public CadSession Session { get; }

        public LineTool Tool { get; }
    }
}
