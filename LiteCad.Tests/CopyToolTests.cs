using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class CopyToolTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void CopySingleEdge_CreatesIndependentCopy()
    {
        var document = CreateHorizontalChain();
        var original = document.Edges.First(edge =>
            MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(document, edge), new PointF(0, 0), Tol));
        var selection = new Selection();
        selection.SelectedEdgeIds.Add(original.Id);

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        var created = CopyOperations.ExecuteObjectCopy(document, selection, snapshot, new PointF(0, 5));

        Assert.Equal(3, document.Edges.Count);
        Assert.Contains(document.Edges, edge => edge.Id == original.Id);
        Assert.Equal(1, created.Count);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void CopyMultipleEdges_PreservesRelativeTopology()
    {
        var document = CreateHorizontalChain();
        var selection = new Selection();
        foreach (var edge in document.Edges)
        {
            selection.SelectedEdgeIds.Add(edge.Id);
        }

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        CopyOperations.ExecuteObjectCopy(document, selection, snapshot, new PointF(5, 0));

        Assert.Equal(4, document.Edges.Count);
        var copiedStarts = document.Edges
            .Where(edge => TopologyService.GetEdgeStartPoint(document, edge).X >= 5 - Tol)
            .Select(edge => TopologyService.GetEdgeStartPoint(document, edge))
            .OrderBy(point => point.X)
            .ToList();

        Assert.Equal(2, copiedStarts.Count);
        Assert.Equal(5, copiedStarts[0].X, 3);
        Assert.Equal(6, copiedStarts[1].X, 3);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void CopyDoesNotModifyOriginal()
    {
        var document = CreateUnitSquare();
        var selection = new Selection();
        var originalIds = new HashSet<Guid>();
        var originalGeometry = new Dictionary<Guid, (PointF Start, PointF End)>();
        foreach (var edge in document.Edges)
        {
            selection.SelectedEdgeIds.Add(edge.Id);
            originalIds.Add(edge.Id);
            originalGeometry[edge.Id] = (
                TopologyService.GetEdgeStartPoint(document, edge),
                TopologyService.GetEdgeEndPoint(document, edge));
        }

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        CopyOperations.ExecuteObjectCopy(document, selection, snapshot, new PointF(10, 0));

        Assert.Equal(8, document.Edges.Count);
        foreach (var originalId in originalIds)
        {
            var edge = document.Edges.First(item => item.Id == originalId);
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            Assert.Equal(originalGeometry[originalId].Start, start);
            Assert.Equal(originalGeometry[originalId].End, end);
        }

        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void CopyTool_ActivateWithSelection_IsActive_Sta()
    {
        RunSta(() =>
        {
            var session = new CadSession();
            CreateUnitSquare(session.Document);
            session.Selection.SelectedEdgeIds.Add(session.Document.Edges[0].Id);

            var copyTool = new CopyTool();
            session.ToolService.Initialize(CreateToolContext(session));
            session.ToolService.ActivateTool(copyTool);

            Assert.True(copyTool.IsActive);
        });
    }

    [Fact]
    public void CopyTool_ActivateWithSelection_IsActive()
    {
        var session = new CadSession();
        CreateUnitSquare(session.Document);
        session.Selection.SelectedEdgeIds.Add(session.Document.Edges[0].Id);

        var copyTool = new CopyTool();
        session.ToolService.Initialize(CreateToolContext(session));
        session.ToolService.ActivateTool(copyTool);

        Assert.True(copyTool.IsActive);
    }

    [Fact]
    public void CopyPreview_DoesNotModifyDocument()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquare();
            SetPreciseCopyZoom(harness);
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);
            harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);

            harness.ToolService.ActivateTool(harness.CopyTool);
            harness.CopyTool.OnMouseMove(CreateMouseEvent(), new PointF(100, 0));

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
            Assert.True(harness.CopyTool.IsActive);
        });
    }

    [Fact]
    public void CopyCommit_RequiresLeftClick()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquare();
            SetPreciseCopyZoom(harness);
            harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);
            harness.ToolService.ActivateTool(harness.CopyTool);

            harness.CopyTool.OnMouseMove(CreateMouseEvent(), new PointF(100, 0));
            Assert.Equal(4, harness.Session.Document.Edges.Count);

            harness.CopyTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(100, 0));

            Assert.Equal(5, harness.Session.Document.Edges.Count);
            Assert.False(harness.CopyTool.IsActive);
        });
    }

    [Fact]
    public void CopyRightClick_Cancels()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquare();
            SetPreciseCopyZoom(harness);
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);
            harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);
            harness.ToolService.ActivateTool(harness.CopyTool);

            harness.CopyTool.OnMouseMove(CreateMouseEvent(), new PointF(100, 0));
            harness.CopyTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Right), new PointF(0, 0));

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
            Assert.False(harness.CopyTool.IsActive);
            Assert.Single(harness.Session.Selection.SelectedEdgeIds);
        });
    }

    [Fact]
    public void CopyEscape_Cancels()
    {
        RunSta(() =>
        {
            if (Application.Current is null)
            {
                new Application();
            }

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
                var harness = CreateHarnessWithSquare();
                SetPreciseCopyZoom(harness);
                var fingerprintBefore = DocumentFingerprint(harness.Session.Document);
                harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);
                harness.ToolService.ActivateTool(harness.CopyTool);

                harness.CopyTool.OnMouseMove(CreateMouseEvent(), new PointF(100, 0));
                harness.CopyTool.OnKeyDown(CreateKeyEvent(window, Key.Escape));

                Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
                Assert.False(harness.CopyTool.IsActive);
                Assert.Single(harness.Session.Selection.SelectedEdgeIds);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CopyCreatesSingleUndoEntry()
    {
        RunSta(() =>
        {
            var harness = CreateHarnessWithSquare();
            SetPreciseCopyZoom(harness);
            harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);
            Assert.False(harness.Session.History.CanUndo);

            harness.ToolService.ActivateTool(harness.CopyTool);
            harness.CopyTool.OnMouseMove(CreateMouseEvent(), new PointF(100, 0));
            harness.CopyTool.OnMouseDown(CreateMouseButtonEvent(MouseButton.Left), new PointF(100, 0));

            Assert.True(harness.Session.History.CanUndo);
            Assert.True(harness.Session.History.Undo(harness.Session.Document, Tol));
            Assert.Equal(4, harness.Session.Document.Edges.Count);
        });
    }

    [Fact]
    public void CopyUndo_RemovesOnlyCopiedGeometry()
    {
        var session = new CadSession();
        var document = CreateUnitSquare(session.Document);
        var originalFingerprint = EdgeFingerprint(document);
        var selection = session.Selection;
        foreach (var edge in document.Edges)
        {
            selection.SelectedEdgeIds.Add(edge.Id);
        }

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        session.History.Record(document);
        CopyOperations.ExecuteObjectCopy(document, selection, snapshot, new PointF(10, 0));

        Assert.True(session.History.Undo(document, Tol));
        Assert.Equal(originalFingerprint, EdgeFingerprint(document));
        Assert.Equal(4, document.Edges.Count);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void CopyRedo_RestoresCopiedGeometry()
    {
        var session = new CadSession();
        var document = CreateUnitSquare(session.Document);
        var selection = session.Selection;
        foreach (var edge in document.Edges)
        {
            selection.SelectedEdgeIds.Add(edge.Id);
        }

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        session.History.Record(document);
        CopyOperations.ExecuteObjectCopy(document, selection, snapshot, new PointF(10, 0));
        var fingerprintCopied = EdgeFingerprint(document);

        Assert.True(session.History.Undo(document, Tol));
        Assert.True(session.History.Redo(document, Tol));
        Assert.Equal(fingerprintCopied, EdgeFingerprint(document));
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void CopyTwoRooms_PreservesTwoFaces()
    {
        var document = CreateTwoRooms();
        var originalFaceCount = document.Polygons.Count;
        var selection = new Selection();
        foreach (var face in document.Polygons)
        {
            selection.SelectedPolygonIds.Add(face.Id);
        }

        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        CopyOperations.ExecuteObjectCopy(document, selection, snapshot, new PointF(20, 0));

        Assert.Equal(2, originalFaceCount);
        Assert.Equal(4, document.Polygons.Count);
        Assert.All(document.Polygons, face => Assert.Equal(16.0, PolygonGeometry.GetArea(document, face, Tol), 3));
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void CopyPreservesStyles()
    {
        var document = new CadDocument();
        var template = TestDocumentHelpers.CreateTemplate();
        template.Color = Colors.Red;
        template.Thickness = 3.5;
        template.LineType = EdgeLineType.Dashed;
        TopologyService.CreateEdgeFromPoints(document, new PointF(0, 0), new PointF(4, 0), template, Tol);
        PolygonBuilder.SyncFaces(document, Tol);

        var original = document.Edges[0];
        var selection = new Selection();
        selection.SelectedEdgeIds.Add(original.Id);
        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        CopyOperations.ExecuteObjectCopy(document, selection, snapshot, new PointF(0, 5));

        var copied = document.Edges.Single(edge => edge.Id != original.Id);
        Assert.Equal(Colors.Red, copied.Color);
        Assert.Equal(3.5, copied.Thickness, 3);
        Assert.Equal(EdgeLineType.Dashed, copied.LineType);
    }

    [Fact]
    public void CopyDoesNotShareVertexReferencesWithOriginal()
    {
        var document = CreateUnitSquare();
        var selection = new Selection();
        foreach (var edge in document.Edges)
        {
            selection.SelectedEdgeIds.Add(edge.Id);
        }

        var originalVertexIds = document.Vertices.Select(vertex => vertex.Id).ToHashSet();
        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        CopyOperations.ExecuteObjectCopy(document, selection, snapshot, new PointF(10, 0));

        var copiedEdges = document.Edges
            .Where(edge => !snapshot.Edges.Any(entry => entry.OriginalEdgeId == edge.Id))
            .ToList();

        Assert.Equal(4, copiedEdges.Count);
        foreach (var edge in copiedEdges)
        {
            Assert.DoesNotContain(edge.StartVertexId, originalVertexIds);
            Assert.DoesNotContain(edge.EndVertexId, originalVertexIds);
        }
    }

    [Fact]
    public void CopyDoesNotShareEdgeReferencesWithOriginal()
    {
        var document = CreateUnitSquare();
        var selection = new Selection();
        foreach (var edge in document.Edges)
        {
            selection.SelectedEdgeIds.Add(edge.Id);
        }

        var originalEdgeIds = document.Edges.Select(edge => edge.Id).ToHashSet();
        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        var created = CopyOperations.ExecuteObjectCopy(document, selection, snapshot, new PointF(10, 0));

        Assert.Equal(4, created.Count);
        Assert.All(created, id => Assert.DoesNotContain(id, originalEdgeIds));
    }

    private static CadDocument CreateUnitSquare(CadDocument? document = null, float size = 4)
    {
        document ??= new CadDocument();
        AddSquare(document, 0, 0, size);
        PolygonBuilder.SyncFaces(document, Tol);
        return document;
    }

    private static CadDocument CreateHorizontalChain()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(1, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(1, 0), new PointF(2, 0), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        return document;
    }

    private static CadDocument CreateTwoRooms()
    {
        var document = new CadDocument();
        AddSquare(document, 0, 0, 4);
        AddSquare(document, 10, 0, 4);
        PolygonBuilder.SyncFaces(document, Tol);
        return document;
    }

    private static void AddSquare(CadDocument document, float x, float y, float size)
    {
        TestDocumentHelpers.AddEdge(document, new PointF(x, y), new PointF(x + size, y), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(x + size, y), new PointF(x + size, y + size), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(x + size, y + size), new PointF(x, y + size), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(x, y + size), new PointF(x, y), Tol);
    }

    private static string EdgeFingerprint(CadDocument document)
        => string.Join(
            ";",
            document.Edges
                .OrderBy(edge => edge.Id)
                .Select(edge =>
                {
                    var start = TopologyService.GetEdgeStartPoint(document, edge);
                    var end = TopologyService.GetEdgeEndPoint(document, edge);
                    return $"{start.X},{start.Y}->{end.X},{end.Y}";
                }));

    private static string DocumentFingerprint(CadDocument document)
    {
        var vertices = document.Vertices
            .OrderBy(vertex => vertex.Id)
            .Select(vertex => $"{vertex.Id}:{vertex.Position.X},{vertex.Position.Y}");
        var edges = document.Edges
            .OrderBy(edge => edge.Id)
            .Select(edge => $"{edge.Id}:{edge.StartVertexId}-{edge.EndVertexId}");
        return string.Join("|", document.Vertices.Count, document.Edges.Count, string.Join(";", vertices), string.Join(";", edges));
    }

    private static void SetPreciseCopyZoom(CopyHarness harness)
        => harness.Session.Camera.ZoomAt(new Point(400, 300), 100, harness.ViewportSize);

    private static CopyHarness CreateHarnessWithSquare()
    {
        var harness = new CopyHarness();
        CreateUnitSquare(harness.Session.Document);
        return harness;
    }

    private static void RunSta(Action action) => WpfTestUtilities.RunSta(action);

    private static MouseButtonEventArgs CreateMouseButtonEvent(MouseButton button)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, button);
        args.RoutedEvent = button switch
        {
            MouseButton.Left => UIElement.MouseLeftButtonDownEvent,
            MouseButton.Right => UIElement.MouseRightButtonDownEvent,
            _ => UIElement.MouseDownEvent
        };
        return args;
    }

    private static MouseEventArgs CreateMouseEvent()
        => new(Mouse.PrimaryDevice, 0)
        {
            RoutedEvent = UIElement.MouseMoveEvent
        };

    private static KeyEventArgs CreateKeyEvent(Window window, Key key)
    {
        var source = PresentationSource.FromVisual(window)
            ?? throw new InvalidOperationException("Presentation source was not available.");
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
        {
            RoutedEvent = UIElement.KeyDownEvent
        };
        return args;
    }

    private static ToolContext CreateToolContext(CadSession session)
        => new(
            session,
            () => new Size(800, 600),
            _ => PointF.Zero,
            _ => new Point(0, 0),
            () => { },
            () => { },
            () => { },
            recordUndo: () => session.History.Record(session.Document));

    private sealed class CopyHarness
    {
        public CopyHarness()
        {
            Session = new CadSession();
            ToolService = Session.ToolService;
            CopyTool = new CopyTool();
            ToolService.Initialize(CreateToolContext(Session));
        }

        public CadSession Session { get; }

        public ToolService ToolService { get; }

        public CopyTool CopyTool { get; }

        public Size ViewportSize { get; } = new(800, 600);

        public bool RedrawRequested { get; private set; }
    }
}
