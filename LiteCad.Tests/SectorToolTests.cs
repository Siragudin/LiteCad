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

public class SectorToolTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void Sector_90Degrees_CreatesElevenEdges()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            CreateSectorByMouse(harness, new PointF(0, 0), new PointF(100, 0), new PointF(0, 100));
            Assert.Equal(11, harness.Session.Document.Edges.Count);
            Assert.Single(harness.Session.Document.Polygons);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void Sector_31Degrees_CreatesSixEdges()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.SecondClick(new PointF(100, 0));
            harness.ApplyAngle(31);
            Assert.Equal(6, harness.Session.Document.Edges.Count);
        });
    }

    [Fact]
    public void Sector_360Degrees_CreatesCircleWithoutRadialEdges()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.SecondClick(new PointF(100, 0));
            harness.ApplyAngle(360);

            Assert.Equal(36, harness.Session.Document.Edges.Count);
            Assert.DoesNotContain(
                harness.Session.Document.Edges,
                edge => EdgeTouchesCenter(edge, harness.Session.Document, new PointF(0, 0)));
            Assert.Single(harness.Session.Document.Polygons);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void Sector_AfterFirstClick_ShowsRadiusInput()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            harness.FirstClick(new PointF(0, 0));

            Assert.Equal(LineInputLabelMode.Radius, harness.StatusBar.LineInputLabelMode);
        });
    }

    [Fact]
    public void Sector_AfterSecondClick_ShowsAngleInput()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.SecondClick(new PointF(100, 0));

            Assert.Equal(LineInputLabelMode.Angle, harness.StatusBar.LineInputLabelMode);
        });
    }

    [Fact]
    public void Sector_AngleEnter_CommitsSector()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.SecondClick(new PointF(100, 0));
            harness.Move(new PointF(0, 100));

            Assert.True(harness.Tool.TryApplyLengthInput("90"));
            Assert.Equal(11, harness.Session.Document.Edges.Count);
            Assert.Equal(LineInputLabelMode.Length, harness.StatusBar.LineInputLabelMode);
        });
    }

    [Fact]
    public void Sector_AngleEnterWithoutTyping_CommitsCurrentPreview()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.SecondClick(new PointF(100, 0));
            harness.Move(new PointF(0, 100));
            harness.ThirdClick(new PointF(0, 100));

            Assert.Equal(11, harness.Session.Document.Edges.Count);
        });
    }

    [Fact]
    public void Sector_ExactRadiusInput_FixesStartPoint()
    {
        RunSta(() =>
        {
            using var harness = CreateInteractiveHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.Move(new PointF(0, 100));

            Assert.True(harness.Tool.TryApplyLengthInput("100"));
            Assert.Equal(LineInputLabelMode.Angle, harness.StatusBar.LineInputLabelMode);
        });
    }

    [Theory]
    [InlineData(360, 36, false)]
    [InlineData(361, 36, false)]
    [InlineData(720, 36, false)]
    [InlineData(1000, 36, false)]
    public void Sector_ExactAngleAbove360_ClampedToSingleFullCircle(int inputAngle, int expectedEdges, bool hasRadialEdges)
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            ApplyExactAngleInput(harness, inputAngle);

            Assert.Equal(expectedEdges, harness.Session.Document.Edges.Count);
            Assert.Equal(
                hasRadialEdges,
                harness.Session.Document.Edges.Any(edge => EdgeTouchesCenter(edge, harness.Session.Document, new PointF(0, 0))));
            Assert.Single(harness.Session.Document.Polygons);
        });
    }

    [Fact]
    public void Sector_ExactAngle360_Applies360()
    {
        Assert.Equal(360, SectorTool.NormalizeExactInputAngleDegrees(360));
    }

    [Fact]
    public void Sector_ExactAngle361_Applies360()
    {
        Assert.Equal(360, SectorTool.NormalizeExactInputAngleDegrees(361));
    }

    [Fact]
    public void Sector_ExactAngle720_Applies360()
    {
        Assert.Equal(360, SectorTool.NormalizeExactInputAngleDegrees(720));
    }

    [Fact]
    public void Sector_ExactAngle1000_Applies360()
    {
        Assert.Equal(360, SectorTool.NormalizeExactInputAngleDegrees(1000));
    }

    [Fact]
    public void Sector_ExactAngle270_Applies270()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            ApplyExactAngleInput(harness, 270);
            Assert.Equal(29, harness.Session.Document.Edges.Count);
            Assert.Equal(270, SectorTool.NormalizeExactInputAngleDegrees(270));
        });
    }

    [Fact]
    public void Sector_ExactAngle31_Applies31()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            ApplyExactAngleInput(harness, 31);
            Assert.Equal(6, harness.Session.Document.Edges.Count);
            Assert.Equal(31, SectorTool.NormalizeExactInputAngleDegrees(31));
        });
    }

    [Fact]
    public void Sector_ExactInputAbove360_CreatesOnlyOneFullCircle()
    {
        RunSta(() =>
        {
            var harness360 = CreateHarness();
            ApplyExactAngleInput(harness360, 360);

            var harness720 = CreateHarness();
            ApplyExactAngleInput(harness720, 720);

            Assert.Equal(harness360.Session.Document.Edges.Count, harness720.Session.Document.Edges.Count);
            Assert.Equal(36, harness720.Session.Document.Edges.Count);
        });
    }

    [Fact]
    public void Sector_ExactInputAbove360_NoDuplicateSecondRevolution()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            ApplyExactAngleInput(harness, 720);

            Assert.Equal(36, harness.Session.Document.Edges.Count);
            Assert.NotEqual(72, harness.Session.Document.Edges.Count);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    private static void ApplyExactAngleInput(SectorToolHarness harness, double angleDegrees)
    {
        harness.FirstClick(new PointF(0, 0));
        harness.SecondClick(new PointF(100, 0));
        Assert.True(harness.Tool.TryApplyLengthInput(angleDegrees.ToString(CultureInfo.InvariantCulture)));
    }

    private static bool EdgeTouchesCenter(Edge edge, CadDocument document, PointF center)
    {
        var start = TopologyService.GetVertexPosition(document, edge.StartVertexId);
        var end = TopologyService.GetVertexPosition(document, edge.EndVertexId);
        return MathUtils.ArePointsEqual(start, center, Tol) || MathUtils.ArePointsEqual(end, center, Tol);
    }

    private static void CreateSectorByMouse(SectorToolHarness harness, PointF center, PointF start, PointF end)
    {
        harness.FirstClick(center);
        harness.SecondClick(start);
        harness.Move(end);
        harness.ThirdClick(end);
    }

    private static SectorToolHarness CreateHarness() => new();

    private static SectorInteractiveHarness CreateInteractiveHarness() => new();

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

    private sealed class SectorToolHarness
    {
        public SectorToolHarness()
        {
            Session = new CadSession();
            Tool = new SectorTool();
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
                getLineInputText: () => string.Empty,
                recordUndo: () => Session.History.Record(Session.Document));

            ToolService.Initialize(context);
            ToolService.ActivateTool(Tool);
        }

        public CadSession Session { get; }

        public SectorTool Tool { get; }

        public ToolService ToolService { get; }

        public void FirstClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void SecondClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void ThirdClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void Move(PointF world) => Tool.OnMouseMove(CreateMouseMove(), world);

        public void ApplyAngle(double angle) => Tool.TryApplyLength(angle);
    }

    private sealed class SectorInteractiveHarness : IDisposable
    {
        private readonly Window _window;

        public SectorInteractiveHarness()
        {
            Session = new CadSession();
            Tool = new SectorTool();
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

        public SectorTool Tool { get; }

        public StatusBar StatusBar { get; }

        public void FirstClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void SecondClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void Move(PointF world) => Tool.OnMouseMove(CreateMouseMove(), world);

        public void Dispose() => _window.Close();
    }
}
