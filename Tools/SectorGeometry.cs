using LiteCad.Core.Geometry;

namespace LiteCad.Tools;

public static class SectorGeometry
{
    public const double DegreesPerSegment = 10.0;

    public static int ComputeSegmentCount(double angleDegrees)
    {
        var abs = Math.Abs(angleDegrees);
        if (abs <= 0)
        {
            return 0;
        }

        return (int)Math.Ceiling(abs / DegreesPerSegment);
    }

    public static double[] ComputeSegmentSweepAngles(double angleDegrees)
    {
        var count = ComputeSegmentCount(angleDegrees);
        if (count == 0)
        {
            return [];
        }

        var absTotal = Math.Abs(angleDegrees);
        var sign = Math.Sign(angleDegrees);
        if (sign == 0)
        {
            sign = 1;
        }

        var segments = new double[count];
        for (var i = 0; i < count - 1; i++)
        {
            segments[i] = sign * DegreesPerSegment;
        }

        segments[count - 1] = sign * (absTotal - (count - 1) * DegreesPerSegment);
        return segments;
    }

    public static PointF PointOnArc(PointF center, double radius, double angleRadians)
        => new(
            center.X + (float)(Math.Cos(angleRadians) * radius),
            center.Y + (float)(Math.Sin(angleRadians) * radius));

    public static PointF[] ComputeArcPoints(PointF center, double radius, double startAngleRadians, double sweepAngleDegrees)
    {
        var sweeps = ComputeSegmentSweepAngles(sweepAngleDegrees);
        if (sweeps.Length == 0)
        {
            return [];
        }

        var points = new PointF[sweeps.Length + 1];
        points[0] = PointOnArc(center, radius, startAngleRadians);

        var currentAngle = startAngleRadians;
        for (var i = 0; i < sweeps.Length; i++)
        {
            currentAngle += sweeps[i] * Math.PI / 180.0;
            points[i + 1] = PointOnArc(center, radius, currentAngle);
        }

        return points;
    }

    public static bool IsFullCircle(double sweepAngleDegrees)
        => Math.Abs(Math.Abs(sweepAngleDegrees) - 360.0) < 1e-6;

    public static double ComputeRadius(PointF center, PointF pointOnCircle)
        => MathUtils.Distance(center, pointOnCircle);

    public static double ComputeStartAngleRadians(PointF center, PointF startPoint)
        => Math.Atan2(startPoint.Y - center.Y, startPoint.X - center.X);

    public static double ComputeSignedSweepDegrees(double startAngleRadians, PointF cursor, PointF center)
    {
        var cursorAngle = Math.Atan2(cursor.Y - center.Y, cursor.X - center.X);
        var deltaRadians = cursorAngle - startAngleRadians;

        while (deltaRadians <= -Math.PI)
        {
            deltaRadians += 2 * Math.PI;
        }

        while (deltaRadians > Math.PI)
        {
            deltaRadians -= 2 * Math.PI;
        }

        return deltaRadians * 180.0 / Math.PI;
    }
}
