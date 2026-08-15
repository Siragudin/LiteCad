using LiteCad.Core.Document;

namespace LiteCad.Core.Geometry;

public static class Geometry2D
{
    public static bool TryGetSegmentIntersection(
        PointF a1,
        PointF a2,
        PointF b1,
        PointF b2,
        out PointF intersection,
        double tolerance = MathUtils.DefaultTolerance)
    {
        intersection = PointF.Zero;

        var d1X = a2.X - a1.X;
        var d1Y = a2.Y - a1.Y;
        var d2X = b2.X - b1.X;
        var d2Y = b2.Y - b1.Y;
        var denominator = d1X * d2Y - d1Y * d2X;

        if (Math.Abs(denominator) < tolerance)
        {
            return false;
        }

        var t = ((b1.X - a1.X) * d2Y - (b1.Y - a1.Y) * d2X) / denominator;
        var u = ((b1.X - a1.X) * d1Y - (b1.Y - a1.Y) * d1X) / denominator;

        if (t < -tolerance || t > 1 + tolerance || u < -tolerance || u > 1 + tolerance)
        {
            return false;
        }

        intersection = new PointF(a1.X + t * d1X, a1.Y + t * d1Y);
        return true;
    }

    public static bool TryProjectPointOnSegment(
        PointF point,
        PointF segmentStart,
        PointF segmentEnd,
        out PointF projection,
        out double distance,
        double tolerance = MathUtils.DefaultTolerance)
    {
        projection = PointF.Zero;
        distance = double.MaxValue;

        var dx = segmentEnd.X - segmentStart.X;
        var dy = segmentEnd.Y - segmentStart.Y;
        var lengthSquared = dx * dx + dy * dy;

        if (lengthSquared < tolerance * tolerance)
        {
            return false;
        }

        var t = ((point.X - segmentStart.X) * dx + (point.Y - segmentStart.Y) * dy) / lengthSquared;
        t = Math.Clamp(t, 0, 1);
        projection = new PointF(segmentStart.X + t * dx, segmentStart.Y + t * dy);
        distance = MathUtils.Distance(point, projection);
        return true;
    }

    public static bool IsPointOnSegmentInterior(
        PointF point,
        PointF segmentStart,
        PointF segmentEnd,
        double tolerance)
    {
        if (!TryProjectPointOnSegment(point, segmentStart, segmentEnd, out var projection, out var distance, tolerance))
        {
            return false;
        }

        if (distance > tolerance)
        {
            return false;
        }

        return !MathUtils.ArePointsEqual(point, segmentStart, tolerance) &&
               !MathUtils.ArePointsEqual(point, segmentEnd, tolerance);
    }

    public static OrthoAlignment GetOrthoAlignment(PointF start, PointF end, double tolerance)
    {
        if (Math.Abs(start.Y - end.Y) <= tolerance)
        {
            return OrthoAlignment.Horizontal;
        }

        if (Math.Abs(start.X - end.X) <= tolerance)
        {
            return OrthoAlignment.Vertical;
        }

        return OrthoAlignment.None;
    }

    public static PointF ApplyOrtho(PointF start, PointF target)
    {
        var dx = Math.Abs(target.X - start.X);
        var dy = Math.Abs(target.Y - start.Y);
        return dx >= dy
            ? new PointF(target.X, start.Y)
            : new PointF(start.X, target.Y);
    }

    public static bool AreDirectionsParallel(
        double dx1,
        double dy1,
        double length1,
        double dx2,
        double dy2,
        double length2,
        double tolerance)
    {
        if (length1 <= tolerance || length2 <= tolerance)
        {
            return false;
        }

        var cross = Math.Abs(dx1 * dy2 - dy1 * dx2);
        var sinAngleMax = Math.Min(0.2, tolerance / Math.Min(length1, length2));
        return cross <= sinAngleMax * length1 * length2;
    }

    public static bool ArePointsSame(PointF a, PointF b, double tolerance)
        => MathUtils.ArePointsEqual(a, b, tolerance);

