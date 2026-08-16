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

public class MoveExactInputEnterCommitTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void MoveVertex_ExactDistance_EnterCommitsImmediately()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.SelectVertex(new PointF(0, 0));
            host.BeginVertexMove(new PointF(0.5, 0));
            host.MoveMouse(new PointF(5, 0));

            Assert.True(host.CommitDistanceEnter("2"));
            Assert.Equal(2, TopologyService.GetVertexPosition(host.Session.Document, host.SelectedVertexId).X, 3);
            Assert.False(host.MoveTool.HasActiveMove);
            TopologyValidator.AssertValid(host.Session.Document, Tol);
        });
    }

    [Fact]
    public void MoveObject_ExactDistance_EnterCommitsImmediately()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.BeginMove(new PointF(0.5, 0), allEdges: true);
            host.MoveMouse(new PointF(3, 0));

            Assert.True(host.CommitDistanceEnter("3"));
            Assert.Equal(3, host.Session.Document.Vertices.Min(vertex => vertex.Position.X), 3);
            Assert.False(host.MoveTool.HasActiveMove);
            TopologyValidator.AssertValid(host.Session.Document, Tol);
        });
    }

    [Fact]
    public void MoveOrtho_ExactDistance_EnterCommitsImmediately()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.BeginMove(new PointF(0.5, 0));
            host.MoveMouse(new PointF(-4, 1));

            Assert.True(host.CommitDistanceEnter("200"));
            Assert.True(host.Session.Document.Vertices.Any(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(-200, 0), Tol)));
            Assert.False(host.MoveTool.HasActiveMove);
        });
    }

    [Fact]
    public void MoveXY_ExactInput_EnterCommitsImmediately()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.BeginMove(new PointF(0.5, 0), allEdges: true);

            Assert.True(host.CommitOffsetEnter("1000", "-500"));
            Assert.True(host.Session.Document.Vertices.Any(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(1000, -500), Tol)));
            Assert.False(host.MoveTool.HasActiveMove);
        });
    }

    [Fact]
    public void MoveInput_ClearsAfterSuccessfulEnter()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.BeginMove(new PointF(0.5, 0));
            host.MoveMouse(new PointF(5, 0));
            host.StatusBar.SetLineInputText("200");

            Assert.True(host.PressDistanceEnter());
            Assert.Equal(string.Empty, host.StatusBar.LineInputText);
        });
    }

    [Fact]
    public void MoveInput_InvalidValueIsNotCleared()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.BeginMove(new PointF(0.5, 0));
            host.MoveMouse(new PointF(5, 0));
            host.StatusBar.SetLineInputText("abc");

            host.PressDistanceEnter();
            Assert.Equal("abc", host.StatusBar.LineInputText);
            Assert.True(host.MoveTool.HasActiveMove);
        });
    }

    [Fact]
    public void MoveAfterExactCommit_StartsWithEmptyInput()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.BeginMove(new PointF(0.5, 0));
            host.MoveMouse(new PointF(5, 0));
            host.StatusBar.SetLineInputText("1");
            Assert.True(host.PressDistanceEnter());

            host.BeginMove(new PointF(0.5, 0));

            Assert.Equal(string.Empty, host.StatusBar.LineInputText);
        });
    }

    [Fact]
    public void MoveMouseOnly_StillRequiresSecondClick()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.Session.Camera.ZoomAt(new Point(400, 300), 100, host.ViewportSize);
            host.SelectBottomEdge();
            var before = EdgeFingerprint(host.Session.Document);

            host.MoveTool.OnMouseDown(host.CreateMouseButton(MouseButton.Left), new PointF(0.5f, 0));
            Assert.True(host.MoveTool.HasActiveMove);

            host.MoveTool.OnMouseMove(host.CreateMouseEvent(), new PointF(5, 0));
            Assert.Equal(before, EdgeFingerprint(host.Session.Document));

            host.MoveTool.OnMouseDown(host.CreateMouseButton(MouseButton.Left), new PointF(5, 0));
            Assert.NotEqual(before, EdgeFingerprint(host.Session.Document));
            Assert.False(host.MoveTool.HasActiveMove);
        });
    }

    [Fact]
    public void EnterDoesNotCommitTwice()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.BeginMove(new PointF(0.5, 0), allEdges: true);
            host.MoveMouse(new PointF(5, 0));
            var before = DocumentFingerprint(host.Session.Document);

            Assert.True(host.CommitDistanceEnter("1"));
            var afterFirst = DocumentFingerprint(host.Session.Document);

            Assert.False(host.CommitDistanceEnter("1"));
            Assert.Equal(afterFirst, DocumentFingerprint(host.Session.Document));
            Assert.True(host.Session.History.Undo(host.Session.Document, Tol));
            Assert.Equal(before, DocumentFingerprint(host.Session.Document));
            Assert.False(host.Session.History.Undo(host.Session.Document, Tol));
        });
    }

    [Fact]
    public void UndoAfterEnterCommit_IsSingleOperation()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.BeginMove(new PointF(0.5, 0), allEdges: true);
            var before = EdgeFingerprint(host.Session.Document);

            Assert.True(host.CommitOffsetEnter("2", "0"));
            var after = EdgeFingerprint(host.Session.Document);

            Assert.True(host.Session.History.CanUndo);
            Assert.True(host.Session.History.Undo(host.Session.Document, Tol));
            Assert.Equal(before, EdgeFingerprint(host.Session.Document));
            Assert.NotEqual(after, EdgeFingerprint(host.Session.Document));
        });
    }

    [Fact]
    public void RedoAfterEnterCommit_RestoresMovedState()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.BeginMove(new PointF(0.5, 0), allEdges: true);

            Assert.True(host.CommitOffsetEnter("2", "0"));
            var moved = EdgeFingerprint(host.Session.Document);

            Assert.True(host.Session.History.Undo(host.Session.Document, Tol));
            Assert.True(host.Session.History.Redo(host.Session.Document, Tol));
            Assert.Equal(moved, EdgeFingerprint(host.Session.Document));
        });
    }

    private static MoveExactInputEnterCommitTestHost CreateHost() => new();

    private static string DocumentFingerprint(CadDocument document)
        => string.Join("|",
            document.Vertices.Select(vertex => $"{vertex.Id}:{vertex.Position.X},{vertex.Position.Y}"),
            document.Edges.Select(edge => edge.Id));

    private static string EdgeFingerprint(CadDocument document)
        => string.Join(";",
            document.Edges
                .OrderBy(edge => edge.Id)
                .Select(edge =>
                {
                    var start = TopologyService.GetEdgeStartPoint(document, edge);
                    var end = TopologyService.GetEdgeEndPoint(document, edge);
                    return $"{start.X},{start.Y}->{end.X},{end.Y}";
                }));

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

    private sealed class MoveExactInputEnterCommitTestHost : IDisposable
    {
        private readonly Window _window;

        public MoveExactInputEnterCommitTestHost()
        {
            Session = new CadSession();
            MoveTool = new MoveTool();
            StatusBar = new StatusBar();
            _window = new Window
            {
                Width = 800,
                Height = 600,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Visibility = Visibility.Hidden
            };
            _window.Show();

            var context = new ToolContext(
                Session,
                () => ViewportSize,
                screen => Session.Camera.ScreenToWorld(screen, ViewportSize),
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                setRectangleSizeInputEnabled: enabled => StatusBar.SetRectangleSizeInputEnabled(enabled),
                setRectangleSizePreview: StatusBar.SetRectangleSizePreview,
                resetRectangleSizeInput: StatusBar.ResetRectangleSizeInput,
                processRectangleSizeKey: StatusBar.ProcessRectangleSizeKey,
                setLengthInputEnabled: enabled => StatusBar.SetLengthInputEnabled(enabled),
                resetLengthInput: StatusBar.ResetLengthEditing,
                processLengthKey: StatusBar.ProcessLengthKey,
                setDualFieldInputEnabled: StatusBar.SetRectangleSizeInputEnabled,
                setLineInputModeEnabled: StatusBar.SetLengthInputEnabled,
                getDualFieldInputText: StatusBar.GetDualFieldInputText,
                setDualFieldInputText: StatusBar.SetDualFieldInputText,
                getLineInputText: () => StatusBar.LineInputText,
                setLineInputText: StatusBar.SetLineInputText,
                recordUndo: () => Session.History.Record(Session.Document));

            Session.ToolService.Initialize(context);
            Session.ToolService.ActivateTool(MoveTool);

            StatusBar.TryCommitRectangleSizeInput = sizes =>
                MoveTool.TryApplyRectangleSize(sizes.Width, sizes.Height);
            StatusBar.TryCommitLengthInput = input =>
            {
                if (MoveTool.TryApplyLengthInput(input))
                {
                    return true;
                }

                return double.TryParse(
                           input.Replace(',', '.'),
                           NumberStyles.Float,
                           CultureInfo.InvariantCulture,
                           out var length)
                       && MoveTool.TryApplyLength(length);
            };

            CreateUnitSquare(Session.Document);
        }

        public CadSession Session { get; }

        public MoveTool MoveTool { get; }

        public StatusBar StatusBar { get; }

        public Guid SelectedVertexId { get; private set; }

        public Size ViewportSize { get; } = new(800, 600);

        public void SetOrtho(bool enabled)
            => Session.LineToolOptions.OrthoEnabled = enabled;

        public void SelectVertex(PointF position)
        {
            Session.Selection.Clear();
            var vertex = Session.Document.Vertices.First(item =>
                MathUtils.ArePointsEqual(item.Position, position, Tol));
            SelectedVertexId = vertex.Id;
            Session.Selection.SelectedVertexIds.Add(vertex.Id);
        }

        public void BeginVertexMove(PointF basePoint)
        {
            Session.Camera.ZoomAt(new Point(400, 300), 100, ViewportSize);
            MoveTool.OnMouseDown(CreateMouseButton(MouseButton.Left), basePoint);
        }

        public void BeginMove(PointF basePoint, bool allEdges = false)
        {
            Session.Camera.ZoomAt(new Point(400, 300), 100, ViewportSize);
            if (allEdges)
            {
                SelectAllEdges();
            }
            else
            {
                SelectBottomEdge();
            }

            MoveTool.OnMouseDown(CreateMouseButton(MouseButton.Left), basePoint);
        }

        public void MoveMouse(PointF world)
            => MoveTool.OnMouseMove(CreateMouseEvent(), world);

        public void CommitMove(PointF destination)
            => MoveTool.OnMouseDown(CreateMouseButton(MouseButton.Left), destination);

        public bool CommitDistanceEnter(string distance)
            => StatusBar.TryCommitLengthInput?.Invoke(distance) == true;

        public bool PressDistanceEnter()
        {
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(_window),
                0,
                Key.Enter)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };

            StatusBar.ProcessLengthKey(args);
            return !MoveTool.HasActiveMove;
        }

        public bool CommitOffsetEnter(string x, string y)
            => StatusBar.TryCommitRectangleSizeInput?.Invoke((x, y)) == true;

        public bool PressOffsetEnter(string x, string y)
        {
            StatusBar.SetDualFieldInputText(x, y);
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(_window),
                0,
                Key.Enter)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };

            StatusBar.ProcessRectangleSizeKey(args);
            return !MoveTool.HasActiveMove;
        }

        public void Dispose()
        {
            _window.Close();
        }

        public void SelectBottomEdge()
        {
            Session.Selection.Clear();
            var bottom = Session.Document.Edges.First(edge =>
            {
                var start = TopologyService.GetEdgeStartPoint(Session.Document, edge);
                var end = TopologyService.GetEdgeEndPoint(Session.Document, edge);
                return Math.Abs(start.Y) < 0.01 && Math.Abs(end.Y) < 0.01;
            });
            Session.Selection.SelectedEdgeIds.Add(bottom.Id);
        }

        public MouseButtonEventArgs CreateMouseButton(MouseButton button)
        {
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, button);
            args.RoutedEvent = button == MouseButton.Left
                ? UIElement.MouseLeftButtonDownEvent
                : UIElement.MouseRightButtonDownEvent;
            return args;
        }

        public MouseEventArgs CreateMouseEvent()
            => new(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseMoveEvent };

        private void SelectAllEdges()
        {
            Session.Selection.Clear();
            foreach (var edge in Session.Document.Edges)
            {
                Session.Selection.SelectedEdgeIds.Add(edge.Id);
            }
        }

        private static void CreateUnitSquare(CadDocument document)
        {
            TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(1, 0), Tol);
            TestDocumentHelpers.AddEdge(document, new PointF(1, 0), new PointF(1, 1), Tol);
            TestDocumentHelpers.AddEdge(document, new PointF(1, 1), new PointF(0, 1), Tol);
            TestDocumentHelpers.AddEdge(document, new PointF(0, 1), new PointF(0, 0), Tol);
            PolygonBuilder.SyncFaces(document, Tol);
        }
    }
}
