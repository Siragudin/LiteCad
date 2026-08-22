using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Dimensions;

namespace LiteCad.Services;

public static class AxisOffsetOperations
{
    public static bool TryPickNearestAxis(
        CadDocument document,
        PointF world,
        double tolerance,
        out Axis axis)
    {
        axis = null!;
        if (!SelectionPickOperations.TryPickAxis(document, world, tolerance, out var axisId))
        {
            return false;
        }

        axis = document.Axes.FirstOrDefault(item => item.Id == axisId)!;
        return axis is not null;
    }

    public static double ComputeSignedDistance(Axis axis, PointF cursor, double tolerance)
    {
        if (MathUtils.Distance(axis.Start, axis.End) <= tolerance)
        {
            return 0;
        }

        return DimensionGeometry.ComputeSignedOffset(axis.Start, axis.End, cursor);
    }

    public static bool TryComputeOffsetSegment(
        Axis axis,
        double signedDistance,
        double tolerance,
        out PointF offsetStart,
        out PointF offsetEnd)
    {
        offsetStart = PointF.Zero;
        offsetEnd = PointF.Zero;

        if (Math.Abs(signedDistance) <= tolerance)
        {
            return false;
        }

        if (!TryGetLeftNormal(axis.Start, axis.End, tolerance, out var normalX, out var normalY))
        {
            return false;
        }

        offsetStart = new PointF(
            axis.Start.X + normalX * (float)signedDistance,
            axis.Start.Y + normalY * (float)signedDistance);
        offsetEnd = new PointF(
            axis.End.X + normalX * (float)signedDistance,
            axis.End.Y + normalY * (float)signedDistance);

        return MathUtils.Distance(offsetStart, offsetEnd) > tolerance;
    }

    public static Guid? ExecuteAxisOffset(
        CadDocument document,
        Selection selection,
        Axis axis,
        double signedDistance,
        double tolerance)
    {
        if (!TryComputeOffsetSegment(axis, signedDistance, tolerance, out var start, out var end))
        {
            return null;
        }

        tolerance = TopologyTolerance.ForMutation;
        var edgeIdsBefore = document.Edges.Select(edge => edge.Id).ToHashSet();
        var template = Edge.CreateStyleTemplate();
        EdgeOperations.AddSegment(document, start, end, template, tolerance);

        var newEdgeId = document.Edges
            .FirstOrDefault(edge => !edgeIdsBefore.Contains(edge.Id))
            ?.Id;

        if (newEdgeId is null)
        {
            return null;
        }

        PolygonBuilder.SyncFaces(document, tolerance);

        selection.Clear();
        selection.SelectedEdgeIds.Add(newEdgeId.Value);
        return newEdgeId;
    }

    private static bool TryGetLeftNormal(
        PointF start,
        PointF end,
        double tolerance,
        out float normalX,
        out float normalY)
    {
        normalX = 0;
        normalY = 0;

        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= tolerance)
        {
            return false;
        }

        normalX = (float)(-dy / length);
        normalY = (float)(dx / length);
        return true;
    }
}
