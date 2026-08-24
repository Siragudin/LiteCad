using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class SelectionToolClickTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void ClickNearVertex_DoesNotSelectVertex()
    {
        RunSta(session =>
        {
            TestDocumentHelpers.AddEdge(session.Document, new PointF(0, 0), new PointF(5, 0), Tol);

            Click(session, new PointF(0, 1));

            Assert.Empty(session.Selection.SelectedVertexIds);
        });
    }

    [Fact]
    public void ClickOnVertexEndpoint_SelectsEdgeNotVertex()
    {
        RunSta(session =>
        {
            var edge = TestDocumentHelpers.AddEdge(session.Document, new PointF(0, 0), new PointF(5, 0), Tol);

            Click(session, new PointF(0, 0));

            Assert.Single(session.Selection.SelectedEdgeIds);
            Assert.Equal(edge.Id, session.Selection.SelectedEdgeIds.First());
            Assert.Empty(session.Selection.SelectedVertexIds);
        });
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    public void ClickOnShortEdgeMidpoint_SelectsEdge(double lengthMm)
    {
        RunSta(session =>
        {
            var edge = TestDocumentHelpers.AddEdge(
                session.Document,
                new PointF(100, 100),
                new PointF(100 + (float)lengthMm, 100),
                Tol);

            Click(session, new PointF(100 + (float)(lengthMm / 2), 100));

            Assert.Single(session.Selection.SelectedEdgeIds);
            Assert.Equal(edge.Id, session.Selection.SelectedEdgeIds.First());
            Assert.Empty(session.Selection.SelectedVertexIds);
        });
    }

    [Fact]
    public void LineToolSnap_ToVertexEndpoint_StillWorks()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        var start = TopologyService.GetEdgeStartPoint(document, document.Edges[0]);
        var cursor = new PointF(start.X + 2, start.Y + 2);

        var snapService = new SnapService();
        var snap = snapService.FindBestSnap(document, cursor, MathUtils.SnapToleranceWorld(1), includeOnEdge: true);

        Assert.True(snap.HasSnap);
        Assert.Equal(SnapKind.Endpoint, snap.Snap!.Value.Kind);
        Assert.Equal(start.X, snap.Snap.Value.Position.X, 3);
        Assert.Equal(start.Y, snap.Snap.Value.Position.Y, 3);
    }

    [Fact]
    public void ClickOnAxis_SelectsAxis()
    {
        RunSta(session =>
        {
            var axis = AxisService.Create(session.Document, new PointF(0, 50), new PointF(100, 50), Tol)!;

            Click(session, new PointF(50, 50));

            Assert.Single(session.Selection.SelectedAxisIds);
            Assert.Equal(axis.Id, session.Selection.SelectedAxisIds.First());
            Assert.Empty(session.Selection.SelectedVertexIds);
        });
    }

    [Fact]
    public void ClickOnDimension_SelectsDimension()
    {
        RunSta(session =>
        {
            var edge = TestDocumentHelpers.AddEdge(session.Document, new PointF(0, 0), new PointF(100, 0), Tol);
            DimensionService.Create(session.Document, edge.StartVertexId, edge.EndVertexId, 10, Tol);

            Click(session, new PointF(50, 10));

            Assert.Single(session.Selection.SelectedDimensionIds);
            Assert.Empty(session.Selection.SelectedEdgeIds);
            Assert.Empty(session.Selection.SelectedVertexIds);
        });
    }

    [Fact]
    public void ClickInsideFace_SelectsPolygon()
    {
        RunSta(session =>
        {
            CreateUnitSquare(session.Document);
            var face = session.Document.Polygons.First(polygon => polygon.Type == PolygonType.Face);

            Click(session, new PointF(50, 50));

            Assert.Single(session.Selection.SelectedPolygonIds);
            Assert.Equal(face.Id, session.Selection.SelectedPolygonIds.First());
            Assert.Empty(session.Selection.SelectedVertexIds);
        });
    }

    private static void CreateUnitSquare(CadDocument document)
    {
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(100, 100), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 100), new PointF(0, 100), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 100), new PointF(0, 0), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
    }

    private static void Click(CadSession session, PointF world)
    {
        var harness = new SelectionToolHarness(session);
        harness.ToolService.ActivateTool(harness.SelectionTool);
        harness.SelectionTool.OnMouseDown(CreateMouseButton(MouseButton.Left), world);
        harness.SelectionTool.OnMouseUp(CreateMouseButton(MouseButton.Left, isUp: true), world);
    }

    private static void RunSta(Action<CadSession> action)
    {
        WpfTestUtilities.RunSta(() =>
        {
            var session = new CadSession();
            session.Camera.Reset();
            action(session);
        });
    }

    private static MouseButtonEventArgs CreateMouseButton(MouseButton button, bool isUp = false)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, button)
        {
            RoutedEvent = isUp ? UIElement.MouseLeftButtonUpEvent : UIElement.MouseLeftButtonDownEvent
        };
        return args;
    }

    private sealed class SelectionToolHarness
    {
        public SelectionToolHarness(CadSession session)
        {
            Session = session;
            ToolService = session.ToolService;
            SelectionTool = new SelectionTool();
            ToolService.Initialize(new ToolContext(
                Session,
                () => new Size(800, 600),
                _ => PointF.Zero,
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                () => { }));
        }

        public CadSession Session { get; }

        public ToolService ToolService { get; }

        public SelectionTool SelectionTool { get; }
    }
}
