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

public class OffsetToolTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void OffsetTool_PreviewDoesNotModifyDocument()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquareFace();
            SetPreciseOffsetZoom(harness);
            harness.ToolService.ActivateTool(harness.OffsetTool);
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.OffsetTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(2, 2));
            harness.OffsetTool.OnMouseMove(CreateMouseEvent(), new PointF(2, -1));

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
            Assert.True(harness.OffsetTool.HasActiveOffset);
            Assert.True(harness.OffsetTool.PreviewSignedDistance > 0);
        });
    }

    [Fact]
    public void OffsetTool_CommitCreatesNewFaceAndPreservesOriginal()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquareFace();
            SetPreciseOffsetZoom(harness);
            var originalFace = harness.Session.Document.Polygons.Single();
            var originalEdgeIds = originalFace.OuterLoop.Edges
                .Select(reference => reference.EdgeId)
                .ToHashSet();
            var originalEdgeCount = harness.Session.Document.Edges.Count;
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(2, 2));
            harness.OffsetTool.OnMouseMove(CreateMouseEvent(), new PointF(2, -1));
            harness.OffsetTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(2, -1));

            Assert.False(harness.OffsetTool.HasActiveOffset);
            Assert.Equal(2, harness.Session.Document.Polygons.Count(polygon => polygon.Type == PolygonType.Face));
            Assert.Equal(originalEdgeCount + 4, harness.Session.Document.Edges.Count);
            Assert.All(
                originalEdgeIds,
                edgeId => Assert.Contains(edgeId, harness.Session.Document.Edges.Select(edge => edge.Id)));

            var selectedFace = harness.Session.Document.Polygons.Single(
                polygon => polygon.Id == harness.Session.Selection.SelectedPolygonIds.Single());
            Assert.All(
                selectedFace.OuterLoop.Edges,
                reference => Assert.DoesNotContain(reference.EdgeId, originalEdgeIds));
            Assert.Equal(36, PolygonGeometry.GetArea(harness.Session.Document, selectedFace, Tol), 2);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void OffsetTool_CursorInsideUsesInwardOffset()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquareFace();
            SetPreciseOffsetZoom(harness);
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(2, 2));
            harness.OffsetTool.OnMouseMove(CreateMouseEvent(), new PointF(2, 0.5));

            Assert.True(harness.OffsetTool.PreviewSignedDistance < 0);
            Assert.Equal(OffsetValidationResult.Valid, harness.OffsetTool.PreviewValidation);
        });
    }

    [Fact]
    public void OffsetTool_UndoRemovesOnlyOffsetContour()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquareFace();
            SetPreciseOffsetZoom(harness);
            var originalEdgeCount = harness.Session.Document.Edges.Count;
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(2, 2));
            harness.OffsetTool.OnMouseMove(CreateMouseEvent(), new PointF(2, -1));
            harness.OffsetTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(2, -1));

            Assert.True(harness.Session.History.Undo(harness.Session.Document, Tol));
            Assert.Single(harness.Session.Document.Polygons);
            Assert.Equal(originalEdgeCount, harness.Session.Document.Edges.Count);
        });
    }

    [Fact]
    public void OffsetTool_EscapeCancelsWithoutClearingSelection()
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
                var harness = CreateHarnessWithSquareFace();
                SetPreciseOffsetZoom(harness);
                harness.ToolService.ActivateTool(harness.OffsetTool);

                harness.OffsetTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(2, 2));
                Assert.True(harness.OffsetTool.HasActiveOffset);
                Assert.Single(harness.Session.Selection.SelectedPolygonIds);

                harness.OffsetTool.OnKeyDown(CreateKeyEvent(window, Key.Escape));

                Assert.False(harness.OffsetTool.HasActiveOffset);
                Assert.Single(harness.Session.Selection.SelectedPolygonIds);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void OffsetTool_RightClickCancelsAndClearsSelection()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquareFace();
            SetPreciseOffsetZoom(harness);
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(2, 2));
            harness.OffsetTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Right), new PointF(2, 2));

            Assert.False(harness.OffsetTool.HasActiveOffset);
            Assert.True(harness.Session.Selection.IsEmpty);
        });
    }

    [Fact]
    public void OffsetTool_NumericInputCommitsOffset()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquareFace();
            SetPreciseOffsetZoom(harness);
            harness.ToolService.ActivateTool(harness.OffsetTool);

            harness.OffsetTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(2, 2));
            harness.OffsetTool.OnMouseMove(CreateMouseEvent(), new PointF(2, -1));
            Assert.True(harness.OffsetTool.TryApplyLengthInput("1"));

            Assert.Equal(2, harness.Session.Document.Polygons.Count(polygon => polygon.Type == PolygonType.Face));
            Assert.False(harness.OffsetTool.HasActiveOffset);
        });
    }

    private static string DocumentFingerprint(CadDocument document)
        => string.Join("|", document.Edges.Count, document.Polygons.Count);

    private static OffsetToolHarness CreateHarnessWithSquareFace()
    {
        var harness = new OffsetToolHarness();
        TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(0, 0), new PointF(4, 0), Tol);
        TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(4, 0), new PointF(4, 4), Tol);
        TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(4, 4), new PointF(0, 4), Tol);
        TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(0, 4), new PointF(0, 0), Tol);
        PolygonBuilder.SyncFaces(harness.Session.Document, Tol);
        return harness;
    }

    private static void SetPreciseOffsetZoom(OffsetToolHarness harness)
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
        => new(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseMoveEvent };

    private static KeyEventArgs CreateKeyEvent(Window window, Key key)
    {
        var source = PresentationSource.FromVisual(window)
            ?? throw new InvalidOperationException("Presentation source was not available.");
        return new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
        {
            RoutedEvent = UIElement.KeyDownEvent
        };
    }

    private sealed class OffsetToolHarness
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
                () => RedrawRequested = true,
                () => { },
                () => { },
                () => { },
                recordUndo: () => Session.History.Record(Session.Document));

            ToolService.Initialize(context);
        }

        public CadSession Session { get; }

        public ToolService ToolService { get; }

        public OffsetTool OffsetTool { get; }

        public Size ViewportSize { get; } = new(800, 600);

        public bool RedrawRequested { get; private set; }
    }
}
