using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using LiteCad.UI.Layout;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class MoveExactInputTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void MoveOrthoOff_ShowsXYFields()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.SetBasePoint(new PointF(0.5, 0));

            Assert.True(host.StatusBar.IsRectangleInputActive);
            Assert.Equal(DualFieldLabelMode.MoveOffset, host.StatusBar.DualFieldLabels);
            Assert.Equal("X:", host.StatusBar.FirstFieldLabelText);
        });
    }

    [Fact]
    public void MoveOrthoOn_ShowsDistanceField()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.SetBasePoint(new PointF(0.5, 0));

            Assert.True(host.StatusBar.IsLineInputActive);
            Assert.Equal("Distance:", host.StatusBar.LineInputLabelText);
        });
    }

    [Fact]
    public void MoveOrthoOff_AltCyclesXAndY()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.SetBasePoint(new PointF(0.5, 0));

            Assert.Equal(RectangleSizeField.Width, host.StatusBar.ActiveRectangleField);
            host.PressAlt();
            Assert.Equal(RectangleSizeField.Height, host.StatusBar.ActiveRectangleField);
            host.PressAlt();
            Assert.Equal(RectangleSizeField.Width, host.StatusBar.ActiveRectangleField);
        });
    }

    [Fact]
    public void MoveOrthoOn_AltDoesNothing()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.SetBasePoint(new PointF(0.5, 0));

            host.PressAlt();
            Assert.True(host.StatusBar.IsLineInputActive);
            Assert.False(host.StatusBar.IsRectangleInputActive);
        });
    }

    [Fact]
    public void MoveOrthoHorizontalPositiveDistance()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.SetPreciseZoom();
            host.SetBasePoint(new PointF(0.5, 0));
            host.MoveMouse(new PointF(10, 2));

            Assert.True(host.MoveTool.HasActiveMove);
            Assert.Equal(9.5, host.MoveTool.PreviewDelta.X, 3);
            Assert.Equal(0, host.MoveTool.PreviewDelta.Y, 3);
        });
    }

    [Fact]
    public void MoveOrthoHorizontalPositiveDistance_EnterCommits()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.SetPreciseZoom();
            host.SetBasePoint(new PointF(0.5, 0));
            host.MoveMouse(new PointF(10, 2));

            Assert.True(host.MoveTool.TryApplyLength(1000));
            Assert.False(host.MoveTool.HasActiveMove);
            Assert.True(host.Session.Document.Vertices.Any(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(1000, 0), Tol)));
        });
    }

    [Fact]
    public void MoveOrthoHorizontalNegativeDistance()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.SetPreciseZoom();
            host.SetBasePoint(new PointF(0.5, 0));
            host.MoveMouse(new PointF(-10, 1));

            Assert.True(host.MoveTool.HasActiveMove);
            Assert.Equal(-10.5, host.MoveTool.PreviewDelta.X, 3);
            Assert.Equal(0, host.MoveTool.PreviewDelta.Y, 3);
        });
    }

    [Fact]
    public void MoveOrthoVerticalPositiveDistance()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.SetPreciseZoom();
            host.SetBasePoint(new PointF(0.5, 0));
            host.MoveMouse(new PointF(1, 10));

            Assert.True(host.MoveTool.HasActiveMove);
            Assert.Equal(0, host.MoveTool.PreviewDelta.X, 3);
            Assert.Equal(10, host.MoveTool.PreviewDelta.Y, 3);
        });
    }

    [Fact]
    public void MoveOrthoVerticalNegativeDistance()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.SetPreciseZoom();
            host.SetBasePoint(new PointF(0.5, 0));
            host.MoveMouse(new PointF(2, -10));

            Assert.True(host.MoveTool.HasActiveMove);
            Assert.Equal(0, host.MoveTool.PreviewDelta.X, 3);
            Assert.Equal(-10, host.MoveTool.PreviewDelta.Y, 3);
        });
    }

    [Fact]
    public void MoveXYExactDelta()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.SetPreciseZoom();
            host.BeginMove(new PointF(0.5, 0), allEdges: true);

            Assert.True(host.MoveTool.TryApplyRectangleSize("1000", "-500"));

            var vertex = host.Session.Document.Vertices.First(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(1000, -500), Tol));
            Assert.NotEqual(Guid.Empty, vertex.Id);
            TopologyValidator.AssertValid(host.Session.Document, Tol);
        });
    }

    [Fact]
    public void MoveDistanceExactDelta()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(true);
            host.SetPreciseZoom();
            host.SetBasePoint(new PointF(0.5, 0));
            host.MoveMouse(new PointF(5, 0));

            Assert.True(host.MoveTool.TryApplyLengthInput("750"));

            Assert.True(host.Session.Document.Vertices.Any(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(750, 0), Tol)));
            TopologyValidator.AssertValid(host.Session.Document, Tol);
        });
    }

    [Fact]
    public void ToggleOrtho_DoesNotMixInputValues()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.SetBasePoint(new PointF(0.5, 0));
            host.StatusBar.SetDualFieldInputText("1000", "-500");

            host.SetOrtho(true);
            host.MoveMouse(new PointF(0, 0));
            host.StatusBar.SetLineInputText("2000");

            host.SetOrtho(false);
            host.MoveMouse(new PointF(0, 0));

            Assert.Equal("1000", host.StatusBar.RectangleWidthText);
            Assert.Equal("-500", host.StatusBar.RectangleHeightText);

            host.SetOrtho(true);
            host.MoveMouse(new PointF(0, 0));

            Assert.Equal("2000", host.StatusBar.LineInputText);
        });
    }

    [Fact]
    public void PreviewDoesNotMutateDocument()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.SetBasePoint(new PointF(0.5, 0));
            var fingerprintBefore = DocumentFingerprint(host.Session.Document);

            Assert.True(host.MoveTool.TryApplyRectangleSize("1000", "-500"));
            host.MoveMouse(new PointF(1000, -500));

            Assert.Equal(fingerprintBefore, DocumentFingerprint(host.Session.Document));
        });
    }

    [Fact]
    public void UndoRedoStillWorks()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.SetPreciseZoom();
            host.SetBasePoint(new PointF(0.5, 0));
            var before = EdgeFingerprint(host.Session.Document);

            host.Session.History.Record(host.Session.Document);
            Assert.True(host.MoveTool.TryApplyRectangleSize("2", "0"));
            host.CommitMove();
            var moved = EdgeFingerprint(host.Session.Document);

            Assert.True(host.Session.History.Undo(host.Session.Document, Tol));
            Assert.Equal(before, EdgeFingerprint(host.Session.Document));

            Assert.True(host.Session.History.Redo(host.Session.Document, Tol));
            Assert.Equal(moved, EdgeFingerprint(host.Session.Document));
        });
    }

    [Fact]
    public void ExistingObjectMoveStillWorks()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.SetOrtho(false);
            host.BeginMove(new PointF(0.5, 0), allEdges: true);

            Assert.True(host.MoveTool.TryApplyRectangleSize("3", "0"));
            host.CommitMove();

            var minX = host.Session.Document.Vertices.Min(vertex => vertex.Position.X);
            Assert.Equal(3, minX, 3);
            TopologyValidator.AssertValid(host.Session.Document, Tol);
        });
    }

    private static MoveExactInputTestHost CreateHost() => new();

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

    private sealed class MoveExactInputTestHost : IDisposable
    {
        private readonly Window _window;

        public MoveExactInputTestHost()
        {
            Session = new CadSession();
            MoveTool = new MoveTool();
            StatusBar = new StatusBar();
            _window = new Window
            {
                Content = StatusBar,
                Width = 800,
                Height = 120,
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
                setLineInputText: StatusBar.SetLineInputText);

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

                return double.TryParse(input.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var length)
                    && MoveTool.TryApplyLength(length);
            };

            CreateUnitSquare(Session.Document);
        }

        public CadSession Session { get; }

        public MoveTool MoveTool { get; }

        public StatusBar StatusBar { get; }

        public Size ViewportSize { get; } = new(800, 600);

        public void SetOrtho(bool enabled)
            => Session.LineToolOptions.OrthoEnabled = enabled;

        public void SetPreciseZoom()
            => Session.Camera.ZoomAt(new Point(400, 300), 100, ViewportSize);

        public void BeginMove(PointF basePoint, bool allEdges = false)
        {
            SetPreciseZoom();
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

        public void SetBasePoint(PointF world) => BeginMove(world);

        public void MoveMouse(PointF world)
            => MoveTool.OnMouseMove(CreateMouseEvent(), world);

        public void CommitMove()
            => MoveTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(0, 0));

        public void SelectAllEdges()
        {
            Session.Selection.Clear();
            foreach (var edge in Session.Document.Edges)
            {
                Session.Selection.SelectedEdgeIds.Add(edge.Id);
            }
        }

        public void PressAlt()
        {
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(_window),
                0,
                Key.LeftAlt)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };

            StatusBar.ProcessRectangleSizeKey(args);
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
