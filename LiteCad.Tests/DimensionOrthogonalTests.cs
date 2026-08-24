using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using LiteCad.UI.Layout;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class DimensionOrthogonalTests
{
    private const double Tol = 1e-4;

    private static readonly PointF DiagonalStart = new(0, 0);
    private static readonly PointF DiagonalEnd = new(100, 50);

    [Fact]
    public void OrthoOff_DiagonalLayout_BehaviorUnchanged()
    {
        Assert.True(DimensionGeometry.TryCreateLayout(
            new PointF(0, 0),
            new PointF(30, 40),
            10,
            Tol,
            out var layout));

        Assert.Equal(50, layout.MeasuredDistance, 3);
        Assert.False(IsAxisAligned(layout));
    }

    [Fact]
    public void OrthoOn_DiagonalAnchors_DimensionLineIsAxisAligned()
    {
        Assert.True(DimensionGeometry.TryCreateLayout(
            DiagonalStart,
            DiagonalEnd,
            55,
            Tol,
            out var layout,
            isOrthogonal: true,
            orthogonalIsHorizontal: true));

        Assert.True(IsAxisAligned(layout));
        Assert.Equal(100, layout.MeasuredDistance, 3);
        Assert.Equal(layout.DimensionLineStart.Y, layout.DimensionLineEnd.Y, 3);
    }

    [Fact]
    public void OrthoOn_HorizontalLayout_MeasuresHorizontalDistance()
    {
        Assert.True(DimensionGeometry.TryCreateLayout(
            DiagonalStart,
            DiagonalEnd,
            55,
            Tol,
            out var layout,
            isOrthogonal: true,
            orthogonalIsHorizontal: true));

        Assert.Equal(100, layout.MeasuredDistance, 3);
        Assert.True(MathUtils.ArePointsEqual(new PointF(0, 80), layout.FirstExtensionEnd, Tol));
        Assert.True(MathUtils.ArePointsEqual(new PointF(100, 80), layout.SecondExtensionEnd, Tol));
        Assert.True(MathUtils.ArePointsEqual(new PointF(0, 0), layout.FirstAnchor, Tol));
        Assert.True(MathUtils.ArePointsEqual(new PointF(100, 50), layout.SecondAnchor, Tol));
    }

    [Fact]
    public void OrthoOn_VerticalLayout_MeasuresVerticalDistance()
    {
        Assert.True(DimensionGeometry.TryCreateLayout(
            DiagonalStart,
            DiagonalEnd,
            100,
            Tol,
            out var layout,
            isOrthogonal: true,
            orthogonalIsHorizontal: false));

        Assert.Equal(50, layout.MeasuredDistance, 3);
        Assert.Equal(layout.DimensionLineStart.X, layout.DimensionLineEnd.X, 3);
        Assert.True(MathUtils.ArePointsEqual(new PointF(150, 0), layout.FirstExtensionEnd, Tol));
        Assert.True(MathUtils.ArePointsEqual(new PointF(150, 50), layout.SecondExtensionEnd, Tol));
    }

    [Fact]
    public void OrthoOrientation_IsResolvedFromCursor()
    {
        Assert.True(DimensionGeometry.ResolveOrthogonalIsHorizontal(
            DiagonalStart,
            DiagonalEnd,
            new PointF(50, 100)));

        Assert.False(DimensionGeometry.ResolveOrthogonalIsHorizontal(
            DiagonalStart,
            DiagonalEnd,
            new PointF(150, 25)));
    }

    [Fact]
    public void OrthoToggle_DoesNotCreateOrChangeEdgesOrVertices()
    {
        var session = new CadSession();
        var document = session.Document;
        var edge = TestDocumentHelpers.AddEdge(document, DiagonalStart, DiagonalEnd, Tol);
        var vertexCountBefore = document.Vertices.Count;
        var edgeCountBefore = document.Edges.Count;

        session.DimensionToolOptions.OrthoEnabled = true;
        var dimension = DimensionService.Create(
            document,
            edge.StartVertexId,
            edge.EndVertexId,
            55,
            Tol);
        dimension.IsOrthogonal = true;
        dimension.OrthogonalIsHorizontal = true;

        Assert.Equal(vertexCountBefore, document.Vertices.Count);
        Assert.Equal(edgeCountBefore, document.Edges.Count);
        Assert.True(dimension.IsOrthogonal);
        Assert.Equal(100, DimensionService.GetMeasuredDistance(document, dimension), 3);
    }

    [Fact]
    public void CreateDimension_WithOrthoEnabled_StoresOrthogonalFlags()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateDimension();
            host.SetDimensionOrthoChecked(true);
            host.CreateDiagonalDimension(DiagonalStart, DiagonalEnd, new PointF(50, 100), 55);

            var dimension = host.Session.Document.Dimensions.Single();
            Assert.True(dimension.IsOrthogonal);
            Assert.True(dimension.OrthogonalIsHorizontal);
            Assert.Equal(100, DimensionService.GetMeasuredDistance(host.Session.Document, dimension), 3);
        });
    }

    [Fact]
    public void DimensionProperties_ShowOrthoToggleByDefaultOff()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateDimension();

            Assert.False(host.PropertiesPanel.DimensionOrthoIsChecked);
            Assert.False(host.Session.DimensionToolOptions.OrthoEnabled);
        });
    }

    private static bool IsAxisAligned(DimensionLayout layout)
    {
        var horizontal = Math.Abs(layout.DimensionLineStart.Y - layout.DimensionLineEnd.Y) <= Tol;
        var vertical = Math.Abs(layout.DimensionLineStart.X - layout.DimensionLineEnd.X) <= Tol;
        return horizontal || vertical;
    }

    private static void RunSta(Action action) => WpfTestUtilities.RunSta(action);

    private static DimensionOrthogonalHost CreateHost()
        => new();

    private static MouseButtonEventArgs CreateMouseButton(MouseButton button, bool isUp = false)
        => new(Mouse.PrimaryDevice, 0, button)
        {
            RoutedEvent = isUp ? UIElement.MouseLeftButtonUpEvent : UIElement.MouseLeftButtonDownEvent
        };

    private sealed class DimensionOrthogonalHost : IDisposable
    {
        public DimensionOrthogonalHost()
        {
            Session = new CadSession();
            ToolService = Session.ToolService;
            DimensionTool = new DimensionTool();
            PropertiesPanel = new PropertiesPanel();

            ToolService.Initialize(new ToolContext(
                Session,
                () => new Size(800, 600),
                _ => PointF.Zero,
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                () => { },
                recordUndo: () => Session.History.Record(Session.Document)));

            PropertiesPanel.BindSession(Session);
        }

        public CadSession Session { get; }

        public ToolService ToolService { get; }

        public DimensionTool DimensionTool { get; }

        public PropertiesPanel PropertiesPanel { get; }

        public void ActivateDimension()
        {
            ToolService.ActivateTool(DimensionTool);
            PropertiesPanel.SetActiveTool(ToolId.Dimension);
        }

        public void SetDimensionOrthoChecked(bool enabled)
            => PropertiesPanel.SetDimensionOrthoChecked(enabled);

        public void CreateDiagonalDimension(PointF start, PointF end, PointF offsetCursor, double offset)
        {
            TestDocumentHelpers.AddEdge(Session.Document, start, end, Tol);
            DimensionTool.OnMouseDown(CreateMouseButton(MouseButton.Left), start);
            DimensionTool.OnMouseDown(CreateMouseButton(MouseButton.Left), end);
            DimensionTool.OnMouseMove(CreateMouseEvent(), offsetCursor);
            DimensionTool.TryApplyLength(offset);
        }

        public void Dispose()
        {
        }
    }

    private static MouseEventArgs CreateMouseEvent()
        => new(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseMoveEvent };
}
