using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using LiteCad.UI.Layout;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class StretchToolTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void TryCreatePlan_RectangleBottomEdge_Succeeds()
    {
        var document = CreateRectangle(0, 0, 4, 4);
        var bottom = FindHorizontalEdge(document, y: 0, minX: 0, maxX: 4);

        Assert.True(StretchOperations.TryCreatePlan(document, bottom.Id, Tol, out var plan));
        Assert.Equal(bottom.Id, plan.EdgeId);
    }

    [Fact]
    public void TryCreatePlan_IsolatedEdge_Fails()
    {
        var document = new CadDocument();
        var edge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(5, 0), Tol);

        Assert.False(StretchOperations.TryCreatePlan(document, edge.Id, Tol, out _));
    }

    [Fact]
    public void TryCreatePlan_LShapeCorner_Fails()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(4, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(4, 0), new PointF(4, 4), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(4, 4), new PointF(0, 4), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 4), new PointF(0, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(0, 2), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 2), new PointF(2, 2), Tol);

        var vertical = document.Edges.First(edge =>
            MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(document, edge), new PointF(0, 0), Tol)
            && MathUtils.ArePointsEqual(TopologyService.GetEdgeEndPoint(document, edge), new PointF(0, 2), Tol));

        Assert.False(StretchOperations.TryCreatePlan(document, vertical.Id, Tol, out _));
    }

    [Fact]
    public void ApplyStretch_MovesSelectedEdgePerpendicular()
    {
        var document = CreateRectangle(0, 0, 4, 4);
        var bottom = FindHorizontalEdge(document, y: 0, minX: 0, maxX: 4);
        Assert.True(StretchOperations.TryCreatePlan(document, bottom.Id, Tol, out var plan));

        var fixedCorner = document.Vertices.First(vertex =>
            MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 4), Tol));

        StretchOperations.ApplyStretch(document, plan, new PointF(0, 1));

        Assert.Equal(1, TopologyService.GetEdgeStartPoint(document, bottom).Y, 3);
        Assert.Equal(4, TopologyService.GetVertexPosition(document, fixedCorner.Id).Y, 3);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void ApplyStretch_PreservesOuterVertices()
    {
        var document = CreateRectangle(0, 0, 4, 4);
        var bottom = FindHorizontalEdge(document, y: 0, minX: 0, maxX: 4);
        Assert.True(StretchOperations.TryCreatePlan(document, bottom.Id, Tol, out var plan));

        var topLeftId = document.Vertices.First(vertex =>
            MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 4), Tol)).Id;
        var topRightId = document.Vertices.First(vertex =>
            MathUtils.ArePointsEqual(vertex.Position, new PointF(4, 4), Tol)).Id;
        var topLeftBefore = TopologyService.GetVertexPosition(document, topLeftId);
        var topRightBefore = TopologyService.GetVertexPosition(document, topRightId);

        StretchOperations.ApplyStretch(document, plan, new PointF(0, -1));

        Assert.Equal(topLeftBefore, TopologyService.GetVertexPosition(document, topLeftId));
        Assert.Equal(topRightBefore, TopologyService.GetVertexPosition(document, topRightId));
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void ApplyStretch_RebuildsFace()
    {
        var document = CreateRectangle(0, 0, 4, 4);
        var bottom = FindHorizontalEdge(document, y: 0, minX: 0, maxX: 4);
        Assert.True(StretchOperations.TryCreatePlan(document, bottom.Id, Tol, out var plan));

        StretchOperations.ApplyStretch(document, plan, new PointF(0, -1));

        Assert.Single(document.Polygons);
        Assert.Equal(20.0, PolygonGeometry.GetArea(document, document.Polygons[0], Tol), 3);
    }

    [Fact]
    public void ApplyStretch_ProjectsDeltaOntoNormal()
    {
        var document = CreateRectangle(0, 0, 4, 4);
        var bottom = FindHorizontalEdge(document, y: 0, minX: 0, maxX: 4);
        Assert.True(StretchOperations.TryCreatePlan(document, bottom.Id, Tol, out var plan));

        StretchOperations.ApplyStretch(document, plan, new PointF(5, 1));

        Assert.Equal(1, TopologyService.GetEdgeStartPoint(document, bottom).Y, 3);
        Assert.Equal(0, TopologyService.GetEdgeStartPoint(document, bottom).X, 3);
    }

    [Fact]
    public void ApplyStretch_UndoRedo()
    {
        var session = new CadSession();
        var document = CreateRectangle(session.Document, 0, 0, 4, 4);
        var bottom = FindHorizontalEdge(document, y: 0, minX: 0, maxX: 4);
        Assert.True(StretchOperations.TryCreatePlan(document, bottom.Id, Tol, out var plan));
        var yBefore = TopologyService.GetEdgeStartPoint(document, bottom).Y;

        session.History.Record(document);
        StretchOperations.ApplyStretch(document, plan, new PointF(0, 2));

        Assert.True(session.History.Undo(document, Tol));
        Assert.Equal(yBefore, TopologyService.GetEdgeStartPoint(document, bottom).Y, 3);

        Assert.True(session.History.Redo(document, Tol));
        Assert.Equal(2, TopologyService.GetEdgeStartPoint(document, bottom).Y, 3);
        TopologyValidator.AssertValid(document, Tol);
    }

    [Fact]
    public void StretchTool_ActivationRequiresSingleEdge()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.ToolService.ActivateTool(harness.StretchTool);
            Assert.False(harness.StretchTool.HasValidPlan);

            harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);
            harness.StretchTool.OnActivated();
            Assert.True(harness.StretchTool.HasValidPlan);
        });
    }

    [Fact]
    public void StretchTool_PreviewDoesNotMutateDocument()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            SelectBottomEdge(harness);
            harness.ToolService.ActivateTool(harness.StretchTool);
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.StretchTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(1, 0.5f));
            harness.StretchTool.OnMouseMove(CreateMouseEvent(), new PointF(1, 2.5f));

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
        });
    }

    [Fact]
    public void StretchTool_CommitStretchesRectangle()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            SelectBottomEdge(harness);
            var bottomId = harness.Session.Selection.SelectedEdgeIds.First();
            SetPreciseStretchZoom(harness);
            harness.ToolService.ActivateTool(harness.StretchTool);

            harness.StretchTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(1, 0.5f));
            harness.StretchTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(1, 1.5f));

            var bottom = harness.Session.Document.Edges.First(edge => edge.Id == bottomId);
            Assert.Equal(1, TopologyService.GetEdgeStartPoint(harness.Session.Document, bottom).Y, 3);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void StretchTool_InvalidEdge_HasNoPlan()
    {
        RunSta(() =>
        {
            var harness = new StretchHarness();
            TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(0, 0), new PointF(3, 0), Tol);
            harness.Session.Selection.SelectedEdgeIds.Add(harness.Session.Document.Edges[0].Id);
            harness.ToolService.ActivateTool(harness.StretchTool);

            Assert.False(harness.StretchTool.HasValidPlan);
        });
    }

    [Fact]
    public void Stretch_LeftClick_SelectsValidEdge()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            var bottom = FindHorizontalEdge(harness.Session.Document, y: 0, minX: 0, maxX: 4);

            harness.LeftClick(new PointF(2, 0));

            Assert.True(harness.StretchTool.HasValidPlan);
            Assert.Single(harness.Session.Selection.SelectedEdgeIds);
            Assert.Equal(bottom.Id, harness.Session.Selection.SelectedEdgeIds.First());
        });
    }

    [Fact]
    public void Stretch_LeftClick_ReplacesPreviousEdge()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            var top = FindHorizontalEdge(harness.Session.Document, y: 4, minX: 0, maxX: 4);

            harness.LeftClick(new PointF(2, 0));
            harness.LeftClick(new PointF(2, 4));

            Assert.True(harness.StretchTool.HasValidPlan);
            Assert.Single(harness.Session.Selection.SelectedEdgeIds);
            Assert.Equal(top.Id, harness.Session.Selection.SelectedEdgeIds.First());
        });
    }

    [Fact]
    public void Stretch_LeftClick_InvalidEdgeDoesNothing()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness(includeRectangle: false);
            TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(0, 0), new PointF(3, 0), Tol);

            harness.LeftClick(new PointF(1.5f, 0));

            Assert.False(harness.StretchTool.HasValidPlan);
            Assert.Empty(harness.Session.Selection.SelectedEdgeIds);
        });
    }

    [Fact]
    public void Stretch_RightClick_ClearsSelection()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            harness.LeftClick(new PointF(2, 0));

            harness.RightClick();

            Assert.Empty(harness.Session.Selection.SelectedEdgeIds);
            Assert.False(harness.StretchTool.HasValidPlan);
        });
    }

    [Fact]
    public void Stretch_RightClick_StaysInStretchTool()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            harness.LeftClick(new PointF(2, 0));

            harness.RightClick();

            Assert.Same(harness.StretchTool, harness.ToolService.ActiveTool);
        });
    }

    [Fact]
    public void StretchDistanceInput_1000()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            var bottom = FindHorizontalEdge(harness.Session.Document, y: 0, minX: 0, maxX: 4);
            harness.LeftClick(new PointF(2, 0));
            harness.LeftClick(new PointF(1, 0.5f));
            harness.MoveMouse(new PointF(1, 2.5f));

            Assert.True(harness.StretchTool.TryApplyLengthInput("1000"));

            Assert.Equal(1000, TopologyService.GetEdgeStartPoint(harness.Session.Document, bottom).Y, 3);
        });
    }

    [Fact]
    public void StretchDistanceInput_NegativeDirection_UsesPositiveDistance()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            var bottom = FindHorizontalEdge(harness.Session.Document, y: 0, minX: 0, maxX: 4);
            harness.LeftClick(new PointF(2, 0));
            harness.LeftClick(new PointF(1, 0.5f));
            harness.MoveMouse(new PointF(1, -1.5f));

            Assert.True(harness.StretchTool.ProjectedPreviewDelta.Y < 0);
            Assert.True(harness.StretchTool.TryApplyLengthInput("1000"));

            Assert.Equal(-1000, TopologyService.GetEdgeStartPoint(harness.Session.Document, bottom).Y, 3);
        });
    }

    [Fact]
    public void StretchDistanceInput_EnterCommitsImmediately()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            var bottom = FindHorizontalEdge(harness.Session.Document, y: 0, minX: 0, maxX: 4);
            harness.LeftClick(new PointF(2, 0));
            harness.LeftClick(new PointF(1, 0.5f));
            harness.MoveMouse(new PointF(1, 1.5f));

            Assert.True(harness.CommitDistanceEnter("1"));
            Assert.False(harness.StretchTool.HasActiveOperation);
            Assert.Equal(1, TopologyService.GetEdgeStartPoint(harness.Session.Document, bottom).Y, 3);
        });
    }

    [Fact]
    public void StretchDistanceInput_ClearsAfterCommit()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            harness.LeftClick(new PointF(2, 0));
            harness.LeftClick(new PointF(1, 0.5f));
            harness.MoveMouse(new PointF(1, 1.5f));
            harness.StatusBar.SetLineInputText("1");

            Assert.True(harness.PressDistanceEnter());
            Assert.Equal(string.Empty, harness.StatusBar.LineInputText);
        });
    }

    [Fact]
    public void StretchDistanceInput_InvalidValuePreserved()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            harness.LeftClick(new PointF(2, 0));
            harness.LeftClick(new PointF(1, 0.5f));
            harness.MoveMouse(new PointF(1, 1.5f));
            harness.StatusBar.SetLineInputText("abc");

            harness.PressDistanceEnter();

            Assert.Equal("abc", harness.StatusBar.LineInputText);
            Assert.True(harness.StretchTool.HasActiveOperation);
        });
    }

    [Fact]
    public void ValidRoomWallStretchStillWorks()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            var bottomId = FindHorizontalEdge(harness.Session.Document, y: 0, minX: 0, maxX: 4).Id;

            harness.LeftClick(new PointF(2, 0));
            harness.LeftClick(new PointF(1, 0.5f));
            harness.LeftClick(new PointF(1, 1.5f));

            var bottom = harness.Session.Document.Edges.First(edge => edge.Id == bottomId);
            Assert.Equal(1, TopologyService.GetEdgeStartPoint(harness.Session.Document, bottom).Y, 3);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void StretchPreservesConnectedPerpendicularEdges()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            var topLeftBefore = harness.Session.Document.Vertices.First(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 4), Tol)).Id;

            harness.LeftClick(new PointF(2, 0));
            harness.LeftClick(new PointF(1, 0.5f));
            harness.LeftClick(new PointF(1, 1.5f));

            Assert.Equal(
                new PointF(0, 4),
                TopologyService.GetVertexPosition(harness.Session.Document, topLeftBefore));
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void StretchRejectsNonPerpendicularConnections()
    {
        var document = CreateLShape();
        var vertical = document.Edges.First(edge =>
            MathUtils.ArePointsEqual(TopologyService.GetEdgeStartPoint(document, edge), new PointF(0, 0), Tol)
            && MathUtils.ArePointsEqual(TopologyService.GetEdgeEndPoint(document, edge), new PointF(0, 2), Tol));

        Assert.False(StretchOperations.TryCreatePlan(document, vertical.Id, Tol, out _));
    }

    [Fact]
    public void StretchRejectsNonParallelConnections()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(4, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(0, 4), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(4, 0), new PointF(4, 4), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(1, 1), Tol);

        var bottom = FindHorizontalEdge(document, y: 0, minX: 0, maxX: 4);
        Assert.False(StretchOperations.TryCreatePlan(document, bottom.Id, Tol, out _));
    }

    [Fact]
    public void StretchRebuildsFaces()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            harness.LeftClick(new PointF(2, 0));
            harness.LeftClick(new PointF(1, 0.5f));
            harness.LeftClick(new PointF(1, -0.5f));

            Assert.Single(harness.Session.Document.Polygons);
            Assert.Equal(20.0, PolygonGeometry.GetArea(harness.Session.Document, harness.Session.Document.Polygons[0], Tol), 3);
        });
    }

    [Fact]
    public void Stretch_EnterCreatesSingleUndo()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            var bottom = FindHorizontalEdge(harness.Session.Document, y: 0, minX: 0, maxX: 4);
            harness.LeftClick(new PointF(2, 0));
            harness.LeftClick(new PointF(1, 0.5f));
            harness.MoveMouse(new PointF(1, 1.5f));

            Assert.True(harness.CommitDistanceEnter("1"));
            Assert.Equal(1, TopologyService.GetEdgeStartPoint(harness.Session.Document, bottom).Y, 3);

            Assert.True(harness.Session.History.Undo(harness.Session.Document, Tol));
            Assert.Equal(0, TopologyService.GetEdgeStartPoint(harness.Session.Document, bottom).Y, 3);
            Assert.False(harness.Session.History.Undo(harness.Session.Document, Tol));
        });
    }

    [Fact]
    public void Stretch_UndoRedo()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            var bottom = FindHorizontalEdge(harness.Session.Document, y: 0, minX: 0, maxX: 4);
            harness.LeftClick(new PointF(2, 0));
            harness.LeftClick(new PointF(1, 0.5f));
            harness.MoveMouse(new PointF(1, 2.5f));

            Assert.True(harness.CommitDistanceEnter("2"));
            var stretched = TopologyService.GetEdgeStartPoint(harness.Session.Document, bottom).Y;

            Assert.True(harness.Session.History.Undo(harness.Session.Document, Tol));
            Assert.Equal(0, TopologyService.GetEdgeStartPoint(harness.Session.Document, bottom).Y, 3);

            Assert.True(harness.Session.History.Redo(harness.Session.Document, Tol));
            Assert.Equal(stretched, TopologyService.GetEdgeStartPoint(harness.Session.Document, bottom).Y, 3);
        });
    }

    [Fact]
    public void StretchPreviewDoesNotMutateDocument()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            harness.LeftClick(new PointF(2, 0));
            var fingerprintBefore = DocumentFingerprint(harness.Session.Document);

            harness.LeftClick(new PointF(1, 0.5f));
            harness.MoveMouse(new PointF(1, 2.5f));

            Assert.Equal(fingerprintBefore, DocumentFingerprint(harness.Session.Document));
        });
    }

    private static CadDocument CreateRectangle(float x, float y, float width, float height)
    {
        var document = new CadDocument();
        return CreateRectangle(document, x, y, width, height);
    }

    private static CadDocument CreateRectangle(CadDocument document, float x, float y, float width, float height)
    {
        TestDocumentHelpers.AddEdge(document, new PointF(x, y), new PointF(x + width, y), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(x + width, y), new PointF(x + width, y + height), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(x + width, y + height), new PointF(x, y + height), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(x, y + height), new PointF(x, y), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        return document;
    }

    private static Edge FindHorizontalEdge(CadDocument document, float y, float minX, float maxX)
        => document.Edges.First(edge =>
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            var edgeMinX = Math.Min(start.X, end.X);
            var edgeMaxX = Math.Max(start.X, end.X);
            return Math.Abs(start.Y - y) < 0.01
                && Math.Abs(end.Y - y) < 0.01
                && Math.Abs(edgeMinX - minX) < 0.01
                && Math.Abs(edgeMaxX - maxX) < 0.01;
        });

    private static StretchHarness CreateHarness()
    {
        var harness = new StretchHarness();
        CreateRectangle(harness.Session.Document, 0, 0, 4, 4);
        return harness;
    }

    private static StretchHarness CreateInteractiveHarness(bool includeRectangle = true)
    {
        var harness = new StretchHarness(interactive: true);
        if (includeRectangle)
        {
            CreateRectangle(harness.Session.Document, 0, 0, 4, 4);
        }

        SetPreciseStretchZoom(harness);
        harness.ToolService.ActivateTool(harness.StretchTool);
        return harness;
    }

    private static CadDocument CreateLShape()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(4, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(4, 0), new PointF(4, 4), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(4, 4), new PointF(0, 4), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 4), new PointF(0, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(0, 2), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 2), new PointF(2, 2), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        return document;
    }

    private static void SelectBottomEdge(StretchHarness harness)
    {
        var bottom = FindHorizontalEdge(harness.Session.Document, y: 0, minX: 0, maxX: 4);
        harness.Session.Selection.SelectedEdgeIds.Add(bottom.Id);
    }

    private static void SetPreciseStretchZoom(StretchHarness harness)
        => harness.Session.Camera.ZoomAt(new Point(400, 300), 100, harness.ViewportSize);

    private static string DocumentFingerprint(CadDocument document)
        => string.Join("|",
            document.Vertices.Select(vertex => $"{vertex.Id}:{vertex.Position.X},{vertex.Position.Y}"),
            document.Edges.Select(edge => edge.Id));

    private static void RunSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }

    private static MouseButtonEventArgs CreateMouseButton(MouseButton button)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, button);
        args.RoutedEvent = button == MouseButton.Left
            ? UIElement.MouseLeftButtonDownEvent
            : UIElement.MouseRightButtonDownEvent;
        return args;
    }

    private static MouseEventArgs CreateMouseEvent()
        => new(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseMoveEvent };

    private sealed class StretchHarness : IDisposable
    {
        private readonly Window? _window;

        public StretchHarness(bool interactive = false)
        {
            Session = new CadSession();
            ToolService = Session.ToolService;
            StretchTool = new StretchTool();
            StatusBar = interactive ? new StatusBar() : null!;

            if (interactive)
            {
                _window = new Window
                {
                    Width = 800,
                    Height = 600,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    Visibility = Visibility.Hidden
                };
                _window.Show();
            }

            var context = new ToolContext(
                Session,
                () => ViewportSize,
                screen => Session.Camera.ScreenToWorld(screen, ViewportSize),
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                setLength: length => StatusBar?.SetLength(length),
                setLengthInputEnabled: enabled => StatusBar?.SetLengthInputEnabled(enabled),
                resetLengthInput: length => StatusBar?.ResetLengthEditing(length),
                processLengthKey: e => StatusBar?.ProcessLengthKey(e) == true,
                setLineInputModeEnabled: (enabled, label) => StatusBar?.SetLengthInputEnabled(enabled, label),
                getLineInputText: () => StatusBar?.LineInputText ?? string.Empty,
                setLineInputText: text => StatusBar?.SetLineInputText(text),
                recordUndo: () => Session.History.Record(Session.Document));

            ToolService.Initialize(context);

            if (interactive && StatusBar is not null)
            {
                StatusBar.TryCommitLengthInput = input =>
                {
                    if (StretchTool.TryApplyLengthInput(input))
                    {
                        return true;
                    }

                    return double.TryParse(
                               input.Replace(',', '.'),
                               NumberStyles.Float,
                               CultureInfo.InvariantCulture,
                               out var length)
                           && StretchTool.TryApplyLength(length);
                };
            }
        }

        public CadSession Session { get; }

        public ToolService ToolService { get; }

        public StretchTool StretchTool { get; }

        public StatusBar StatusBar { get; } = null!;

        public Size ViewportSize { get; } = new(800, 600);

        public void LeftClick(PointF world)
            => StretchTool.OnMouseDown(CreateMouseButton(MouseButton.Left), world);

        public void RightClick()
            => StretchTool.OnMouseDown(CreateMouseButton(MouseButton.Right), PointF.Zero);

        public void MoveMouse(PointF world)
            => StretchTool.OnMouseMove(CreateMouseEvent(), world);

        public bool CommitDistanceEnter(string distance)
            => StatusBar.TryCommitLengthInput?.Invoke(distance) == true;

        public bool PressDistanceEnter()
        {
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(_window!),
                0,
                Key.Enter)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };

            StatusBar.ProcessLengthKey(args);
            return !StretchTool.HasActiveOperation;
        }

        public void Dispose()
        {
            _window?.Close();
        }
    }
}
