using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
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

public class CircleToolTests
{
    private const double Tol = 1e-4;
    private const double UiTolerance = 12.0;

    [Fact]
    public void Circle_Creates36Vertices()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            CreateCircleByMouse(harness, new PointF(0, 0), new PointF(100, 0));
            Assert.Equal(36, harness.Session.Document.Vertices.Count);
        });
    }

    [Fact]
    public void Circle_Creates36Edges()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            CreateCircleByMouse(harness, new PointF(0, 0), new PointF(100, 0));
            Assert.Equal(36, harness.Session.Document.Edges.Count);
        });
    }

    [Fact]
    public void Circle_ClosesContour()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            CreateCircleByMouse(harness, new PointF(0, 0), new PointF(100, 0));
            var polygon = Assert.Single(harness.Session.Document.Polygons);
            Assert.Equal(36, polygon.OuterLoop.Edges.Count);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void Circle_CreatesFace()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            CreateCircleByMouse(harness, new PointF(0, 0), new PointF(100, 0));
            Assert.Single(harness.Session.Document.Polygons);
            Assert.Equal(PolygonType.Face, harness.Session.Document.Polygons[0].Type);
        });
    }

    [Fact]
    public void Circle_Radius100()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            CreateCircleByMouse(harness, new PointF(0, 0), new PointF(100, 0));
            AssertAllVerticesAtRadius(harness.Session.Document, new PointF(0, 0), 100, Tol);
            Assert.Contains(
                harness.Session.Document.Vertices,
                vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(100, 0), Tol));
        });
    }

    [Fact]
    public void Circle_Radius1000()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            CreateCircleByMouse(harness, new PointF(50, 50), new PointF(1050, 50));
            AssertAllVerticesAtRadius(harness.Session.Document, new PointF(50, 50), 1000, Tol);
        });
    }

    [Fact]
    public void Circle_AlwaysUses36Segments()
    {
        RunSta(() =>
        {
            var small = CreateHarness();
            CreateCircleByMouse(small, new PointF(0, 0), new PointF(10, 0));
            Assert.Equal(36, small.Session.Document.Edges.Count);

            var large = CreateHarness();
            CreateCircleByMouse(large, new PointF(0, 0), new PointF(500, 0));
            Assert.Equal(36, large.Session.Document.Edges.Count);
        });
    }

    [Fact]
    public void Circle_VertexAnglesAre10Degrees()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            CreateCircleByMouse(harness, new PointF(0, 0), new PointF(200, 0));

            var angles = harness.Session.Document.Vertices
                .Select(vertex => NormalizeDegrees(Math.Atan2(vertex.Position.Y, vertex.Position.X) * 180.0 / Math.PI))
                .OrderBy(angle => angle)
                .ToArray();

            Assert.Equal(36, angles.Length);
            for (var i = 0; i < angles.Length; i++)
            {
                var expected = i * CircleGeometry.DegreesPerSegment;
                Assert.Equal(expected, angles[i], 2);
            }
        });
    }

    [Fact]
    public void Circle_PreviewDoesNotMutateDocument()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(10, 10));
            harness.Move(new PointF(200, 150));

            Assert.Empty(harness.Session.Document.Edges);
            Assert.Empty(harness.Session.Document.Polygons);
            Assert.Empty(harness.Session.Document.Vertices);
        });
    }

    [Fact]
    public void Circle_ExactRadiusInput()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.Move(new PointF(100, 0));

            Assert.True(harness.ApplyRadius("1000"));
            Assert.Equal(36, harness.Session.Document.Edges.Count);
            AssertAllVerticesAtRadius(harness.Session.Document, new PointF(0, 0), 1000, Tol);
        });
    }

    [Fact]
    public void Circle_EnterCommitsImmediately()
    {
        RunSta(() =>
        {
            using var host = CreateInteractiveHarness();
            host.FirstClick(new PointF(0, 0));
            host.Move(new PointF(100, 0));
            host.TypeRadius("750");
            host.PressEnter();

            Assert.Equal(36, host.Session.Document.Edges.Count);
            AssertAllVerticesAtRadius(host.Session.Document, new PointF(0, 0), 750, Tol);
        });
    }

    [Fact]
    public void Circle_InputClearsAfterCommit()
    {
        RunSta(() =>
        {
            using var host = CreateInteractiveHarness();
            host.FirstClick(new PointF(0, 0));
            host.Move(new PointF(100, 0));
            host.TypeRadius("500");
            host.PressEnter();

            Assert.Equal(string.Empty, host.StatusBar.LineInputText);
        });
    }

    [Fact]
    public void Circle_InvalidInputPreserved()
    {
        RunSta(() =>
        {
            using var host = CreateInteractiveHarness();
            host.FirstClick(new PointF(0, 0));
            host.Move(new PointF(100, 0));
            host.TypeRadius("abc");
            host.PressEnter();

            Assert.Empty(host.Session.Document.Edges);
            Assert.Equal("abc", host.StatusBar.LineInputText);
        });
    }

    [Fact]
    public void Circle_UndoRedo()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            CreateCircleByMouse(harness, new PointF(0, 0), new PointF(100, 0));
            Assert.Equal(36, harness.Session.Document.Edges.Count);
            Assert.Single(harness.Session.Document.Polygons);

            Assert.True(harness.Session.History.Undo(harness.Session.Document, UiTolerance));
            Assert.Empty(harness.Session.Document.Edges);
            Assert.Empty(harness.Session.Document.Vertices);
            Assert.Empty(harness.Session.Document.Polygons);

            Assert.True(harness.Session.History.Redo(harness.Session.Document, UiTolerance));
            Assert.Equal(36, harness.Session.Document.Edges.Count);
            Assert.Single(harness.Session.Document.Polygons);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    private static void CreateCircleByMouse(CircleToolHarness harness, PointF center, PointF radiusPoint)
    {
        harness.FirstClick(center);
        harness.Move(radiusPoint);
        harness.SecondClick(radiusPoint);
    }

    private static void AssertAllVerticesAtRadius(CadDocument document, PointF center, double radius, double tolerance)
    {
        foreach (var vertex in document.Vertices)
        {
            var distance = MathUtils.Distance(center, vertex.Position);
            Assert.Equal(radius, distance, tolerance);
        }
    }

    private static double NormalizeDegrees(double angle)
    {
        var normalized = angle % 360.0;
        if (normalized < 0)
        {
            normalized += 360.0;
        }

        return normalized;
    }

    private static CircleToolHarness CreateHarness() => new();

    private static CircleInteractiveHarness CreateInteractiveHarness() => new();

    private static void RunSta(Action action) => WpfTestUtilities.RunSta(action);

    private static MouseButtonEventArgs CreateMouseDown(MouseButton button = MouseButton.Left)
        => new(Mouse.PrimaryDevice, 0, button)
        {
            RoutedEvent = button == MouseButton.Left
                ? UIElement.MouseLeftButtonDownEvent
                : UIElement.MouseRightButtonDownEvent
        };

    private static MouseEventArgs CreateMouseMove()
        => new(Mouse.PrimaryDevice, 0)
        {
            RoutedEvent = UIElement.MouseMoveEvent
        };

    private static KeyEventArgs CreateEnterKey()
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, Keyboard.PrimaryDevice.ActiveSource!, 0, Key.Enter)
        {
            RoutedEvent = UIElement.KeyDownEvent
        };
        return args;
    }

    private sealed class CircleToolHarness
    {
        public CircleToolHarness()
        {
            Session = new CadSession();
            Tool = new CircleTool();
            ToolService = Session.ToolService;

            var context = new ToolContext(
                Session,
                () => new Size(800, 600),
                screen => Session.Camera.ScreenToWorld(screen, new Size(800, 600)),
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                () => { },
                setLength: _ => { },
                setLengthInputEnabled: _ => { },
                resetLengthInput: _ => { },
                processLengthKey: _ => false,
                setLineInputModeEnabled: (_, _) => { },
                recordUndo: () => Session.History.Record(Session.Document));

            ToolService.Initialize(context);
            ToolService.ActivateTool(Tool);
        }

        public CadSession Session { get; }

        public CircleTool Tool { get; }

        public ToolService ToolService { get; }

        public void FirstClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void SecondClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void Move(PointF world) => Tool.OnMouseMove(CreateMouseMove(), world);

        public bool ApplyRadius(string radius)
            => Tool.TryApplyLengthInput(radius)
               || (double.TryParse(radius.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                   && Tool.TryApplyLength(value));
    }

    private sealed class CircleInteractiveHarness : IDisposable
    {
        private readonly Window _window;

        public CircleInteractiveHarness()
        {
            Session = new CadSession();
            Tool = new CircleTool();
            StatusBar = new StatusBar();
            _window = WpfTestUtilities.CreateHiddenWindow(StatusBar);

            var context = new ToolContext(
                Session,
                () => new Size(800, 600),
                screen => Session.Camera.ScreenToWorld(screen, new Size(800, 600)),
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                () => { },
                setLengthInputEnabled: enabled => StatusBar.SetLengthInputEnabled(enabled),
                resetLengthInput: StatusBar.ResetLengthEditing,
                processLengthKey: StatusBar.ProcessLengthKey,
                setLineInputModeEnabled: StatusBar.SetLengthInputEnabled,
                getLineInputText: () => StatusBar.LineInputText,
                setLineInputText: StatusBar.SetLineInputText,
                recordUndo: () => Session.History.Record(Session.Document));

            Session.ToolService.Initialize(context);
            Session.ToolService.ActivateTool(Tool);

            TestLinearInputCommit.WireStatusBar(Session, StatusBar, Tool);
        }

        public CadSession Session { get; }

        public CircleTool Tool { get; }

        public StatusBar StatusBar { get; }

        public void FirstClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void Move(PointF world) => Tool.OnMouseMove(CreateMouseMove(), world);

        public void TypeRadius(string text) => StatusBar.SetLineInputText(text);

        public void PressEnter() => StatusBar.ProcessLengthKey(WpfTestUtilities.CreateKeyDown(StatusBar, Key.Enter));

        public void Dispose() => _window.Close();
    }
}
