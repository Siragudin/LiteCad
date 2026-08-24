using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using LiteCad.UI.Layout;
using System.Globalization;
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
    public void Arc_AfterSecondClick_ShowsArcHeightInput()
    {
        RunSta(() =>
        {
            var harness = CreateInteractiveHarness();
            try
            {
                harness.FirstClick(new PointF(0, 0));
                harness.SecondClick(new PointF(100, 0));

                Assert.Equal(LineInputLabelMode.ArcHeight, harness.StatusBar.LineInputLabelMode);
            }
            finally
            {
                harness.Dispose();
            }
        });
    }

    [Fact]
    public void Arc_HeightInput_CommitsOpenArc()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            var geometry = CreateMinorArcGeometry(90, 100);
            var edges = CommitArcWithHeight(harness, geometry);

            Assert.Equal(9, edges);
            TopologyValidator.AssertValid(harness.Session.Document, 1e-4);
        });
    }

    [Fact]
    public void Arc_TrueRadius_MatchesCircleThroughArcPoints()
    {
        RunSta(() =>
        {
            var geometry = CreateMinorArcGeometry(90, 100);
            var height = GetArcHeight(geometry);
            Assert.True(ArcGeometry.TryComputeRadiusFromSignedSagitta(
                MathUtils.Distance(geometry.Start, geometry.End),
                height,
                out var radius));
            Assert.Equal(100, radius, 1);
        });
    }

    [Fact]
    public void Arc_SagittaDistance_MatchesPreviewHeight()
    {
        RunSta(() =>
        {
            var geometry = CreateMinorArcGeometry(90, 100);
            var height = GetArcHeight(geometry);
            Assert.True(ArcGeometry.TryComputeRadiusFromSignedSagitta(
                MathUtils.Distance(geometry.Start, geometry.End),
                height,
                out var radius));
            Assert.True(ArcGeometry.TryCreateBendPoint(geometry.Start, geometry.End, height, out var bendPoint));
            Assert.True(ArcGeometry.TryBuildArc(
                geometry.Start,
                geometry.End,
                radius,
                bendPoint,
                out var center,
                out var startAngle,
                out var sweep));

            var arcMidpoint = SectorGeometry.PointOnArc(center, radius, startAngle + sweep * Math.PI / 360.0);
            var chordMidpoint = MathUtils.Midpoint(geometry.Start, geometry.End);
            var measuredHeight = MathUtils.Distance(arcMidpoint, chordMidpoint);
            Assert.Equal(Math.Abs(height), measuredHeight, 1);
        });
    }

    [Fact]
    public void Arc_31Degrees_CreatesFourEdges()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            var geometry = CreateMinorArcGeometry(31, 200);
            Assert.Equal(4, CommitArcWithHeight(harness, geometry));
        });
    }

    [Fact]
    public void Arc_90Degrees_CreatesNineEdges()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            var geometry = CreateMinorArcGeometry(90, 100);
            Assert.Equal(9, CommitArcWithHeight(harness, geometry));
        });
    }

    [Fact]
    public void Arc_180Degrees_CreatesEighteenEdges()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            var geometry = CreateMinorArcGeometry(180, 100);
            Assert.Equal(18, CommitArcWithHeight(harness, geometry));
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
            Assert.Equal(36, CommitArcWithHeight(harness, geometry));
        });
    }

    [Fact]
    public void Arc_EnterCommit_CreatesArc()
    {
        RunSta(() =>
        {
            var harness = CreateInteractiveHarness();
            try
            {
                var geometry = CreateMinorArcGeometry(90, 100);
                var height = Math.Abs(GetArcHeight(geometry));

                harness.FirstClick(geometry.Start);
                harness.SecondClick(geometry.End);
                harness.Move(geometry.Cursor);
                harness.StatusBar.SetLineInputText(height.ToString(CultureInfo.InvariantCulture));

                Assert.True(LinearInputCommit.TryCommitLength(
                    harness.Tool,
                    height.ToString(CultureInfo.InvariantCulture),
                    LinearDisplayUnit.Millimeters,
                    LineInputLabelMode.ArcHeight));
                Assert.Equal(9, harness.Session.Document.Edges.Count);
                Assert.Equal(string.Empty, harness.StatusBar.LineInputText);
            }
            finally
            {
                harness.Dispose();
            }
        });
    }

    [Fact]
    public void Arc_ExactInputMillimeters_UpdatesPreviewWithoutCommit()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            var geometry = CreateMinorArcGeometry(90, 100);
            var height = Math.Abs(GetArcHeight(geometry));

            harness.FirstClick(geometry.Start);
            harness.SecondClick(geometry.End);
            harness.Move(geometry.Cursor);

            Assert.True(harness.Tool.TryApplyLengthFromInput(height.ToString(CultureInfo.InvariantCulture), commit: false));
            Assert.Empty(harness.Session.Document.Edges);
            Assert.NotNull(harness.LastDisplayedLength());
            Assert.Equal(height, harness.LastDisplayedLength()!.Value, 1);
        });
    }

    [Fact]
    public void Arc_ExactInputMeters_CommitsCorrectGeometry()
    {
        RunSta(() =>
        {
            var harness = CreateInteractiveHarness();
            try
            {
                var geometry = CreateMinorArcGeometry(90, 100);
                var heightMillimeters = Math.Abs(GetArcHeight(geometry));
                harness.Session.DisplayUnitSettings.LinearUnit = LinearDisplayUnit.Meters;

                harness.FirstClick(geometry.Start);
                harness.SecondClick(geometry.End);
                harness.Move(geometry.Cursor);

                var heightMeters = heightMillimeters / 1000.0;
                Assert.True(LinearInputCommit.TryCommitLength(
                    harness.Tool,
                    heightMeters.ToString(CultureInfo.InvariantCulture),
                    LinearDisplayUnit.Meters,
                    LineInputLabelMode.ArcHeight));
                Assert.Equal(9, harness.Session.Document.Edges.Count);
            }
            finally
            {
                harness.Dispose();
            }
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
    public void Arc_InvalidHeightInput_IsPreserved()
    {
        RunSta(() =>
        {
            var harness = CreateInteractiveHarness();
            try
            {
                var geometry = CreateMinorArcGeometry(90, 100);

                harness.FirstClick(geometry.Start);
                harness.SecondClick(geometry.End);
                harness.Move(geometry.Cursor);
                harness.StatusBar.SetLineInputText("0");

                Assert.False(LinearInputCommit.TryCommitLength(
                    harness.Tool,
                    "0",
                    LinearDisplayUnit.Millimeters,
                    LineInputLabelMode.ArcHeight));
                Assert.Equal("0", harness.StatusBar.LineInputText);
                Assert.Empty(harness.Session.Document.Edges);
            }
            finally
            {
                harness.Dispose();
            }
        });
    }

    private static int CommitArcWithHeight(ArcToolHarness harness, ArcTestGeometry geometry)
    {
        harness.FirstClick(geometry.Start);
        harness.SecondClick(geometry.End);
        harness.Move(geometry.Cursor);
        var height = GetArcHeight(geometry);
        Assert.True(ArcGeometry.TryComputeRadiusFromSignedSagitta(
            MathUtils.Distance(geometry.Start, geometry.End),
            height,
            out var radius));
        Assert.True(ArcGeometry.TryCreateBendPoint(geometry.Start, geometry.End, height, out var bendPoint));
        Assert.True(ArcGeometry.TryBuildArc(geometry.Start, geometry.End, radius, bendPoint, out _, out _, out var sweep));
        Assert.Equal(geometry.ExpectedSweepDegrees, Math.Abs(sweep), 1.0);
        Assert.True(harness.Tool.TryApplyLength(Math.Abs(height)));
        return harness.Session.Document.Edges.Count;
    }

    private static double GetArcHeight(ArcTestGeometry geometry)
    {
        Assert.True(ArcGeometry.TryGetSignedSagitta(geometry.Start, geometry.End, geometry.Cursor, out var signedSagitta));
        return signedSagitta;
    }

    private static ArcTestGeometry CreateMinorArcGeometry(double sweepDegrees, double radius)
    {
        var halfAngleRadians = sweepDegrees * Math.PI / 360.0;
        var chord = 2 * radius * Math.Sin(halfAngleRadians);
        var start = new PointF(0, 0);
        var end = new PointF((float)chord, 0);
        var sag = radius - Math.Sqrt(Math.Max(0, radius * radius - (chord / 2) * (chord / 2)));
        Assert.True(ArcGeometry.TryCreateBendPoint(start, end, sag, out var cursor));

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

    private readonly record struct ArcTestGeometry(
        PointF Start,
        PointF End,
        PointF Cursor,
        double ExpectedSweepDegrees);

    private sealed class ArcToolHarness
    {
        private double? _lastLength;

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
                () => { },
                setLength: length => _lastLength = length,
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

        public double? LastDisplayedLength() => _lastLength;
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

        public ArcTool Tool { get; }

        public StatusBar StatusBar { get; }

        public void FirstClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void SecondClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void Move(PointF world) => Tool.OnMouseMove(CreateMouseMove(), world);

        public void Dispose() => _window.Close();
    }
}