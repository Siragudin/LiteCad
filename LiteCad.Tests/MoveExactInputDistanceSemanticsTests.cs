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

public class MoveExactInputDistanceSemanticsTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void OrthoRight_Distance200_ProducesPlus200()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.BeginMove(new PointF(0.5, 0));
            host.MoveMouse(new PointF(10, 2));

            Assert.True(host.MoveTool.TryApplyLengthInput("200"));
            Assert.True(host.Session.Document.Vertices.Any(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(200, 0), Tol)));
        });
    }

    [Fact]
    public void OrthoLeft_Distance200_ProducesMinus200()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.BeginMove(new PointF(0.5, 0));
            host.MoveMouse(new PointF(-10, 1));

            Assert.True(host.MoveTool.TryApplyLengthInput("200"));
            Assert.True(host.Session.Document.Vertices.Any(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(-200, 0), Tol)));
        });
    }

    [Fact]
    public void OrthoUp_Distance200_ProducesPlus200()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.BeginMove(new PointF(0.5, 0));
            host.MoveMouse(new PointF(1, 10));

            Assert.True(host.MoveTool.TryApplyLengthInput("200"));
            Assert.True(host.Session.Document.Vertices.Any(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 200), Tol)));
        });
    }

    [Fact]
    public void OrthoDown_Distance200_ProducesMinus200()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.BeginMove(new PointF(0.5, 0));
            host.MoveMouse(new PointF(2, -10));

            Assert.True(host.MoveTool.TryApplyLengthInput("200"));
            Assert.True(host.Session.Document.Vertices.Any(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(0, -200), Tol)));
        });
    }

    [Fact]
    public void DistanceFieldNeverRequiresNegativeValue()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.BeginMove(new PointF(0.5, 0));
            host.MoveMouse(new PointF(-10, 1));

            Assert.True(host.MoveTool.PreviewDelta.X < 0);
            Assert.False(host.MoveTool.TryApplyLengthInput("-200"));
            Assert.True(host.LastDistanceDisplay >= 0);
            Assert.True(host.MoveTool.TryApplyLengthInput("200"));
            Assert.True(host.Session.Document.Vertices.Any(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(-200, 0), Tol)));
        });
    }

    [Fact]
    public void XPositiveUnchanged()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.BeginMove(new PointF(0.5, 0), allEdges: true);

            Assert.True(host.MoveTool.TryApplyRectangleSize("200", "0"));
            Assert.True(host.Session.Document.Vertices.Any(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(200, 0), Tol)));
        });
    }

    [Fact]
    public void XNegativeUnchanged()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.BeginMove(new PointF(0.5, 0), allEdges: true);

            Assert.True(host.MoveTool.TryApplyRectangleSize("-200", "0"));
            Assert.True(host.Session.Document.Vertices.Any(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(-200, 0), Tol)));
        });
    }

    [Fact]
    public void YPositiveUnchanged()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.BeginMove(new PointF(0.5, 0), allEdges: true);

            Assert.True(host.MoveTool.TryApplyRectangleSize("0", "200"));
            Assert.True(host.Session.Document.Vertices.Any(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 200), Tol)));
        });
    }

    [Fact]
    public void YNegativeUnchanged()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.BeginMove(new PointF(0.5, 0), allEdges: true);

            Assert.True(host.MoveTool.TryApplyRectangleSize("0", "-200"));
            Assert.True(host.Session.Document.Vertices.Any(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(0, -200), Tol)));
        });
    }

    [Fact]
    public void EnterCommitsCorrectSignedDelta()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.BeginMove(new PointF(0.5, 0));
            host.MoveMouse(new PointF(-8, 0));
            host.StatusBar.SetLineInputText("200");

            Assert.True(host.PressDistanceEnter());
            Assert.True(host.Session.Document.Vertices.Any(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(-200, 0), Tol)));
            Assert.False(host.MoveTool.HasActiveMove);
        });
    }

    [Fact]
    public void InputClearsAfterCommit()
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

    private static MoveExactInputDistanceSemanticsTestHost CreateHost() => new();

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

    private sealed class MoveExactInputDistanceSemanticsTestHost : IDisposable
    {
        private readonly Window _window;

        public MoveExactInputDistanceSemanticsTestHost()
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
                setLength: length =>
                {
                    LastDistanceDisplay = length ?? 0;
                    StatusBar.SetLength(length);
                },
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

        public double LastDistanceDisplay { get; private set; }

        public Size ViewportSize { get; } = new(800, 600);

        public void SetOrtho(bool enabled)
            => Session.LineToolOptions.OrthoEnabled = enabled;

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

        public void Dispose()
        {
            _window.Close();
        }

        private void SelectBottomEdge()
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
    }
}
