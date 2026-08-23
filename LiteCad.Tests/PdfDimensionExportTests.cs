using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Rendering;
using LiteCad.Rendering.Pdf;
using LiteCad.Services;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class PdfDimensionExportTests : IDisposable
{
    private const double Tolerance = 1e-4;

    private readonly string _tempRoot;

    public PdfDimensionExportTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "LiteCadPdfDimensionTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Export_LargeDrawingWithDimensions_UsesReasonableStrokeWidths()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateLargeDrawingWithDimensions();
            var layout = PdfExportLayout.Create(document);
            Assert.True(layout.NeedsZoomCompensation);

            var pdfPath = Path.Combine(_tempRoot, "LargeDimensions.pdf");
            PdfExporter.Export(document, LinearDisplayUnit.Millimeters, new Renderer(), pdfPath);

            var strokeWidths = ExtractStrokeWidths(ReadPdfPageContent(pdfPath));
            Assert.NotEmpty(strokeWidths);
            Assert.All(strokeWidths, width => Assert.True(width <= 3.0, $"Unexpected PDF stroke width {width}."));
            Assert.DoesNotContain(strokeWidths, width => width >= 10.0);
        });
    }

    [Fact]
    public void Export_ComplexDrawing_FitsPageAndExportsPdf()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateComplexDrawing();
            var layout = PdfExportLayout.Create(document);
            layout.EnsureFitsPrintableArea();

            var pdfPath = Path.Combine(_tempRoot, "ComplexDrawing.pdf");
            PdfExporter.Export(document, LinearDisplayUnit.Millimeters, new Renderer(), pdfPath);

            Assert.True(File.Exists(pdfPath));
            using var pdf = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
            Assert.True(pdf.PageCount >= 1);

            var strokeWidths = ExtractStrokeWidths(ReadPdfPageContent(pdfPath));
            Assert.All(strokeWidths, width => Assert.True(width <= 3.0, $"Unexpected PDF stroke width {width}."));
        });
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(5_000, 500)]
    [InlineData(50_000, 5_000)]
    [InlineData(100_000, 100_000)]
    public void Export_DimensionsAtDifferentScales_UseReasonableStrokeWidths(double width, double height)
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateRectangleWithDimensions(width, height);
            var pdfPath = Path.Combine(_tempRoot, $"{width}x{height}.pdf");
            PdfExporter.Export(document, LinearDisplayUnit.Millimeters, new Renderer(), pdfPath);

            var strokeWidths = ExtractStrokeWidths(ReadPdfPageContent(pdfPath));
            Assert.NotEmpty(strokeWidths);
            Assert.All(strokeWidths, value => Assert.True(value <= 3.0, $"Stroke width {value} is too large for {width}x{height}."));
        });
    }

    [Fact]
    public void Export_VisualTree_DimensionPensAreScaledByTransformInPdf()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateLargeDrawingWithDimensions();
            var layout = PdfExportLayout.Create(document);
            var visual = PdfExporter.BuildExportVisual(new PdfSheet(document), LinearDisplayUnit.Millimeters, new Renderer(), layout);
            var drawing = VisualTreeHelper.GetDrawing(visual) as DrawingGroup;

            Assert.NotNull(drawing);
            var maxRecordedPen = FindMaxPenThickness(drawing!);
            Assert.True(maxRecordedPen > 10.0, "Expected large recorded WPF pen thickness for low export zoom.");

            var pdfPath = Path.Combine(_tempRoot, "ScaledDimensions.pdf");
            PdfExporter.Export(document, LinearDisplayUnit.Millimeters, new Renderer(), pdfPath);
            var strokeWidths = ExtractStrokeWidths(ReadPdfPageContent(pdfPath));
            Assert.All(strokeWidths, width => Assert.True(width < maxRecordedPen / 5.0));
        });
    }

    [Fact]
    public void Export_AxisAndDimensionScreenPens_UsePdfLineweightTableWidths()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateOrientedDimensionsDocument();
            AxisService.Create(document, new PointF(-200, 750), new PointF(2_200, 750), Tolerance);
            document.Edges[0].IsAxis = true;

            var pdfPath = Path.Combine(_tempRoot, "PhysicalLineweights.pdf");
            PdfExporter.Export(document, LinearDisplayUnit.Millimeters, new Renderer(), pdfPath);

            var strokeWidths = ExtractStrokeWidths(ReadPdfPageContent(pdfPath));
            Assert.NotEmpty(strokeWidths);

            AssertContainsPhysicalWidth(strokeWidths, PenStyle.Axis);
            AssertContainsPhysicalWidth(strokeWidths, PenStyle.AxisEdge);
            AssertContainsPhysicalWidth(strokeWidths, PenStyle.Dimension);
            AssertContainsPhysicalWidth(strokeWidths, PenStyle.Extension);

            var clampedDimensionPoints = PdfLineweightTable.MinVisiblePrintThicknessPoints;
            Assert.DoesNotContain(
                strokeWidths,
                width => Math.Abs(width - clampedDimensionPoints) < 0.01
                    && Math.Abs(width - PdfLineweightTable.ToPoints(PenStyle.Dimension)) > 0.01);
        });
    }

    private static void AssertContainsPhysicalWidth(List<double> strokeWidths, PenStyle style)
    {
        var points = PdfLineweightTable.ToPoints(style);
        var dip = points * PdfExportLayout.DipPerInch / PdfExportLayout.PointsPerInch;
        Assert.True(
            strokeWidths.Any(width => Math.Abs(width - points) < 0.02 || Math.Abs(width - dip) < 0.02),
            $"Missing {style} lineweight. expected {points:G6} pt or {dip:G6} DIP, actual [{string.Join(", ", strokeWidths.Select(width => width.ToString("G6")))}].");
    }

    private static double FindMaxPenThickness(DrawingGroup group)
    {
        var max = 0.0;
        foreach (var child in group.Children)
        {
            switch (child)
            {
                case GeometryDrawing { Pen: { } pen }:
                    max = Math.Max(max, pen.Thickness);
                    break;
                case DrawingGroup nested:
                    max = Math.Max(max, FindMaxPenThickness(nested));
                    break;
            }
        }

        return max;
    }

    private static List<double> ExtractStrokeWidths(string content)
    {
        return Regex.Matches(content, @"(?<![\d.])(\d+\.?\d*|\.\d+)\s+w")
            .Select(match => double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
    }

    private static string ReadPdfPageContent(string pdfPath)
    {
        using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        return ReadPdfPageContent(document.Pages[0]);
    }

    private static string ReadPdfPageContent(PdfPage page)
    {
        var builder = new StringBuilder();
        AppendPdfContentObject(builder, page.Elements.GetObject("/Contents"));
        return builder.ToString();
    }

    private static void AppendPdfContentObject(StringBuilder builder, PdfObject? contents)
    {
        if (contents is null)
        {
            return;
        }

        if (contents is PdfArray array)
        {
            foreach (var item in array.Elements)
            {
                if (item is PdfReference reference)
                {
                    AppendPdfContentObject(builder, reference.Value);
                }
            }

            return;
        }

        PdfDictionary? streamDictionary = contents switch
        {
            PdfDictionary dictionary => dictionary,
            _ when contents.Reference?.Value is PdfDictionary referenced => referenced,
            _ => null
        };

        if (streamDictionary?.Stream is not null)
        {
            builder.Append(Encoding.Latin1.GetString(streamDictionary.Stream.UnfilteredValue));
        }
    }

    [Fact]
    public void Export_DimensionText_IsAlignedWithDimensionLine()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateOrientedDimensionsDocument();
            var layout = PdfExportLayout.Create(document);
            AssertDimensionTextAlignment(document, layout);
        });
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(5_000, 500)]
    [InlineData(100_000, 100_000)]
    public void Export_DimensionText_StaysAlignedAtDifferentScales(double width, double height)
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateRectangleWithDimensions(width, height);
            var layout = PdfExportLayout.Create(document);
            AssertDimensionTextAlignment(document, layout);
        });
    }

    [Fact]
    public void Export_ShortExtensionDimensions_KeepTextAligned()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateOrientedDimensionsDocument();
            foreach (var dimension in document.Dimensions)
            {
                DimensionService.TrySetExtensionStyle(document, dimension.Id, DimensionExtensionStyle.Short);
            }

            var layout = PdfExportLayout.Create(document);
            AssertDimensionTextAlignment(document, layout);
        });
    }

    [Fact]
    public void Export_DimensionText_ContainsFormattedValues()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateOrientedDimensionsDocument();
            var pdfPath = Path.Combine(_tempRoot, "DimensionValues.pdf");
            PdfExporter.Export(document, LinearDisplayUnit.Millimeters, new Renderer(), pdfPath);

            var content = ReadPdfPageContent(pdfPath);
            Assert.Contains("2000", content);
            Assert.Contains("1500", content);
        });
    }

    private static void AssertDimensionTextAlignment(CadDocument document, PdfExportLayout layout)
    {
        const double textGapScreen = 12.0;
        var textScale = layout.NeedsZoomCompensation ? layout.ZoomCompensation : 1.0;
        var expectedGap = textGapScreen * textScale;

        foreach (var dimension in document.Dimensions)
        {
            if (!DimensionGeometry.TryGetAnchorPoints(document, dimension, out var firstAnchor, out var secondAnchor)
                || !DimensionGeometry.TryCreateLayout(
                    firstAnchor,
                    secondAnchor,
                    dimension,
                    TopologyTolerance.ForMutation,
                    out var dimensionLayout))
            {
                continue;
            }

            var lineStart = ToExportPoint(
                layout,
                layout.ExportCamera.WorldToScreen(dimensionLayout.DimensionLineStart, layout.ContentSizeDip));
            var lineEnd = ToExportPoint(
                layout,
                layout.ExportCamera.WorldToScreen(dimensionLayout.DimensionLineEnd, layout.ContentSizeDip));
            var textCenter = ComputeExpectedDimensionTextCenter(dimensionLayout, layout);

            var perpendicularDistance = PerpendicularDistanceToSegment(textCenter, lineStart, lineEnd, out var midpointOffset);
            var lineLength = Distance(lineStart, lineEnd);

            Assert.True(
                perpendicularDistance >= expectedGap * 0.5 && perpendicularDistance <= expectedGap + 18.0,
                $"Text is {perpendicularDistance:F2} DIP from dimension line, expected about {expectedGap:F2}.");
            Assert.True(
                Math.Abs(midpointOffset) * lineLength <= 4.0,
                $"Text is {Math.Abs(midpointOffset) * lineLength:F2} DIP away from the line midpoint along the dimension.");
        }
    }

    private static Point ComputeExpectedDimensionTextCenter(DimensionLayout layout, PdfExportLayout exportLayout)
    {
        var screenCenter = ComputeDimensionTextScreenCenter(
            layout,
            exportLayout.ExportCamera,
            exportLayout.ContentSizeDip);
        return ToExportPoint(exportLayout, screenCenter);
    }

    private static Point ToExportPoint(PdfExportLayout layout, Point contentPoint)
    {
        if (layout.NeedsZoomCompensation)
        {
            var centerX = layout.ContentSizeDip.Width / 2.0;
            var centerY = layout.ContentSizeDip.Height / 2.0;
            contentPoint = new Point(
                centerX + (contentPoint.X - centerX) * layout.ZoomCompensation,
                centerY + (contentPoint.Y - centerY) * layout.ZoomCompensation);
        }

        return new Point(
            layout.MarginDip + contentPoint.X,
            layout.MarginDip + contentPoint.Y);
    }

    private static double PerpendicularDistanceToSegment(
        Point point,
        Point segmentStart,
        Point segmentEnd,
        out double midpointParameterOffset)
    {
        var dx = segmentEnd.X - segmentStart.X;
        var dy = segmentEnd.Y - segmentStart.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 1e-9)
        {
            midpointParameterOffset = 0.0;
            return Distance(point, segmentStart);
        }

        var parameter = ((point.X - segmentStart.X) * dx + (point.Y - segmentStart.Y) * dy) / lengthSquared;
        midpointParameterOffset = parameter - 0.5;
        var projection = new Point(
            segmentStart.X + parameter * dx,
            segmentStart.Y + parameter * dy);
        return Distance(point, projection);
    }

    private static double Distance(Point left, Point right)
        => Math.Sqrt(Math.Pow(left.X - right.X, 2) + Math.Pow(left.Y - right.Y, 2));

    private static Point ComputeDimensionTextScreenCenter(
        DimensionLayout layout,
        Camera camera,
        Size viewport)
    {
        const double textGapScreen = 12.0;

        var startScreen = camera.WorldToScreen(layout.DimensionLineStart, viewport);
        var endScreen = camera.WorldToScreen(layout.DimensionLineEnd, viewport);
        var dx = endScreen.X - startScreen.X;
        var dy = endScreen.Y - startScreen.Y;

        var center = new Point(
            (startScreen.X + endScreen.X) * 0.5,
            (startScreen.Y + endScreen.Y) * 0.5);

        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 1e-9)
        {
            return center;
        }

        var nx = -dy / length;
        var ny = dx / length;
        if (ny > 0)
        {
            nx = -nx;
            ny = -ny;
        }

        return new Point(
            center.X + nx * textGapScreen,
            center.Y + ny * textGapScreen);
    }

    private static CadDocument CreateOrientedDimensionsDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(2_000, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(2_000, 0), new PointF(2_000, 1_500), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(2_000, 1_500), new PointF(0, 1_500), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 1_500), new PointF(0, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);

        var bottomLeft = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tolerance));
        var bottomRight = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(2_000, 0), Tolerance));
        var topRight = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(2_000, 1_500), Tolerance));
        var topLeft = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 1_500), Tolerance));

        DimensionService.Create(document, bottomLeft.Id, bottomRight.Id, 250, Tolerance);
        DimensionService.Create(document, bottomRight.Id, topRight.Id, 300, Tolerance);
        DimensionService.Create(document, bottomLeft.Id, topRight.Id, 400, Tolerance);

        return document;
    }

    private static CadDocument CreateLargeDrawingWithDimensions()
        => CreateRectangleWithDimensions(80_000, 40_000);

    private static CadDocument CreateComplexDrawing()
    {
        var document = CreateRectangleWithDimensions(12_000, 8_000);
        var face = document.Polygons.First(polygon => polygon.Type == PolygonType.Face);
        Assert.True(FaceFillService.TrySetFillPattern(document, face, FaceFillPattern.Diagonal));
        Assert.True(FaceFillService.TrySetFillColor(document, face, Colors.SteelBlue));
        return document;
    }

    private static CadDocument CreateRectangleWithDimensions(double width, double height)
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF((float)width, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF((float)width, 0), new PointF((float)width, (float)height), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF((float)width, (float)height), new PointF(0, (float)height), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, (float)height), new PointF(0, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);

        var horizontalStart = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF(0, 0), Tolerance));
        var horizontalEnd = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF((float)width, 0), Tolerance));
        var verticalEnd = document.Vertices.First(vertex => MathUtils.ArePointsEqual(vertex.Position, new PointF((float)width, (float)height), Tolerance));

        DimensionService.Create(document, horizontalStart.Id, horizontalEnd.Id, Math.Max(50, height * 0.05), Tolerance);
        DimensionService.Create(document, horizontalEnd.Id, verticalEnd.Id, Math.Max(50, width * 0.05), Tolerance);

        if (width > height * 1.2)
        {
            var diagonalEnd = document.Vertices.First(vertex =>
                MathUtils.ArePointsEqual(vertex.Position, new PointF(0, (float)height), Tolerance));
            DimensionService.Create(document, horizontalStart.Id, diagonalEnd.Id, Math.Max(50, Math.Min(width, height) * 0.08), Tolerance);
        }

        return document;
    }
}