    public static double DistanceToLine(
        PointF point,
        PointF lineStart,
        PointF lineEnd,
        double tolerance = MathUtils.DefaultTolerance)
    {
        var dx = lineEnd.X - lineStart.X;
        var dy = lineEnd.Y - lineStart.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= tolerance)
        {
            return MathUtils.Distance(point, lineStart);
        }

        return Math.Abs((point.X - lineStart.X) * dy - (point.Y - lineStart.Y) * dx) / length;
    }

    public static double GetSegmentParameter(PointF point, PointF segmentStart, PointF segmentEnd, double tolerance)
    {
        var dx = segmentEnd.X - segmentStart.X;
        var dy = segmentEnd.Y - segmentStart.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= tolerance * tolerance)
        {
            return 0;
        }

        return ((point.X - segmentStart.X) * dx + (point.Y - segmentStart.Y) * dy) / lengthSquared;
    }

    public static PointF GetSegmentPoint(PointF segmentStart, PointF segmentEnd, double parameter)
    {
        var dx = segmentEnd.X - segmentStart.X;
        var dy = segmentEnd.Y - segmentStart.Y;
        return new PointF(segmentStart.X + dx * parameter, segmentStart.Y + dy * parameter);
    }

    public static bool AreSegmentsCollinear(
        PointF a1,
        PointF a2,
        PointF b1,
        PointF b2,
        double tolerance)
    {
        var lengthA = MathUtils.Distance(a1, a2);
        var lengthB = MathUtils.Distance(b1, b2);
        if (lengthA <= tolerance || lengthB <= tolerance)
        {
            return false;
        }

        if (!AreDirectionsParallel(
                a2.X - a1.X,
                a2.Y - a1.Y,
                lengthA,
                b2.X - b1.X,
                b2.Y - b1.Y,
                lengthB,
                tolerance))
        {
            return false;
        }

        return DistanceToLine(b1, a1, a2, tolerance) <= tolerance &&
               DistanceToLine(b2, a1, a2, tolerance) <= tolerance;
    }

    public static bool TryGetCollinearSegmentOverlap(
        PointF newStart,
        PointF newEnd,
        PointF existingStart,
        PointF existingEnd,
        double tolerance,
        out PointF overlapStart,
        out PointF overlapEnd)
    {
        overlapStart = PointF.Zero;
        overlapEnd = PointF.Zero;

        if (!AreSegmentsCollinear(newStart, newEnd, existingStart, existingEnd, tolerance))
        {
            return false;
        }

        var tExistingStart = GetSegmentParameter(existingStart, newStart, newEnd, tolerance);
        var tExistingEnd = GetSegmentParameter(existingEnd, newStart, newEnd, tolerance);
        var tMin = Math.Min(tExistingStart, tExistingEnd);
        var tMax = Math.Max(tExistingStart, tExistingEnd);

        var overlapMin = Math.Max(0, tMin);
        var overlapMax = Math.Min(1, tMax);
        if (overlapMax - overlapMin <= tolerance / Math.Max(MathUtils.Distance(newStart, newEnd), tolerance))
        {
            return false;
        }

        overlapStart = GetSegmentPoint(newStart, newEnd, overlapMin);
        overlapEnd = GetSegmentPoint(newStart, newEnd, overlapMax);
        return true;
    }

    public static int ComparePoints(PointF a, PointF b, double tolerance)
    {
        if (Math.Abs(a.X - b.X) > tolerance)
        {
            return a.X < b.X ? -1 : 1;
        }

        if (Math.Abs(a.Y - b.Y) > tolerance)
        {
            return a.Y < b.Y ? -1 : 1;
        }

        return 0;
    }

    public static PointF NormalizePoint(PointF point, double tolerance)
    {
        var scale = 1.0 / tolerance;
        return new PointF(Math.Round(point.X * scale) / scale, Math.Round(point.Y * scale) / scale);
    }
}
