using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Services;
using LiteCad.UI;
using System.Globalization;
using System.Windows;

namespace LiteCad.Rendering.Pdf;

public static class DocumentBoundsCalculator
{
    private const double DefaultPaddingMm = 2.0;

    private const double DimensionTextGapScreen = 12.0;

    private const double DimensionTickHalfLengthScreen = 5.0;

    private const double DimensionTextWidthFactor = 0.55;

    public static Rect ComputeWorldBounds(
        CadDocument document,
        double exportZoom = 1.0,
        LinearDisplayUnit linearUnit = LinearDisplayUnit.Millimeters,
        double tolerance = MathUtils.DefaultTolerance)
    {
        if (document.Vertices.Count == 0 && document.Dimensions.Count == 0 && document.Axes.Count == 0)
        {
            return new Rect(-50, -50, 100, 100);
        }

        var safeZoom = Math.Max(exportZoom, 1e-9);
        var hasBounds = false;
        var minX = double.MaxValue;
        var maxX = double.MinValue;
        var minY = double.MaxValue;
        var maxY = double.MinValue;

        foreach (var vertex in document.Vertices)
        {
            Expand(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, vertex.Position.X, vertex.Position.Y);
        }

        foreach (var edge in document.Edges)
        {
            var halfStroke = Math.Min(100.0, edge.Thickness / (2.0 * safeZoom));
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            ExpandPointWithMargin(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, start, halfStroke);
            ExpandPointWithMargin(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, end, halfStroke);
        }

        const double axisHalfStroke = 1.5;
        foreach (var axis in document.Axes)
        {
            var halfStroke = Math.Min(100.0, axisHalfStroke / (2.0 * safeZoom));
            ExpandPointWithMargin(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, axis.Start, halfStroke);
            ExpandPointWithMargin(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, axis.End, halfStroke);
        }

        foreach (var dimension in document.Dimensions)
        {
            if (!DimensionGeometry.TryGetAnchorPoints(document, dimension, out var firstAnchor, out var secondAnchor)
                || !DimensionGeometry.TryCreateLayout(
                    firstAnchor,
                    secondAnchor,
                    dimension,
                    tolerance,
                    out var layout))
            {
                continue;
            }

            var distanceText = UnitDisplayFormatter.FormatLinear(layout.MeasuredDistance, linearUnit);
            var textWorldHeight = Dimension.NormalizeTextSize(dimension.TextSize);
            var annotationPadding = Math.Max(
                textWorldHeight,
                ScreenPixelsToWorldMargin(
                    DimensionTextGapScreen + DimensionTickHalfLengthScreen,
                    safeZoom));
            ExpandPointWithMargin(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, layout.FirstAnchor, annotationPadding);
            ExpandPointWithMargin(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, layout.SecondAnchor, annotationPadding);
            ExpandPointWithMargin(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, layout.FirstExtensionEnd, annotationPadding);
            ExpandPointWithMargin(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, layout.SecondExtensionEnd, annotationPadding);
            ExpandPointWithMargin(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, layout.DimensionLineStart, annotationPadding);
            ExpandPointWithMargin(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, layout.DimensionLineEnd, annotationPadding);

            var textHalfWidth = Math.Min(
                100.0,
                distanceText.Length * textWorldHeight * DimensionTextWidthFactor * 0.5);
            var textHalfHeight = Math.Min(100.0, textWorldHeight * 0.5);
            var textCenter = new PointF(
                (layout.DimensionLineStart.X + layout.DimensionLineEnd.X) * 0.5f,
                (layout.DimensionLineStart.Y + layout.DimensionLineEnd.Y) * 0.5f);
            Expand(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, textCenter.X - textHalfWidth, textCenter.Y - textHalfHeight);
            Expand(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, textCenter.X + textHalfWidth, textCenter.Y + textHalfHeight);
        }

        if (!hasBounds)
        {
            return new Rect(-50, -50, 100, 100);
        }

        minX -= DefaultPaddingMm;
        minY -= DefaultPaddingMm;
        maxX += DefaultPaddingMm;
        maxY += DefaultPaddingMm;

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static void ExpandPointWithMargin(
        ref bool hasBounds,
        ref double minX,
        ref double maxX,
        ref double minY,
        ref double maxY,
        PointF point,
        double margin)
    {
        Expand(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, point.X - margin, point.Y - margin);
        Expand(ref hasBounds, ref minX, ref maxX, ref minY, ref maxY, point.X + margin, point.Y + margin);
    }

    private static void Expand(
        ref bool hasBounds,
        ref double minX,
        ref double maxX,
        ref double minY,
        ref double maxY,
        double x,
        double y)
    {
        hasBounds = true;
        minX = Math.Min(minX, x);
        maxX = Math.Max(maxX, x);
        minY = Math.Min(minY, y);
        maxY = Math.Max(maxY, y);
    }

    private static double ScreenPixelsToWorldMargin(double screenPixels, double exportZoom, double maxWorldMargin = 100.0)
        => Math.Min(maxWorldMargin, screenPixels / Math.Max(exportZoom, 1e-9));
}
