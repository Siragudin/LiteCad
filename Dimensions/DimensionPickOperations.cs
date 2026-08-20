using LiteCad.Core.Document;
using LiteCad.Core.Geometry;

namespace LiteCad.Dimensions;

public static class DimensionPickOperations
{
    public static bool TryPickAt(
        CadDocument document,
        PointF world,
        double tolerance,
        out Guid dimensionId)
    {
        dimensionId = Guid.Empty;
        Dimension? closest = null;
        var closestDistance = tolerance;

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

            if (TryGetPickDistance(world, layout, tolerance, out var distance)
                && distance <= closestDistance)
            {
                closest = dimension;
                closestDistance = distance;
            }
        }

        if (closest is null)
        {
            return false;
        }

        dimensionId = closest.Id;
        return true;
    }

    private static bool TryGetPickDistance(
        PointF world,
        DimensionLayout layout,
        double tolerance,
        out double distance)
    {
        distance = double.MaxValue;
        var found = false;

        UpdateDistance(world, layout.FirstAnchor, layout.FirstExtensionEnd, tolerance, ref distance, ref found);
        UpdateDistance(world, layout.SecondAnchor, layout.SecondExtensionEnd, tolerance, ref distance, ref found);
        UpdateDistance(world, layout.DimensionLineStart, layout.DimensionLineEnd, tolerance, ref distance, ref found);

        return found;
    }

    private static void UpdateDistance(
        PointF world,
        PointF start,
        PointF end,
        double tolerance,
        ref double bestDistance,
        ref bool found)
    {
        if (!Geometry2D.TryProjectPointOnSegment(world, start, end, out _, out var distance, tolerance))
        {
            return;
        }

        found = true;
        if (distance < bestDistance)
        {
            bestDistance = distance;
        }
    }
}
