using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class RectangleToolTests
{
    private const double Tol = 1e-4;
    private const double UiTolerance = 12.0;

    [Fact]
    public void EmptyDocument_CreateRectangle_CreatesFourVerticesFourEdgesAndOneFace()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.SecondClick(new PointF(100, 80));

            Assert.Equal(4, harness.Session.Document.Vertices.Count);
            Assert.Equal(4, harness.Session.Document.Edges.Count);
            Assert.Single(harness.Session.Document.Polygons);
            Assert.Equal(PolygonType.Face, harness.Session.Document.Polygons[0].Type);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void RectangleCornersUseCanonicalVertices()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            AddSquare(harness.Session.Document, 0, 0, 100);
            var existingCorner = harness.Session.Document.Vertices
                .Single(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(100, 100), Tol));
            var vertexCountBefore = harness.Session.Document.Vertices.Count;

            harness.FirstClick(new PointF(100, 100));
            harness.SecondClick(new PointF(200, 180));

            Assert.Contains(harness.Session.Document.Vertices, vertex => vertex.Id == existingCorner.Id);
            Assert.Equal(
                1,
                harness.Session.Document.Vertices.Count(vertex =>
                    MathUtils.ArePointsEqual(vertex.Position, new PointF(100, 100), Tol)));
            Assert.Equal(vertexCountBefore + 3, harness.Session.Document.Vertices.Count);
        });
    }

    [Fact]
    public void RectanglePreviewDoesNotMutateDocument()
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
    public void Rectangle_MouseFlow()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.Move(new PointF(50, 40));
            harness.SecondClick(new PointF(100, 80));

            Assert.Equal(4, harness.Session.Document.Edges.Count);
            Assert.Single(harness.Session.Document.Polygons);
        });
    }

    [Theory]
    [InlineData(100, 80)]
    [InlineData(-100, 80)]
    [InlineData(100, -80)]
    [InlineData(-100, -80)]
    public void Rectangle_DragNegativeXAndY(double width, double height)
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            var first = new PointF(100, 80);
            var second = new PointF(first.X + width, first.Y + height);

            harness.FirstClick(first);
            harness.SecondClick(second);

            Assert.Equal(Math.Abs(width), GetSpan(harness.Session.Document, axisX: true), 3);
            Assert.Equal(Math.Abs(height), GetSpan(harness.Session.Document, axisX: false), 3);
            Assert.Contains(
                harness.Session.Document.Vertices,
                vertex => MathUtils.ArePointsEqual(vertex.Position, first, Tol));
        });
    }

    [Fact]
    public void Rectangle_ExactSizeInput()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            Assert.True(harness.Tool.TryApplyRectangleSize("4000", "3000"));

            Assert.Equal(4, harness.Session.Document.Vertices.Count);
            Assert.Equal(4000, GetSpan(harness.Session.Document, axisX: true), 3);
            Assert.Equal(3000, GetSpan(harness.Session.Document, axisX: false), 3);
        });
    }

    [Fact]
    public void Rectangle_ExactSizeInput_WithSpaces()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            Assert.True(harness.Tool.TryApplyRectangleSize("4000", "3000"));
            Assert.Equal(4000, GetSpan(harness.Session.Document, axisX: true), 3);
            Assert.Equal(3000, GetSpan(harness.Session.Document, axisX: false), 3);
        });
    }

    [Fact]
    public void Rectangle_ExactSizeInput_Comma()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            Assert.True(harness.Tool.TryApplyLengthInput("4000,3000"));
            Assert.Equal(4000, GetSpan(harness.Session.Document, axisX: true), 3);
            Assert.Equal(3000, GetSpan(harness.Session.Document, axisX: false), 3);
        });
    }

    [Fact]
    public void Rectangle_SizeInputCreatesCorrectArea()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            Assert.True(harness.Tool.TryApplyLengthInput("4000x3000"));

            var width = GetSpan(harness.Session.Document, axisX: true);
            var height = GetSpan(harness.Session.Document, axisX: false);
            Assert.Equal(12_000_000, width * height, 3);
        });
    }

    [Fact]
    public void Rectangle_UndoIsSingleOperation()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.SecondClick(new PointF(100, 80));

            Assert.Equal(1, harness.UndoRecords);
            Assert.True(harness.Session.History.Undo(harness.Session.Document, UiTolerance));
            Assert.Empty(harness.Session.Document.Edges);
            Assert.Empty(harness.Session.Document.Polygons);
            Assert.Empty(harness.Session.Document.Vertices);
        });
    }

    [Fact]
    public void Rectangle_RedoRestores()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.SecondClick(new PointF(100, 80));

            Assert.True(harness.Session.History.Undo(harness.Session.Document, UiTolerance));
            Assert.True(harness.Session.History.Redo(harness.Session.Document, UiTolerance));

            Assert.Equal(4, harness.Session.Document.Edges.Count);
            Assert.Single(harness.Session.Document.Polygons);
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void Rectangle_SnapToExistingCorner()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            AddSquare(harness.Session.Document, 0, 0, 100);
            var corner = harness.Session.Document.Vertices
                .Single(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(100, 100), Tol));

            harness.FirstClick(new PointF(100, 100));
            harness.SecondClick(new PointF(200, 180));

            Assert.Contains(harness.Session.Document.Vertices, vertex => vertex.Id == corner.Id);
        });
    }

    [Fact]
    public void Rectangle_AfterExistingGeometry()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            AddSquare(harness.Session.Document, 0, 0, 100);
            var vertexCountBefore = harness.Session.Document.Vertices.Count;

            harness.FirstClick(new PointF(200, 0));
            harness.SecondClick(new PointF(300, 100));

            Assert.Equal(vertexCountBefore + 4, harness.Session.Document.Vertices.Count);
            Assert.Equal(4 + 4, harness.Session.Document.Edges.Count);
        });
    }

    [Fact]
    public void Rectangle_WithDiagonal()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.SecondClick(new PointF(100, 100));
            TestDocumentHelpers.AddEdge(harness.Session.Document, new PointF(0, 0), new PointF(100, 100), Tol);
            PolygonBuilder.SyncFaces(harness.Session.Document, Tol);

            Assert.Equal(2, harness.Session.Document.Polygons.Count(polygon => polygon.Type == PolygonType.Face));
            TopologyValidator.AssertValid(harness.Session.Document, Tol);
        });
    }

    [Fact]
    public void Rectangle_DeleteEdge()
    {
        RunSta(() =>
        {
            var harness = CreateHarness();
            harness.FirstClick(new PointF(0, 0));
            harness.SecondClick(new PointF(100, 100));

            var edge = harness.Session.Document.Edges[0];
            TopologyService.DeleteEdge(harness.Session.Document, edge);
            TopologyService.PruneUnusedVertices(harness.Session.Document);
            PolygonBuilder.InvalidateSuppressionOnEdgeTopologyChange(harness.Session.Document);
            PolygonBuilder.SyncFaces(harness.Session.Document, Tol);

            Assert.Empty(harness.Session.Document.Polygons);
        });
    }

    [Theory]
    [InlineData("4000 x 3000", 4000, 3000)]
    [InlineData("4000x3000", 4000, 3000)]
    [InlineData("4000 × 3000", 4000, 3000)]
    [InlineData("4000,3000", 4000, 3000)]
    public void RectangleSizeInputParser_ParsesFormats(string input, double width, double height)
    {
        Assert.True(RectangleSizeInputParser.TryParse(input, out var parsedWidth, out var parsedHeight));
        Assert.Equal(width, parsedWidth, 3);
        Assert.Equal(height, parsedHeight, 3);
    }

    private static RectangleToolHarness CreateHarness() => new();

    private static void AddSquare(CadDocument document, float originX, float originY, float size)
    {
        TestDocumentHelpers.AddEdge(document, new PointF(originX, originY), new PointF(originX + size, originY), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(originX + size, originY), new PointF(originX + size, originY + size), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(originX + size, originY + size), new PointF(originX, originY + size), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(originX, originY + size), new PointF(originX, originY), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
    }

    private static double GetSpan(CadDocument document, bool axisX)
    {
        var values = document.Vertices
            .Select(vertex => axisX ? vertex.Position.X : vertex.Position.Y)
            .ToArray();
        return values.Max() - values.Min();
    }

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

    private static MouseButtonEventArgs CreateMouseDown()
        => new(Mouse.PrimaryDevice, 0, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent
        };

    private static MouseEventArgs CreateMouseMove()
        => new(Mouse.PrimaryDevice, 0)
        {
            RoutedEvent = UIElement.MouseMoveEvent
        };

    private sealed class RectangleToolHarness
    {
        public RectangleToolHarness()
        {
            Session = new CadSession();
            Tool = new RectangleTool();
            ToolService = Session.ToolService;

            var context = new ToolContext(
                Session,
                () => new Size(800, 600),
                screen => Session.Camera.ScreenToWorld(screen, new Size(800, 600)),
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                setRectangleSizeInputEnabled: _ => { },
                setRectangleSizePreview: (_, _) => { },
                resetRectangleSizeInput: (_, _) => { },
                processRectangleSizeKey: _ => false,
                recordUndo: () =>
                {
                    UndoRecords++;
                    Session.History.Record(Session.Document);
                });

            ToolService.Initialize(context);
            ToolService.ActivateTool(Tool);
        }

        public CadSession Session { get; }

        public RectangleTool Tool { get; }

        public ToolService ToolService { get; }

        public int UndoRecords { get; private set; }

        public void FirstClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void SecondClick(PointF world) => Tool.OnMouseDown(CreateMouseDown(), world);

        public void Move(PointF world) => Tool.OnMouseMove(CreateMouseMove(), world);

        public bool ApplyRectangleSize(string width, string height) => Tool.TryApplyRectangleSize(width, height);
    }
}
