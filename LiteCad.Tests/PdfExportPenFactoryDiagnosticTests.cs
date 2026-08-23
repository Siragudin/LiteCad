using LiteCad.Rendering;
using LiteCad.Rendering.Pdf;
using System.Windows;
using System.Windows.Media;
using Xunit;
using Xunit.Abstractions;

namespace LiteCad.Tests;

public class PdfExportPenFactoryDiagnosticTests
{
    private readonly ITestOutputHelper _output;

    public PdfExportPenFactoryDiagnosticTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AxisPenFactory_Diagnostics()
    {
        WpfTestUtilities.RunSta(RunAxisPenDiagnostics);
    }

    private void RunAxisPenDiagnostics()
    {
        const double expectedPoints = PdfLineweightTable.AxisMm * (PdfExportLayout.PointsPerInch / PdfExportLayout.MmPerInch);
        var toPoints = PdfLineweightTable.ToPoints(PenStyle.Axis);
        _output.WriteLine($"1. PdfLineweightTable.ToPoints(PenStyle.Axis) = {toPoints:G17}");
        _output.WriteLine($"   Expected ~{expectedPoints:G17} (AxisMm={PdfLineweightTable.AxisMm})");
        Assert.False(double.IsNaN(toPoints));
        Assert.False(double.IsInfinity(toPoints));
        Assert.True(toPoints > 0);
        Assert.Equal(expectedPoints, toPoints, 1e-9);

        var axisLineColor = Color.FromRgb(0x15, 0x65, 0xC0);
        var axisLineDashArray = new DoubleCollection { 12, 4, 2, 4 };
        _output.WriteLine(
            "2. PdfExportPenFactory.Create(PenStyle.Axis, Color.FromRgb(0x15,0x65,0xC0), [12,4,2,4])");
        var pen = PdfExportPenFactory.Create(PenStyle.Axis, axisLineColor, axisLineDashArray);

        const double dipToPoint = PdfExportLayout.PointsPerInch / PdfExportLayout.DipPerInch;
        var expectedThicknessDip = toPoints / dipToPoint;
        _output.WriteLine($"   pen.Thickness (DIP) = {pen.Thickness:G17}");
        _output.WriteLine($"   Expected ~{expectedThicknessDip:G17} (~0.944881889 DIP)");
        Assert.False(double.IsNaN(pen.Thickness));
        Assert.False(double.IsInfinity(pen.Thickness));
        Assert.True(pen.Thickness > 0);
        Assert.Equal(expectedThicknessDip, pen.Thickness, 1e-9);

        _output.WriteLine($"3. pen.MiterLimit after Create (MarkPdfPen inside) = {pen.MiterLimit:G17}");
        _output.WriteLine($"   Expected {PdfStyledSolidColorBrush.PdfPenMiterLimitBase + (int)PenStyle.Axis:G17} (=100)");
        Assert.Equal(PdfStyledSolidColorBrush.PdfPenMiterLimitBase + (int)PenStyle.Axis, pen.MiterLimit, 1e-9);

        var markException = Record.Exception(() =>
            PdfStyledSolidColorBrush.MarkPdfPen(pen, PenStyle.Axis));
        _output.WriteLine($"   MarkPdfPen on frozen pen throws: {markException?.GetType().Name ?? "null"}");
        Assert.IsType<InvalidOperationException>(markException);

        _output.WriteLine("4. Compare with RenderStyles.AxisLinePen at export zooms:");
        foreach (var exportZoom in new[] { 0.016, 0.042, 6.89 })
        {
            var screenPen = RenderStyles.AxisLinePen(exportZoom);
            _output.WriteLine(
                $"   zoom={exportZoom:G4}: AxisLinePen Thickness={screenPen.Thickness:G6}, " +
                $"Dashes=[{string.Join(",", screenPen.DashStyle.Dashes)}], MiterLimit={screenPen.MiterLimit}, Frozen={screenPen.IsFrozen}");
        }

        _output.WriteLine(
            $"   PdfExportPen pen: Dashes=[{string.Join(",", pen.DashStyle.Dashes)}], " +
            $"MiterLimit={pen.MiterLimit}, Frozen={pen.IsFrozen}, Brush={pen.Brush}");

        _output.WriteLine("5. Frozen state:");
        _output.WriteLine($"   pen.IsFrozen = {pen.IsFrozen}");
        var modifyThickness = Record.Exception(() => pen.Thickness = 2.0);
        _output.WriteLine($"   pen.Thickness = 2 after Freeze throws: {modifyThickness?.GetType().Name ?? "null"}");
        Assert.True(pen.IsFrozen);
        Assert.IsType<InvalidOperationException>(modifyThickness);

        _output.WriteLine("6. DrawLine with export pen into DrawingVisual (isolated):");
        var visual = new DrawingVisual();
        Exception? drawException = null;
        Rect bounds = default;
        using (var context = visual.RenderOpen())
        {
            try
            {
                context.DrawLine(pen, new Point(0, 0), new Point(100, 0));
                context.DrawLine(pen, new Point(0, 0), new Point(0, 100));
            }
            catch (Exception ex)
            {
                drawException = ex;
            }
        }

        _output.WriteLine($"   DrawLine exception: {drawException?.ToString() ?? "none"}");
        Assert.Null(drawException);

        var drawing = VisualTreeHelper.GetDrawing(visual);
        bounds = drawing?.Bounds ?? Rect.Empty;
        _output.WriteLine($"   Resulting drawing bounds: {bounds}");

        _output.WriteLine("7. WpfDrawingPdfConverter dash path (strokeWidth > 0 check):");
        var strokeWidth = pen.Thickness;
        if (pen.DashStyle.Dashes.Count > 0 && pen.Thickness > 1e-9)
        {
            var dashPattern = pen.DashStyle.Dashes.ToArray();
            _output.WriteLine($"   strokeWidth={strokeWidth:G17}, dashPattern=[{string.Join(",", dashPattern)}]");
            Assert.All(dashPattern, v =>
            {
                Assert.False(double.IsNaN(v));
                Assert.False(double.IsInfinity(v));
            });
        }
    }
}
