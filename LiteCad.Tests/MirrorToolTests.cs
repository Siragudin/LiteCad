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

public class MirrorToolTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void MirrorOperations_MirrorsPointAcrossDiagonalAxis()
    {
        var mirrored = MirrorOperations.MirrorPoint(
            new PointF(2, 0),
            new PointF(0, 0),
            new PointF(4, 4));

        Assert.True(MathUtils.ArePointsEqual(mirrored, new PointF(0, 2), Tol));
    }

    [Fact]
    public void MirrorOperations_PreservesOriginalObjects()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 2), new PointF(4, 2), Tol);
        var originalEdgeId = document.Edges[0].Id;
        var selection = new Selection();
        selection.SelectedEdgeIds.Add(originalEdgeId);

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        MirrorOperations.ExecuteObjectMirror(
            document,
            selection,
            snapshot,
            new PointF(0, 0),
            new PointF(4, 0));

        Assert.Equal(2, document.Edges.Count);
        var originalStart = TopologyService.GetEdgeStartPoint(
            document,
            document.Edges.Single(edge => edge.Id == originalEdgeId));
        Assert.True(MathUtils.ArePointsEqual(originalStart, new PointF(0, 2), Tol));

        var mirroredEdge = document.Edges.Single(edge => edge.Id != originalEdgeId);
        var mirroredStart = TopologyService.GetEdgeStartPoint(document, mirroredEdge);
        var mirroredEnd = TopologyService.GetEdgeEndPoint(document, mirroredEdge);
        Assert.True(MathUtils.ArePointsEqual(mirroredStart, new PointF(0, -2), Tol));
        Assert.True(MathUtils.ArePointsEqual(mirroredEnd, new PointF(4, -2), Tol));
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MirrorOperations_RejectsZeroLengthAxis()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 2), new PointF(4, 2), Tol);
        var selection = new Selection();
        selection.SelectedEdgeIds.Add(document.Edges[0].Id);

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        var result = MirrorOperations.ExecuteObjectMirror(
            document,
            selection,
            snapshot,
            new PointF(1, 1),
            new PointF(1, 1));

        Assert.Empty(result);
        var start = TopologyService.GetEdgeStartPoint(document, document.Edges[0]);
        Assert.True(MathUtils.ArePointsEqual(start, new PointF(0, 2), Tol));
    }

    [Fact]
    public void MirrorOperations_ReselectsMirroredPolygon()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(2, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(2, 0), new PointF(2, 2), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(2, 2), new PointF(0, 2), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 2), new PointF(0, 0), Tol);

        var polygon = new Polygon { Type = PolygonType.Wall };
        foreach (var edge in document.Edges)
        {
            polygon.OuterLoop.Edges.Add(new DirectedEdgeReference(edge.Id, forward: true));
        }

        document.Polygons.Add(polygon);

        var selection = new Selection();
        selection.SelectedPolygonIds.Add(polygon.Id);

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        MirrorOperations.ExecuteObjectMirror(
            document,
            selection,
            snapshot,
            new PointF(1, -1),
            new PointF(1, 3));

        Assert.Single(selection.SelectedPolygonIds);
        Assert.DoesNotContain(polygon.Id, selection.SelectedPolygonIds);
        Assert.Equal(4, selection.SelectedEdgeIds.Count);
        Assert.Equal(2, document.Polygons.Count(item => item.Type == PolygonType.Wall));
    }

    [Fact]
    public void MirrorTool_OrthoIgnoresGlobalLineOrtho()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithPartialSelection();
            SetPreciseMirrorZoom(harness);
            harness.Session.LineToolOptions.OrthoEnabled = true;
            harness.SetOrtho(false);
            harness.ToolService.ActivateTool(harness.MirrorTool);

            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.MirrorTool.OnMouseMove(CreateMouseEvent(), new PointF(3, 4));

            Assert.True(MathUtils.ArePointsEqual(harness.MirrorTool.PreviewAxisEnd, new PointF(3, 4), Tol));
        });
    }

    [Fact]
    public void MirrorTool_OrthoConstrainsAxisDirection()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithPartialSelection();
            SetPreciseMirrorZoom(harness);
            harness.SetOrtho(true);
            harness.ToolService.ActivateTool(harness.MirrorTool);

            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.MirrorTool.OnMouseMove(CreateMouseEvent(), new PointF(3, 4));

            Assert.True(MathUtils.ArePointsEqual(harness.MirrorTool.PreviewAxisEnd, new PointF(0, 4), Tol));
        });
    }

    [Fact]
    public void MirrorTool_OrthoOff_AllowsDiagonalAxis()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithPartialSelection();
            SetPreciseMirrorZoom(harness);
            harness.SetOrtho(false);
            harness.ToolService.ActivateTool(harness.MirrorTool);

            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.MirrorTool.OnMouseMove(CreateMouseEvent(), new PointF(3, 4));

            Assert.True(MathUtils.ArePointsEqual(harness.MirrorTool.PreviewAxisEnd, new PointF(3, 4), Tol));
        });
    }

    [Fact]
    public void MirrorTool_PreviewAxisTracksCursor()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithPartialSelection();
            SetPreciseMirrorZoom(harness);
            harness.ToolService.ActivateTool(harness.MirrorTool);

            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.MirrorTool.OnMouseMove(CreateMouseEvent(), new PointF(3, 4));

            Assert.True(MathUtils.ArePointsEqual(harness.MirrorTool.PreviewAxisEnd, new PointF(3, 4), Tol));
        });
    }

    [Fact]
    public void MirrorTool_MirrorsAcrossDiagonalAxis()
    {
        RunSta(() =>
        {
            var harness = new MirrorToolHarness();
            TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(0, 0), new PointF(2, 0), Tol);
            harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);
            SetPreciseMirrorZoom(harness);
            harness.ToolService.ActivateTool(harness.MirrorTool);

            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(4, 4));

            Assert.Equal(2, harness.Session.Document.Edges.Count);
            var originalEdgeId = harness.Session.Document.Edges[0].Id;
            var mirroredEdge = harness.Session.Document.Edges.Single(item => item.Id != originalEdgeId);
            var end = TopologyService.GetEdgeEndPoint(harness.Session.Document, mirroredEdge);
            Assert.True(MathUtils.ArePointsEqual(end, new PointF(0, 2), Tol));
            Assert.Contains(mirroredEdge.Id, harness.Session.Selection.SelectedEdgeIds);
            Assert.DoesNotContain(originalEdgeId, harness.Session.Selection.SelectedEdgeIds);
        });
    }

    [Fact]
    public void MirrorTool_EscapeCancelsPreviewWithoutClearingSelection()
    {
        RunSta(() =>
        {
            var window = new Window
            {
                Width = 100,
                Height = 100,
                Visibility = Visibility.Hidden,
                ShowInTaskbar = false
            };
            window.Show();

            try
            {
                var harness = CreateHarnessWithSquareSelection();
                SetPreciseMirrorZoom(harness);
                harness.ToolService.ActivateTool(harness.MirrorTool);
                var fingerprintBefore = DocumentFingerprint(harness.Session.Document);
                var selectedCount = harness.Session.Selection.SelectedEdgeIds.Count;

                harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
                Assert.True(harness.MirrorTool.HasAxisStart);

                harness.MirrorTool.OnKeyDown(CreateKeyEvent(window, Key.Escape));

                Assert.False(harness.MirrorTool.HasAxisStart);
                Assert.Equal(selectedCount, harness.Session.Selection.SelectedEdgeIds.Count);
                Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
                Assert.True(harness.Session.ToolService.ActiveTool is MirrorTool);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MirrorTool_ZeroLengthAxisRejected()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithPartialSelection();
            SetPreciseMirrorZoom(harness);
            harness.ToolService.ActivateTool(harness.MirrorTool);
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(1, 0));
            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(1, 0));

            Assert.False(harness.MirrorTool.HasAxisStart);
            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
            var restored = harness.Session.Document.Edges.Single();
            var restoredEnd = TopologyService.GetEdgeEndPoint(harness.Session.Document, restored);
            Assert.True(MathUtils.ArePointsEqual(restoredEnd, new PointF(2, 3), Tol));
        });
    }

    [Fact]
    public void MirrorTool_CommitReselectsMirroredEdges()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithPartialSelection();
            SetPreciseMirrorZoom(harness);
            harness.ToolService.ActivateTool(harness.MirrorTool);

            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(1, 0));
            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(1, 4));

            Assert.Equal(2, harness.Session.Document.Edges.Count);
            var originalEdgeId = harness.Session.Document.Edges[0].Id;
            var mirroredEdge = harness.Session.Document.Edges.Single(item => item.Id != originalEdgeId);
            Assert.Single(harness.Session.Selection.SelectedEdgeIds);
            Assert.Contains(mirroredEdge.Id, harness.Session.Selection.SelectedEdgeIds);
            Assert.DoesNotContain(originalEdgeId, harness.Session.Selection.SelectedEdgeIds);
        });
    }

    [Fact]
    public void MirrorOperations_MirrorsEdgeAcrossHorizontalAxis()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 2), new PointF(4, 2), Tol);
        var selection = new Selection();
        selection.SelectedEdgeIds.Add(document.Edges[0].Id);

        var originalEdgeId = document.Edges[0].Id;
        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        MirrorOperations.ExecuteObjectMirror(
            document,
            selection,
            snapshot,
            new PointF(0, 0),
            new PointF(4, 0));

        Assert.Equal(2, document.Edges.Count);
        var originalStart = TopologyService.GetEdgeStartPoint(
            document,
            document.Edges.Single(edge => edge.Id == originalEdgeId));
        Assert.True(MathUtils.ArePointsEqual(originalStart, new PointF(0, 2), Tol));

        var mirroredEdge = document.Edges.Single(edge => edge.Id != originalEdgeId);
        var start = TopologyService.GetEdgeStartPoint(document, mirroredEdge);
        var end = TopologyService.GetEdgeEndPoint(document, mirroredEdge);
        Assert.True(MathUtils.ArePointsEqual(start, new PointF(0, -2), Tol));
        Assert.True(MathUtils.ArePointsEqual(end, new PointF(4, -2), Tol));
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void MirrorOperations_InvertsPolygonLoopForwardOnRemap()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(2, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(2, 0), new PointF(2, 2), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(2, 2), new PointF(0, 2), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 2), new PointF(0, 0), Tol);

        var polygon = new Polygon { Type = PolygonType.Wall };
        foreach (var edge in document.Edges)
        {
            polygon.OuterLoop.Edges.Add(new DirectedEdgeReference(edge.Id, forward: true));
        }

        document.Polygons.Add(polygon);

        var selection = new Selection();
        selection.SelectedPolygonIds.Add(polygon.Id);

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        MirrorOperations.ExecuteObjectMirror(
            document,
            selection,
            snapshot,
            new PointF(1, -1),
            new PointF(1, 3));

        var remapped = document.Polygons.Single(item => item.Type == PolygonType.Wall && item.Id != polygon.Id);
        Assert.Equal(4, remapped.OuterLoop.Edges.Count);
        Assert.All(remapped.OuterLoop.Edges, reference => Assert.False(reference.Forward));
        Assert.Equal(2, document.Polygons.Count(item => item.Type == PolygonType.Wall));
    }

    [Fact]
    public void MirrorTool_PreviewDoesNotModifyDocument()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquareSelection();
            SetPreciseMirrorZoom(harness);
            harness.ToolService.ActivateTool(harness.MirrorTool);
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            harness.MirrorTool.OnMouseMove(CreateMouseEvent(), new PointF(0, 4));

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
            Assert.True(harness.MirrorTool.HasAxisStart);
        });
    }

    [Fact]
    public void MirrorTool_CommitMirrorsSelection()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithPartialSelection();
            SetPreciseMirrorZoom(harness);
            harness.ToolService.ActivateTool(harness.MirrorTool);

            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(1, 0));
            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(1, 4));

            Assert.Equal(2, harness.Session.Document.Edges.Count);
            var originalEdgeId = harness.Session.Document.Edges[0].Id;
            var mirroredEdge = harness.Session.Document.Edges.Single(item => item.Id != originalEdgeId);
            Assert.True(MathUtils.ArePointsEqual(
                TopologyService.GetEdgeStartPoint(harness.Session.Document, mirroredEdge),
                new PointF(2, 1),
                Tol));
            var end = TopologyService.GetEdgeEndPoint(harness.Session.Document, mirroredEdge);
            Assert.True(MathUtils.ArePointsEqual(end, new PointF(0, 3), Tol));
            Assert.False(harness.MirrorTool.HasAxisStart);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void MirrorTool_UndoRemovesOnlyMirroredCopy()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithPartialSelection();
            SetPreciseMirrorZoom(harness);
            harness.ToolService.ActivateTool(harness.MirrorTool);
            var originalEdgeId = harness.Session.Document.Edges[0].Id;

            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(1, 0));
            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(1, 4));

            Assert.Equal(2, harness.Session.Document.Edges.Count);

            Assert.True(harness.Session.History.Undo(harness.Session.Document, Tol));

            Assert.Single(harness.Session.Document.Edges);
            Assert.Equal(originalEdgeId, harness.Session.Document.Edges[0].Id);
            var restoredEnd = TopologyService.GetEdgeEndPoint(harness.Session.Document, harness.Session.Document.Edges[0]);
            Assert.True(MathUtils.ArePointsEqual(restoredEnd, new PointF(2, 3), Tol));
        });
    }

    [Fact]
    public void MirrorTool_UndoRedo()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithPartialSelection();
            SetPreciseMirrorZoom(harness);
            harness.ToolService.ActivateTool(harness.MirrorTool);

            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(1, 0));
            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(1, 4));

            Assert.True(harness.Session.History.Undo(harness.Session.Document, Tol));
            Assert.Single(harness.Session.Document.Edges);
            var restored = harness.Session.Document.Edges[0];
            var restoredStart = TopologyService.GetEdgeStartPoint(harness.Session.Document, restored);
            var restoredEnd = TopologyService.GetEdgeEndPoint(harness.Session.Document, restored);
            Assert.True(MathUtils.ArePointsEqual(restoredStart, new PointF(0, 1), Tol));
            Assert.True(MathUtils.ArePointsEqual(restoredEnd, new PointF(2, 3), Tol));

            Assert.True(harness.Session.History.Redo(harness.Session.Document, Tol));
            Assert.Equal(2, harness.Session.Document.Edges.Count);
            var mirroredEdge = harness.Session.Document.Edges.Single(item =>
                MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(harness.Session.Document, item), new PointF(2, 1), Tol));
            var redoneEnd = TopologyService.GetEdgeEndPoint(harness.Session.Document, mirroredEdge);
            Assert.True(MathUtils.ArePointsEqual(redoneEnd, new PointF(0, 3), Tol));
        });
    }

    [Fact]
    public void MirrorTool_RequiresSelection()
    {
        RunSta(() =>
        {
            var harness = new MirrorToolHarness();
            SetPreciseMirrorZoom(harness);
            harness.Session.Selection.Clear();
            harness.ToolService.ActivateTool(harness.MirrorTool);

            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));

            Assert.False(harness.MirrorTool.HasAxisStart);
        });
    }

    [Fact]
    public void MirrorTool_RightClickCancelsPreviewAndClearsSelection()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquareSelection();
            SetPreciseMirrorZoom(harness);
            harness.ToolService.ActivateTool(harness.MirrorTool);
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(0, 0));
            Assert.True(harness.MirrorTool.HasAxisStart);

            harness.MirrorTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Right), new PointF(0, 0));

            Assert.False(harness.MirrorTool.HasAxisStart);
            Assert.True(harness.Session.Selection.IsEmpty);
            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
            Assert.True(harness.Session.ToolService.ActiveTool is MirrorTool);
        });
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

    private static MirrorToolHarness CreateHarnessWithSquareSelection()
    {
        var harness = new MirrorToolHarness();
        AddSquare(harness.Session.Document, 0, 0, 4);
        PolygonBuilder.SyncFaces(harness.Session.Document, Tol);
        foreach (var edge in harness.Session.Document.Edges)
        {
            harness.Session.Selection.SelectedEdgeIds.Add(edge.Id);
        }

        return harness;
    }

    private static MirrorToolHarness CreateHarnessWithPartialSelection()
    {
        var harness = new MirrorToolHarness();
        TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(0, 1), new PointF(2, 3), Tol);
        harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);
        return harness;
    }

    private static void AddSquare(CadDocument document, float originX, float originY, float size)
    {
        TestDocumentHelpers.AddEdge(document, new PointF(originX, originY), new PointF(originX + size, originY), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(originX + size, originY), new PointF(originX + size, originY + size), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(originX + size, originY + size), new PointF(originX, originY + size), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(originX, originY + size), new PointF(originX, originY), Tol);
    }

    private static void SetPreciseMirrorZoom(MirrorToolHarness harness)
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

    private static KeyEventArgs CreateKeyEvent(Window window, Key key)
    {
        var source = PresentationSource.FromVisual(window)
            ?? throw new InvalidOperationException("Presentation source was not available.");
        return new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
        {
            RoutedEvent = UIElement.KeyDownEvent
        };
    }

    private sealed class MirrorToolHarness
    {
        public MirrorToolHarness()
        {
            Session = new CadSession();
            ToolService = Session.ToolService;
            MirrorTool = new MirrorTool();

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

        public MirrorTool MirrorTool { get; }

        public Size ViewportSize { get; } = new(800, 600);

        public bool RedrawRequested { get; private set; }

        public void SetOrtho(bool enabled)
            => Session.MirrorToolOptions.OrthoEnabled = enabled;
    }
}
