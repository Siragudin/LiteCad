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

public class MirrorPropertiesOrthoTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void MirrorProperties_ContainOrthoToggle()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateMirror();

            Assert.True(host.PropertiesPanel.IsMirrorToolPanelVisible);
            Assert.True(host.PropertiesPanel.IsMirrorOrthoToggleVisible);
        });
    }

    [Fact]
    public void MirrorOrthoToggle_ChangesMirrorOrthoEnabled()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateMirror();

            host.PropertiesPanel.SetMirrorOrthoChecked(true);
            Assert.True(host.Session.MirrorToolOptions.OrthoEnabled);
            Assert.True(host.PropertiesPanel.MirrorOrthoIsChecked);

            host.PropertiesPanel.SetMirrorOrthoChecked(false);
            Assert.False(host.Session.MirrorToolOptions.OrthoEnabled);
            Assert.False(host.PropertiesPanel.MirrorOrthoIsChecked);
        });
    }

    [Fact]
    public void MirrorOrthoToggle_DoesNotChangeGlobalOrtho()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.Session.LineToolOptions.OrthoEnabled = false;
            host.ActivateMirror();

            host.PropertiesPanel.SetMirrorOrthoChecked(true);

            Assert.True(host.Session.MirrorToolOptions.OrthoEnabled);
            Assert.False(host.Session.LineToolOptions.OrthoEnabled);
        });
    }

    [Fact]
    public void MirrorOrthoToggle_RestoresStateWhenReturningToMirror()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateMirror();
            host.PropertiesPanel.SetMirrorOrthoChecked(true);

            host.ActivateMove();
            host.ActivateMirror();

            Assert.True(host.PropertiesPanel.MirrorOrthoIsChecked);
            Assert.True(host.Session.MirrorToolOptions.OrthoEnabled);
        });
    }

    [Fact]
    public void MirrorOrthoToggle_DuringActiveMirror_UpdatesPreviewWithoutCommit()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateMirror();
            host.SelectSingleEdge();
            host.PropertiesPanel.SetMirrorOrthoChecked(false);
            host.MirrorTool.OnMouseDown(CreateMouseButton(MouseButton.Left), new PointF(0, 0));
            var fingerprintBefore = DocumentFingerprint(host.Session.Document);

            host.MirrorTool.OnMouseMove(CreateMouseEvent(), new PointF(3, 4));
            Assert.True(MathUtils.ArePointsEqual(host.MirrorTool.PreviewAxisEnd, new PointF(3, 4), Tol));

            host.PropertiesPanel.SetMirrorOrthoChecked(true);

            Assert.Equal(fingerprintBefore, DocumentFingerprint(host.Session.Document));
            Assert.True(MathUtils.ArePointsEqual(host.MirrorTool.PreviewAxisEnd, new PointF(0, 4), Tol));
        });
    }

    [Fact]
    public void MirrorOrthoToggle_HiddenForMoveTool()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateMove();

            Assert.False(host.PropertiesPanel.IsMirrorToolPanelVisible);
        });
    }

    private static MirrorPropertiesOrthoTestHost CreateHost() => new();

    private static string DocumentFingerprint(CadDocument document)
        => string.Join("|", document.Edges.Count, document.Edges.Select(edge => edge.Id));

    private static void RunSta(Action action) => WpfTestUtilities.RunSta(action);

    private static MouseButtonEventArgs CreateMouseButton(MouseButton button)
        => new(Mouse.PrimaryDevice, 0, button)
        {
            RoutedEvent = button == MouseButton.Left
                ? UIElement.MouseLeftButtonDownEvent
                : UIElement.MouseRightButtonDownEvent
        };

    private static MouseEventArgs CreateMouseEvent()
        => new(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseMoveEvent };

    private sealed class MirrorPropertiesOrthoTestHost : IDisposable
    {
        private readonly Window _window;

        public MirrorPropertiesOrthoTestHost()
        {
            Session = new CadSession();
            MirrorTool = new MirrorTool();
            MoveTool = new MoveTool();
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

            PropertiesPanel.BindSession(
                Session,
                onMirrorOrthoChanged: () =>
                {
                    if (Session.ToolService.ActiveTool is MirrorTool mirrorTool)
                    {
                        mirrorTool.NotifyOrthoChanged();
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
                () => { });

            Session.ToolService.Initialize(context);
            TestDocumentHelpers.AddEdge(Session.Document, new PointF(0, 1), new PointF(2, 3), Tol);
            Session.Camera.ZoomAt(new Point(400, 300), 100, ViewportSize);
        }

        public CadSession Session { get; }

        public MirrorTool MirrorTool { get; }

        public MoveTool MoveTool { get; }

        public PropertiesPanel PropertiesPanel { get; }

        public Size ViewportSize { get; } = new(800, 600);

        public void ActivateMirror()
        {
            Session.ToolService.ActivateTool(MirrorTool);
            PropertiesPanel.SetActiveTool(ToolId.Mirror);
        }

        public void ActivateMove()
        {
            Session.ToolService.ActivateTool(MoveTool);
            PropertiesPanel.SetActiveTool(ToolId.Move);
        }

        public void SelectSingleEdge()
        {
            Session.Selection.Clear();
            Session.Selection.SelectedEdgeIds.Add(Session.Document.Edges[0].Id);
        }

        public void Dispose()
        {
            _window.Close();
        }
    }
}
