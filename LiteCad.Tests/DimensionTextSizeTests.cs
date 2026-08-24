using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Services;
using LiteCad.UI;
using LiteCad.UI.Layout;
using LiteCad.Tools;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace LiteCad.Tests;

public class DimensionTextSizeTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void DefaultTextSize_IsEight()
    {
        Assert.Equal(8, Dimension.DefaultTextSize);
        Assert.Equal(8, new DimensionToolOptions().TextSize);
    }

    [Fact]
    public void CreateDimension_HasDefaultTextSize()
    {
        var document = new CadDocument();
        var edge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        var dimension = DimensionService.Create(document, edge.StartVertexId, edge.EndVertexId, 10, Tol);

        Assert.Equal(8, dimension.TextSize);
    }

    [Theory]
    [InlineData(5, 8)]
    [InlineData(0, 8)]
    [InlineData(-3, 8)]
    [InlineData(double.NaN, 8)]
    public void NormalizeTextSize_ClampsBelowMinimum(double input, double expected)
    {
        Assert.Equal(expected, Dimension.NormalizeTextSize(input));
    }

    [Fact]
    public void TrySetTextSize_ClampsFiveToEight()
    {
        var document = CreateDimensionDocument(out var dimensionId);

        Assert.True(DimensionService.TrySetTextSize(document, dimensionId, 5));

        Assert.Equal(8, document.Dimensions.Single().TextSize);
    }

    [Fact]
    public void TrySetTextSize_ClampsZeroToEight()
    {
        var document = CreateDimensionDocument(out var dimensionId);

        Assert.True(DimensionService.TrySetTextSize(document, dimensionId, 0));

        Assert.Equal(8, document.Dimensions.Single().TextSize);
    }

    [Fact]
    public void CreateDimension_UsesExplicitTextSize()
    {
        var document = new CadDocument();
        var edge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        var dimension = DimensionService.Create(document, edge.StartVertexId, edge.EndVertexId, 10, Tol, textSize: 12);

        Assert.Equal(12, dimension.TextSize);
    }

    [Fact]
    public void TrySetTextSize_AcceptsPositiveValue()
    {
        var document = CreateDimensionDocument(out var dimensionId);

        Assert.True(DimensionService.TrySetTextSize(document, dimensionId, 20));

        Assert.Equal(20, document.Dimensions.First(item => item.Id == dimensionId).TextSize);
    }

    [Fact]
    public void TwoDimensions_CanHaveDifferentTextSizes()
    {
        var document = new CadDocument();
        var firstEdge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        var secondEdge = TestDocumentHelpers.AddEdge(document, new PointF(0, 50), new PointF(100, 50), Tol);
        var first = DimensionService.Create(document, firstEdge.StartVertexId, firstEdge.EndVertexId, 10, Tol, textSize: 10);
        var second = DimensionService.Create(document, secondEdge.StartVertexId, secondEdge.EndVertexId, 10, Tol, textSize: 20);

        Assert.Equal(10, first.TextSize);
        Assert.Equal(20, second.TextSize);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(5.0)]
    [InlineData(50.0)]
    public void WorldTextSize_DoesNotDependOnZoom(double zoom)
    {
        var document = CreateDimensionDocument(out var dimensionId);
        DimensionService.TrySetTextSize(document, dimensionId, 14);
        var dimension = document.Dimensions.First(item => item.Id == dimensionId);

        Assert.Equal(14, DimensionAnnotationDrawing.GetWorldTextHeight(dimension));
        _ = zoom;
    }

    [Theory]
    [InlineData(8.0, 1.0, 8.0)]
    [InlineData(8.0, 2.0, 16.0)]
    [InlineData(8.0, 10.0, 80.0)]
    [InlineData(10.0, 2.0, 20.0)]
    public void TextAndGeometry_ScaleEquallyWithZoom(double textWorldHeight, double zoom, double expectedScreenExtent)
    {
        Assert.Equal(expectedScreenExtent, DimensionAnnotationDrawing.GetTextScreenExtent(textWorldHeight, zoom));

        var geometryScreenLength = 100.0 * zoom;
        var textScreenExtent = DimensionAnnotationDrawing.GetTextScreenExtent(textWorldHeight, zoom);
        var ratio = textScreenExtent / geometryScreenLength;
        Assert.Equal(textWorldHeight / 100.0, ratio, 6);
    }

    [Fact]
    public void Serializer_RoundTripsTextSize()
    {
        var original = CreateDimensionDocument(out var dimensionId);
        DimensionService.TrySetTextSize(original, dimensionId, 16);

        var json = ProjectDocumentSerializer.Serialize(original, LinearDisplayUnit.Millimeters);
        var dto = ProjectDocumentSerializer.Deserialize(json);
        var restored = new CadDocument();
        ProjectDocumentSerializer.Apply(restored, dto);

        var restoredDimension = restored.Dimensions.Single();
        Assert.Equal(16, restoredDimension.TextSize);
    }

    [Fact]
    public void Serializer_MissingTextSize_UsesDefaultEight()
    {
        const string json = """
            {
              "formatVersion": 1,
              "linearDisplayUnit": "Millimeters",
              "vertices": [
                { "id": "11111111-1111-1111-1111-111111111111", "x": 0, "y": 0 },
                { "id": "22222222-2222-2222-2222-222222222222", "x": 100, "y": 0 }
              ],
              "edges": [
                {
                  "id": "33333333-3333-3333-3333-333333333333",
                  "startVertexId": "11111111-1111-1111-1111-111111111111",
                  "endVertexId": "22222222-2222-2222-2222-222222222222"
                }
              ],
              "userPolygons": [],
              "dimensions": [
                {
                  "id": "44444444-4444-4444-4444-444444444444",
                  "firstVertexId": "11111111-1111-1111-1111-111111111111",
                  "secondVertexId": "22222222-2222-2222-2222-222222222222",
                  "firstAnchorX": 0,
                  "firstAnchorY": 0,
                  "secondAnchorX": 100,
                  "secondAnchorY": 0,
                  "offset": 10,
                  "extensionStyle": "Full",
                  "isOrthogonal": false,
                  "orthogonalIsHorizontal": false
                }
              ],
              "axes": [],
              "suppressedFaceGeometryKeys": [],
              "faceFillStyles": {}
            }
            """;

        var dto = ProjectDocumentSerializer.Deserialize(json);
        var document = new CadDocument();
        ProjectDocumentSerializer.Apply(document, dto);

        Assert.Equal(8, document.Dimensions.Single().TextSize);
    }

    [Fact]
    public void Serializer_LegacyTextSizeBelowMinimum_ClampsToEight()
    {
        const string json = """
            {
              "formatVersion": 1,
              "linearDisplayUnit": "Millimeters",
              "vertices": [
                { "id": "11111111-1111-1111-1111-111111111111", "x": 0, "y": 0 },
                { "id": "22222222-2222-2222-2222-222222222222", "x": 100, "y": 0 }
              ],
              "edges": [
                {
                  "id": "33333333-3333-3333-3333-333333333333",
                  "startVertexId": "11111111-1111-1111-1111-111111111111",
                  "endVertexId": "22222222-2222-2222-2222-222222222222"
                }
              ],
              "userPolygons": [],
              "dimensions": [
                {
                  "id": "44444444-4444-4444-4444-444444444444",
                  "firstVertexId": "11111111-1111-1111-1111-111111111111",
                  "secondVertexId": "22222222-2222-2222-2222-222222222222",
                  "firstAnchorX": 0,
                  "firstAnchorY": 0,
                  "secondAnchorX": 100,
                  "secondAnchorY": 0,
                  "offset": 10,
                  "extensionStyle": "Full",
                  "isOrthogonal": false,
                  "orthogonalIsHorizontal": false,
                  "textSize": 5
                }
              ],
              "axes": [],
              "suppressedFaceGeometryKeys": [],
              "faceFillStyles": {}
            }
            """;

        var dto = ProjectDocumentSerializer.Deserialize(json);
        var document = new CadDocument();
        ProjectDocumentSerializer.Apply(document, dto);

        Assert.Equal(8, document.Dimensions.Single().TextSize);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(20)]
    public void MeasureDistanceText_HasNonZeroSize(double textSize)
    {
        var size = DimensionAnnotationDrawing.MeasureDistanceText("100", textSize, zoom: 1.0);
        Assert.True(size.Width > 0);
        Assert.True(size.Height > 0);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    [InlineData(50.0)]
    public void DimensionTextSizeEight_RendersAtZoom(double zoom)
    {
        WpfTestUtilities.RunSta(() =>
        {
            var coloredPixels = RenderDimensionColoredPixels(textSize: 8, zoom);
            var minimumPixels = Math.Max(8, (int)(8 * zoom));
            Assert.True(coloredPixels > minimumPixels, $"Expected visible dimension text at zoom {zoom}, got {coloredPixels} colored pixels.");
        });
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void DimensionTextSize_RendersAtDefaultZoom(double textSize)
    {
        WpfTestUtilities.RunSta(() =>
        {
            var coloredPixels = RenderDimensionColoredPixels(textSize, zoom: 1.0);
            Assert.True(coloredPixels > 20, $"Expected visible dimension text for TextSize {textSize}, got {coloredPixels} colored pixels.");
        });
    }

    [Fact]
    public void InvalidTextSizeInput_IsRejectedByParser()
    {
        Assert.False(LinearInputParser.TryParse("abc", LinearDisplayUnit.Millimeters, false, false, out _));
    }

    [Fact]
    public void CreateDimension_UsesToolTextSize()
    {
        WpfTestUtilities.RunSta(() =>
        {
            using var host = CreatePropertiesHost();
            host.ActivateDimension();
            host.SetToolTextSize(10);
            host.CreateHorizontalDimension(new PointF(0, 0), new PointF(100, 0), 50);

            Assert.Equal(10, host.Session.Document.Dimensions.Single().TextSize);
        });
    }

    [Fact]
    public void DimensionToolTextSize_DoesNotChangeExistingDimensions()
    {
        WpfTestUtilities.RunSta(() =>
        {
            using var host = CreatePropertiesHost();
            var document = host.Session.Document;
            var edge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
            var dimension = DimensionService.Create(document, edge.StartVertexId, edge.EndVertexId, 50, Tol, textSize: 12);

            host.ActivateDimension();
            host.SetToolTextSize(20);

            Assert.Equal(12, dimension.TextSize);
        });
    }

    [Fact]
    public void SelectedDimension_ShowsOwnTextSize()
    {
        WpfTestUtilities.RunSta(() =>
        {
            using var host = CreatePropertiesHost();
            var document = host.Session.Document;
            var edge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
            var dimension = DimensionService.Create(document, edge.StartVertexId, edge.EndVertexId, 50, Tol, textSize: 12);
            host.Session.Selection.SelectedDimensionIds.Add(dimension.Id);

            host.ActivateSelection();
            host.PropertiesPanel.SyncDimensionSelection(host.Session);

            Assert.True(host.PropertiesPanel.IsDimensionSelectionPanelVisible);
            Assert.Equal("12", host.PropertiesPanel.DimensionTextSizeDisplayText);
        });
    }

    [Fact]
    public void ChangingSelectedDimensionTextSize_DoesNotAffectOthers()
    {
        WpfTestUtilities.RunSta(() =>
        {
            using var host = CreatePropertiesHost();
            var document = host.Session.Document;
            var firstEdge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
            var secondEdge = TestDocumentHelpers.AddEdge(document, new PointF(0, 50), new PointF(100, 50), Tol);
            var first = DimensionService.Create(document, firstEdge.StartVertexId, firstEdge.EndVertexId, 50, Tol, textSize: 8);
            var second = DimensionService.Create(document, secondEdge.StartVertexId, secondEdge.EndVertexId, 50, Tol, textSize: 8);
            host.Session.Selection.SelectedDimensionIds.Add(first.Id);

            host.ActivateSelection();
            host.PropertiesPanel.SetSelectedDimensionTextSize("10");
            host.PropertiesPanel.CommitSelectedDimensionTextSizeForTests();

            Assert.Equal(10, first.TextSize);
            Assert.Equal(8, second.TextSize);
        });
    }

    private static int RenderDimensionColoredPixels(double textSize, double zoom)
    {
        var document = new CadDocument();
        var edge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        var dimension = DimensionService.Create(document, edge.StartVertexId, edge.EndVertexId, 10, Tol, textSize: textSize);

        if (!DimensionGeometry.TryGetAnchorPoints(document, dimension, out var firstAnchor, out var secondAnchor)
            || !DimensionGeometry.TryCreateLayout(firstAnchor, secondAnchor, dimension, Tol, out var layout))
        {
            return 0;
        }

        var viewport = new Size(800, 600);
        var camera = new Camera();
        SetCameraZoom(camera, zoom, viewport);
        CenterWorldPoint(camera, new PointF(50, 5), viewport);

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            new Renderer().Render(
                context,
                document,
                new Selection(),
                camera,
                viewport,
                activeTool: null,
                LinearDisplayUnit.Millimeters);
        }

        var bitmap = new RenderTargetBitmap(
            (int)viewport.Width,
            (int)viewport.Height,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var textWorldHeight = Dimension.NormalizeTextSize(textSize);
        var textCenterWorld = GetTextWorldCenter(layout, textWorldHeight);
        var screenCenter = camera.WorldToScreen(textCenterWorld, viewport);
        var sampleRadius = Math.Max(24, textWorldHeight * zoom * 3);
        var localPixels = CountColoredPixelsNear(bitmap, screenCenter, sampleRadius);
        var totalPixels = CountColoredPixelsNear(bitmap, new Point(viewport.Width / 2, viewport.Height / 2), viewport.Width * 0.5);
        return Math.Max(localPixels, totalPixels);
    }

    private static PointF GetTextWorldCenter(DimensionLayout layout, double textWorldHeight)
    {
        var start = layout.DimensionLineStart;
        var end = layout.DimensionLineEnd;
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var centerX = (start.X + end.X) * 0.5;
        var centerY = (start.Y + end.Y) * 0.5;
        var textGapWorld = textWorldHeight * (12.0 / 18.0);
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length > 1e-9)
        {
            var nx = -dy / length;
            var ny = dx / length;
            if (ny > 0)
            {
                nx = -nx;
                ny = -ny;
            }

            centerX += nx * textGapWorld;
            centerY += ny * textGapWorld;
        }

        return new PointF((float)centerX, (float)centerY);
    }

    private static void SetCameraZoom(Camera camera, double zoom, Size viewport)
    {
        var factor = zoom / camera.Zoom;
        camera.ZoomAt(new Point(viewport.Width / 2, viewport.Height / 2), factor, viewport);
    }

    private static void CenterWorldPoint(Camera camera, PointF world, Size viewport)
    {
        var screen = camera.WorldToScreen(world, viewport);
        var target = new Point(viewport.Width / 2, viewport.Height / 2);
        camera.PanScreen(target.X - screen.X, target.Y - screen.Y);
    }

    private static int CountColoredPixelsNear(RenderTargetBitmap bitmap, Point center, double radius)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        var minX = Math.Max(0, (int)Math.Floor(center.X - radius));
        var maxX = Math.Min(bitmap.PixelWidth - 1, (int)Math.Ceiling(center.X + radius));
        var minY = Math.Max(0, (int)Math.Floor(center.Y - radius));
        var maxY = Math.Min(bitmap.PixelHeight - 1, (int)Math.Ceiling(center.Y + radius));
        var radiusSquared = radius * radius;
        var count = 0;

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var dx = x - center.X;
                var dy = y - center.Y;
                if (dx * dx + dy * dy > radiusSquared)
                {
                    continue;
                }

                var index = y * stride + x * 4;
                var blue = pixels[index];
                var green = pixels[index + 1];
                var red = pixels[index + 2];
                if (blue > 180 && green < 120 && red < 120)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static CadDocument CreateDimensionDocument(out Guid dimensionId)
    {
        var document = new CadDocument();
        var edge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        var dimension = DimensionService.Create(document, edge.StartVertexId, edge.EndVertexId, 10, Tol);
        dimensionId = dimension.Id;
        return document;
    }

    private static DimensionTextSizePropertiesHost CreatePropertiesHost()
        => new();

    private static MouseButtonEventArgs CreateMouseButton(MouseButton button, bool isUp = false)
        => new(Mouse.PrimaryDevice, 0, button)
        {
            RoutedEvent = isUp ? UIElement.MouseLeftButtonUpEvent : UIElement.MouseLeftButtonDownEvent
        };

    private sealed class DimensionTextSizePropertiesHost : IDisposable
    {
        public DimensionTextSizePropertiesHost()
        {
            Session = new CadSession();
            ToolService = Session.ToolService;
            DimensionTool = new DimensionTool();
            SelectionTool = new SelectionTool();
            PropertiesPanel = new PropertiesPanel();

            var context = new ToolContext(
                Session,
                () => ViewportSize,
                _ => PointF.Zero,
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                () => { },
                recordUndo: () => Session.History.Record(Session.Document));

            ToolService.Initialize(context);
            PropertiesPanel.BindSession(Session);
        }

        public CadSession Session { get; }

        public ToolService ToolService { get; }

        public DimensionTool DimensionTool { get; }

        public SelectionTool SelectionTool { get; }

        public PropertiesPanel PropertiesPanel { get; }

        public Size ViewportSize { get; } = new(800, 600);

        public void ActivateDimension()
        {
            ToolService.ActivateTool(DimensionTool);
            PropertiesPanel.SetActiveTool(ToolId.Dimension);
        }

        public void ActivateSelection()
        {
            ToolService.ActivateTool(SelectionTool);
            PropertiesPanel.SetActiveTool(ToolId.Selection);
        }

        public void SetToolTextSize(double textSize)
            => PropertiesPanel.SetDimensionToolTextSize(textSize);

        public void CreateHorizontalDimension(PointF start, PointF end, double offset)
        {
            TestDocumentHelpers.AddEdge(Session.Document, start, end, Tol);
            DimensionTool.OnMouseDown(CreateMouseButton(MouseButton.Left), start);
            DimensionTool.OnMouseDown(CreateMouseButton(MouseButton.Left), end);
            DimensionTool.TryApplyLength(offset);
        }

        public void Dispose()
        {
        }
    }
}
