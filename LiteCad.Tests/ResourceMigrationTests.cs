using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Resources;
using LiteCad.Services;
using LiteCad.Tools;
using LiteCad.UI.Layout;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class ResourceMigrationTests
{
    private const double Tol = 1e-4;

    private static readonly string[] RequiredResourceKeys =
    [
        "Tool.Selection",
        "Tool.Move",
        "Status.ToolActive",
        "Label.Distance",
        "Label.PolygonType.Face",
        "Selection.NothingSelected",
        "Input.Move.Idle",
        "Menu.Header.File",
        "Format.Coordinates"
    ];

    [Fact]
    public void AllRequiredResourceKeysExist()
    {
        foreach (var key in RequiredResourceKeys)
        {
            var value = Strings.Get(key);
            Assert.False(string.IsNullOrWhiteSpace(value));
            Assert.NotEqual(key, value);
        }
    }

    [Fact]
    public void ToolId_IsIndependentFromDisplayText()
    {
        var moveTool = new MoveTool();
        Assert.Equal(ToolId.Move, moveTool.Id);
        Assert.Equal(Strings.Tool_Move, ToolDisplayNames.Get(ToolId.Move));
        Assert.Equal(Strings.Tool_Move, moveTool.Name);
        Assert.NotEqual(Strings.Tool_Select_Short, moveTool.Name);

        Assert.Equal(Strings.Tool_Select_Short, Strings.Get("Tool.Select.Short"));
        Assert.Equal(Strings.Tool_Selection, ToolDisplayNames.Get(ToolId.Selection));
        Assert.NotEqual(Strings.Tool_Select_Short, ToolDisplayNames.Get(ToolId.Selection));
    }

    [Fact]
    public void PolygonTypeDisplay_MapsToResourceStrings()
    {
        Assert.Equal(Strings.Label_PolygonType_Face, PolygonTypeDisplay.Get(PolygonType.Face));
        Assert.Equal(Strings.Label_PolygonType_Wall, PolygonTypeDisplay.Get(PolygonType.Wall));
        Assert.Equal(Strings.Label_PolygonType_Room, PolygonTypeDisplay.Get(PolygonType.Room));
        Assert.Equal(Strings.Label_PolygonType_Axis, PolygonTypeDisplay.Get(PolygonType.Axis));
        Assert.Same(Strings.Label_PolygonType_Face, PolygonTypeDisplay.Get(PolygonType.Face));
    }

    [Fact]
    public void MoveInputMode_DoesNotDependOnDistanceLabelText()
    {
        RunSta(() =>
        {
            using var host = CreateMoveHost(orthoEnabled: true);
            host.BeginMove(new PointF(0.5f, 0f));

            Assert.Equal(LineInputLabelMode.Distance, host.StatusBar.LineInputLabelMode);
            Assert.Equal(Strings.Label_Distance, host.StatusBar.LineInputLabelText);

            host.StatusBar.SetLengthInputEnabled(true, LineInputLabelMode.Distance);
            Assert.Equal(LineInputLabelMode.Distance, host.StatusBar.LineInputLabelMode);
        });
    }

    [Fact]
    public void ExistingToolBehavior_UnchangedAfterResourceMigration()
    {
        Assert.Equal(ToolId.Move, new MoveTool().Id);
        Assert.Equal(ToolId.Stretch, new StretchTool().Id);
        Assert.Equal(ToolId.Line, new LineTool().Id);
        Assert.Equal(Strings.Tool_Move, new MoveTool().Name);
        Assert.Equal(Strings.Tool_Stretch, new StretchTool().Name);
        Assert.Equal(Strings.Tool_Line, new LineTool().Name);
    }

    [Fact]
    public void StringsResx_IsEmbeddedResource()
    {
        var resourceName = typeof(Strings).Assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("Strings.resources", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(resourceName);
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    private static MoveResourceTestHost CreateMoveHost(bool orthoEnabled)
    {
        var host = new MoveResourceTestHost();
        host.SetOrtho(orthoEnabled);
        return host;
    }

    private sealed class MoveResourceTestHost : IDisposable
    {
        private readonly Window _window;

        public MoveResourceTestHost()
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

            var viewportSize = new Size(800, 600);
            var context = new ToolContext(
                Session,
                () => viewportSize,
                screen => Session.Camera.ScreenToWorld(screen, viewportSize),
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
            CreateUnitSquare(Session.Document);
        }

        public CadSession Session { get; }

        public MoveTool MoveTool { get; }

        public StatusBar StatusBar { get; }

        public void SetOrtho(bool enabled)
            => Session.LineToolOptions.OrthoEnabled = enabled;

        public void BeginMove(PointF basePoint)
        {
            Session.Camera.ZoomAt(new Point(400, 300), 100, new Size(800, 600));
            SelectBottomEdge();
            MoveTool.OnMouseDown(CreateMouseButton(MouseButton.Left), basePoint);
        }

        public void Dispose()
            => _window.Close();

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
    }
}
