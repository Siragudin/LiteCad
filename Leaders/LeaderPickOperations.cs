using LiteCad.Core.Document;
using LiteCad.Core.Geometry;

namespace LiteCad.Leaders;

public static class LeaderPickOperations
{
    public static bool TryPickAt(
        CadDocument document,
        PointF world,
        double tolerance,
        out Guid leaderId,
        double zoom = 1.0)
    {
        leaderId = Guid.Empty;
        Leader? closest = null;
        var closestDistance = tolerance;

        foreach (var leader in document.Leaders)
        {
            if (!TryGetPickDistance(world, leader, tolerance, zoom, out var distance)
                || distance > closestDistance)
            {
                continue;
            }

            closest = leader;
            closestDistance = distance;
        }

        if (closest is null)
        {
            return false;
        }

        leaderId = closest.Id;
        return true;
    }

    private static bool TryGetPickDistance(
        PointF world,
        Leader leader,
        double tolerance,
        double zoom,
        out double distance)
    {
        distance = double.MaxValue;
        var found = false;
        var layout = LeaderGeometry.CreateLayout(leader.Target, leader.TextPosition, zoom);

        foreach (var (start, end) in LeaderGeometry.GetSegments(layout))
        {
            if (!Geometry2D.TryHitTestSegment(world, start, end, tolerance, out var segmentDistance))
            {
                continue;
            }

            found = true;
            if (segmentDistance < distance)
            {
                distance = segmentDistance;
            }
        }

        var textDistance = MathUtils.Distance(world, layout.TextPosition);
        if (textDistance <= Math.Max(tolerance, 8.0))
        {
            found = true;
            if (textDistance < distance)
            {
                distance = textDistance;
            }
        }

        return found;
    }
}
