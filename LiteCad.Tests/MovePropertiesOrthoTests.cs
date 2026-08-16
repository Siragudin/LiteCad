using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using LiteCad.UI.Layout;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class MovePropertiesOrthoTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void MoveProperties_ContainOrthoToggle()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateMove();

            Assert.True(host.PropertiesPanel.IsMoveOrthoToggleVisible);
            Assert.True(host.PropertiesPanel.IsMoveToolPanelVisible);
        });
    }

    [Fact]
    public void MoveOrthoToggle_ChangesOrthoEnabled()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateMove();

            host.PropertiesPanel.SetMoveOrthoChecked(true);
            Assert.True(host.Session.LineToolOptions.OrthoEnabled);
            Assert.True(host.PropertiesPanel.MoveOrthoIsChecked);

            host.PropertiesPanel.SetMoveOrthoChecked(false);
            Assert.False(host.Session.LineToolOptions.OrthoEnabled);
            Assert.False(host.PropertiesPanel.MoveOrthoIsChecked);
        });
    }

    [Fact]
    public void MoveOrthoToggle_On_ShowsDistanceInput()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateMove();
            host.PropertiesPanel.SetMoveOrthoChecked(true);
            host.BeginMove(new PointF(0.5, 0));

            Assert.True(host.StatusBar.IsLineInputActive);
            Assert.Equal("Distance:", host.StatusBar.LineInputLabelText);
        });
    }

    [Fact]
    public void MoveOrthoToggle_Off_ShowsXYInput()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateMove();
            host.PropertiesPanel.SetMoveOrthoChecked(false);
            host.BeginMove(new PointF(0.5, 0));

            Assert.True(host.StatusBar.IsRectangleInputActive);
            Assert.Equal(DualFieldLabelMode.MoveOffset, host.StatusBar.DualFieldLabels);
            Assert.Equal("X:", host.StatusBar.FirstFieldLabelText);
        });
    }

    [Fact]
    public void MoveOrthoToggle_HiddenForLineTool()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateLine();

            Assert.False(host.PropertiesPanel.IsMoveToolPanelVisible);
        });
    }

    [Fact]
    public void MoveOrthoToggle_RestoresStateWhenReturningToMove()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateMove();
            host.PropertiesPanel.SetMoveOrthoChecked(true);

            host.ActivateLine();
            host.ActivateMove();

            Assert.True(host.PropertiesPanel.MoveOrthoIsChecked);
            Assert.True(host.Session.LineToolOptions.OrthoEnabled);
        });
    }

    [Fact]
    public void MoveOrthoToggle_DuringActiveMove_UpdatesPreviewWithoutCommit()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateMove();
            host.PropertiesPanel.SetMoveOrthoChecked(false);
            host.BeginMove(new PointF(0.5, 0));
            var fingerprintBefore = DocumentFingerprint(host.Session.Document);

            host.MoveMouse(new PointF(10, 2));
            host.PropertiesPanel.SetMoveOrthoChecked(true);

            Assert.Equal(fingerprintBefore, DocumentFingerprint(host.Session.Document));
            Assert.Equal(9.5, host.MoveTool.PreviewDelta.X, 3);
            Assert.Equal(0, host.MoveTool.PreviewDelta.Y, 3);
        });
    }

    private static MovePropertiesOrthoTestHost CreateHost() => new();

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

    private sealed class MovePropertiesOrthoTestHost : IDisposable
    {
        private readonly Window _window;

        public MovePropertiesOrthoTestHost()
        {
            Session = new CadSession();
            MoveTool = new MoveTool();
            LineTool = new LineTool();
            StatusBar = new StatusBar();
            PropertiesPanel = new PropertiesPanel();
            _window = new Window
            {
                Content = new System.Windows.Controls.Grid(),
                Width = 800,
                Height = 600,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Visibility = Visibility.Hidden
            };
            _window.Show();

            PropertiesPanel.BindSession(Session, () =>
            {
                if (Session.ToolService.ActiveTool is MoveTool moveTool)
                {
                    moveTool.NotifyOrthoChanged();
                }
            });

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
            CreateUnitSquare(Session.Document);
        }

        public CadSession Session { get; }

        public MoveTool MoveTool { get; }

        public LineTool LineTool { get; }

        public StatusBar StatusBar { get; }

        public PropertiesPanel PropertiesPanel { get; }

        public Size ViewportSize { get; } = new(800, 600);

        public void ActivateMove()
        {
            Session.ToolService.ActivateTool(MoveTool);
            PropertiesPanel.SetActiveTool(MoveTool.Name);
        }

        public void ActivateLine()
        {
            Session.ToolService.ActivateTool(LineTool);
            PropertiesPanel.SetActiveTool(LineTool.Name);
        }

        public void BeginMove(PointF basePoint)
        {
            Session.Camera.ZoomAt(new Point(400, 300), 100, ViewportSize);
            SelectBottomEdge();
            MoveTool.OnMouseDown(CreateMouseButton(MouseButton.Left), basePoint);
        }

        public void MoveMouse(PointF world)
            => MoveTool.OnMouseMove(CreateMouseEvent(), world);

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
