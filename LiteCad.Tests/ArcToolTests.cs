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

public class ArcToolTests
{
    private const double Tol = 1e-3;

    [Fact]
    public void Arc_TwoPoints_ShowsChordPreviewWithoutMutatingDocument()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.Move(new PointF(100, 0));

            Assert.Empty(harness.Session.Document.Edges);
            Assert.Empty(harness.Session.Document.Vertices);
        });
    }

    [Fact]
    public void Arc_AfterSecondClick_ShowsRadiusInput()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.SecondClick(new PointF(100, 0));

            Assert.Equal(LineInputLabelMode.Radius, harness.StatusBar.LineInputLabelMode);
        });
    }

    [Fact]
    public void Arc_RadiusInput_CommitsOpenArc()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            var geometry = CreateMinorArcGeometry(90, 100);
            var edges = CommitArcWithRadius(harness, geometry, 100);

            Assert.Equal(9, edges);
            TopologyValidator.AssertValid(harness.Session.Document, 1e-4);
        });
    }

    [Fact]
    public void Arc_31Degrees_CreatesFourEdges()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            var geometry = CreateMinorArcGeometry(31, 200);
            Assert.Equal(4, CommitArcWithRadius(harness, geometry, 200));
        });
    }

    [Fact]
    public void Arc_90Degrees_CreatesNineEdges()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            var geometry = CreateMinorArcGeometry(90, 100);
            Assert.Equal(9, CommitArcWithRadius(harness, geometry, 100));
        });
    }

    [Fact]
    public void Arc_180Degrees_CreatesEighteenEdges()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            var geometry = CreateMinorArcGeometry(180, 100);
            Assert.Equal(18, CommitArcWithRadius(harness, geometry, 100));
        });
    }

    [Fact]
    public void Arc_360Degrees_CreatesThirtySixEdges()
    {
        RunSta(() =>
        {
            Assert.Equal(36, ArcGeometry.ComputeOpenArcEdgeCount(360));

            var harness = CreateHarness();
            var geometry = CreateMajorArcGeometry(360, 1000);
            Assert.Equal(36, CommitArcWithRadius(harness, geometry, 1000));
        });
    }

    [Fact]
    public void Arc_EnterCommit_CreatesArc()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            var geometry = CreateMinorArcGeometry(90, 100);

            harness.FirstClick(geometry.Start);
            harness.SecondClick(geometry.End);
            harness.Move(geometry.Cursor);
            harness.StatusBar.SetLineInputText("100");

            Assert.True(harness.Tool.TryApplyLengthInput("100"));
            Assert.Equal(9, harness.Session.Document.Edges.Count);
            Assert.Equal(string.Empty, harness.StatusBar.LineInputText);
        });
    }

    [Fact]
    public void Arc_PreviewDoesNotMutateDocument()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            var geometry = CreateMinorArcGeometry(90, 100);

            harness.FirstClick(geometry.Start);
            harness.SecondClick(geometry.End);
            harness.Move(geometry.Cursor);

            Assert.Empty(harness.Session.Document.Edges);
            Assert.Empty(harness.Session.Document.Vertices);
            Assert.Empty(harness.Session.Document.Polygons);
        });
    }

    [Fact]
    public void Arc_InvalidRadiusInput_IsPreserved()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            var geometry = CreateMinorArcGeometry(90, 100);

            harness.FirstClick(geometry.Start);
            harness.SecondClick(geometry.End);
            harness.Move(geometry.Cursor);
            harness.StatusBar.SetLineInputText("5");

            Assert.False(harness.Tool.TryApplyLengthInput("5"));
            Assert.Equal("5", harness.StatusBar.LineInputText);
            Assert.Empty(harness.Session.Document.Edges);
        });
    }

    private static int CommitArcWithRadius(ArcToolHarness harness, ArcTestGeometry geometry, double radius)
    {
        harness.FirstClick(geometry.Start);
        harness.SecondClick(geometry.End);
        harness.Move(geometry.Cursor);
        Assert.True(ArcGeometry.TryBuildArc(geometry.Start, geometry.End, radius, geometry.Cursor, out _, out _, out var sweep));
        Assert.Equal(geometry.ExpectedSweepDegrees, Math.Abs(sweep), 1.0);
        Assert.True(harness.Tool.TryApplyLengthInput(radius.ToString(CultureInfo.InvariantCulture)));
        return harness.Session.Document.Edges.Count;
    }

    private static ArcTestGeometry CreateMinorArcGeometry(double sweepDegrees, double radius)
    {
        var halfAngleRadians = sweepDegrees * Math.PI / 360.0;
        var chord = 2 * radius * Math.Sin(halfAngleRadians);
        var start = new PointF(0, 0);
        var end = new PointF((float)chord, 0);
        var sag = radius - Math.Sqrt(Math.Max(0, radius * radius - (chord / 2) * (chord / 2)));
        var cursor = new PointF((float)(chord / 2), (float)(sag + 10));

        Assert.True(ArcGeometry.TryBuildArc(start, end, radius, cursor, out _, out _, out var builtSweep));
        Assert.Equal(sweepDegrees, Math.Abs(builtSweep), 1.0);

        return new ArcTestGeometry(start, end, cursor, Math.Abs(builtSweep));
    }

    private static ArcTestGeometry CreateMajorArcGeometry(double sweepDegrees, double radius)
    {
        var minorSweep = 360 - sweepDegrees;
        if (minorSweep < 1)
        {
            minorSweep = 1;
        }

        var geometry = CreateMinorArcGeometry(minorSweep, radius);
        Assert.True(ArcGeometry.TryBuildArc(
            geometry.Start,
            geometry.End,
            radius,
            geometry.Cursor,
            out var center,
            out var startAngle,
            out var minorBuiltSweep));

        var majorSweepRadians = minorBuiltSweep > 0
            ? minorBuiltSweep * Math.PI / 180.0 - 2 * Math.PI
            : minorBuiltSweep * Math.PI / 180.0 + 2 * Math.PI;
        var majorMidpoint = SectorGeometry.PointOnArc(center, radius, startAngle + majorSweepRadians / 2);
        var cursor = new PointF(majorMidpoint.X, majorMidpoint.Y);

        Assert.True(ArcGeometry.TryBuildArc(geometry.Start, geometry.End, radius, cursor, out _, out _, out var builtSweep));
        Assert.InRange(Math.Abs(builtSweep), sweepDegrees - 2, sweepDegrees + 0.5);

        return new ArcTestGeometry(geometry.Start, geometry.End, cursor, Math.Abs(builtSweep));
    }

    private static ArcToolHarness CreateHarness() => new();

    private static ArcInteractiveHarness CreateInteractiveHarness() => new();

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

    private readonly record struct ArcTestGeometry(
        PointF Start,
        PointF End,
        PointF Cursor,
        double ExpectedSweepDegrees);

    private sealed class ArcToolHarness
    {
        public ArcToolHarness()
        {
            Session = new CadSession();
            Tool = new ArcTool();
            ToolService = Session.ToolService;

            var context = new ToolContext(
                Session,
                () => new Size(800, 600),
                screen => Session.Camera.ScreenToWorld(screen, new Size(800, 600)),
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                setLength: _ => { },
                setLengthInputEnabled: _ => { },
                resetLengthInput: _ => { },
                processLengthKey: _ => false,
                setLineInputModeEnabled: (_, _) => { },
                getLineInputText: () => string.Empty,
                recordUndo: () => Session.History.Record(Session.Document));

            ToolService.Initialize(context);
            ToolService.ActivateTool(Tool);
        }

        public CadSession Session { get; }

        public ArcTool Tool { get; }

        public ToolService ToolService { get; }

        public void FirstClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void SecondClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void Move(PointF world) => Tool.OnMouseMove(CreateMouseMove(), world);
    }

    private sealed class ArcInteractiveHarness : IDisposable
    {
        private readonly Window _window;

        public ArcInteractiveHarness()
        {
            Session = new CadSession();
            Tool = new ArcTool();
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
                () => new Size(800, 600),
                screen => Session.Camera.ScreenToWorld(screen, new Size(800, 600)),
                _ => new Point(0, 0),
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

            StatusBar.TryCommitLengthInput = input =>
            {
                if (Tool.TryApplyLengthInput(input))
                {
                    return true;
                }

                return double.TryParse(
                           input.Replace(',', '.'),
                           NumberStyles.Float,
                           CultureInfo.InvariantCulture,
                           out var value)
                       && Tool.TryApplyLength(value);
            };
        }

        public CadSession Session { get; }

        public ArcTool Tool { get; }

        public StatusBar StatusBar { get; }

        public void FirstClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void SecondClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void Move(PointF world) => Tool.OnMouseMove(CreateMouseMove(), world);

        public void Dispose() => _window.Close();
    }
}
