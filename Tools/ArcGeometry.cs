using LiteCad.Core.Geometry;

namespace LiteCad.Tools;

public static class ArcGeometry
{
    public static int ComputeOpenArcEdgeCount(double sweepDegrees)
        => SectorGeometry.ComputeSegmentCount(sweepDegrees);

    public static PointF[] ComputeOpenArcPoints(
        PointF center,
        double radius,
        double startAngleRadians,
        double sweepDegrees)
        => SectorGeometry.ComputeArcPoints(center, radius, startAngleRadians, sweepDegrees);

    public static bool TryComputeRadiusFromSagitta(PointF a, PointF b, PointF cursor, out double radius)
    {
        radius = 0;
        var chord = MathUtils.Distance(a, b);
        var topologyTolerance = TopologyTolerance.ForMutation;
        if (chord <= topologyTolerance)
        {
            return false;
        }

        var sag = ComputeSignedSagitta(a, b, cursor);
        if (Math.Abs(sag) <= topologyTolerance)
        {
            return false;
        }

        radius = (chord * chord) / (8 * Math.Abs(sag)) + Math.Abs(sag) / 2;
        return radius + topologyTolerance >= chord / 2;
    }

    public static bool TryBuildArc(
        PointF a,
        PointF b,
        double radius,
        PointF cursor,
        out PointF center,
        out double startAngleRadians,
        out double sweepDegrees)
    {
        center = default;
        startAngleRadians = 0;
        sweepDegrees = 0;

        var topologyTolerance = TopologyTolerance.ForMutation;
        var chord = MathUtils.Distance(a, b);
        if (radius + topologyTolerance < chord / 2)
        {
            return false;
        }

        if (chord <= topologyTolerance)
        {
            center = a;
            startAngleRadians = 0;
            sweepDegrees = 360;
            return true;
        }

        var halfChord = chord / 2;
        var heightSquared = radius * radius - halfChord * halfChord;
        if (heightSquared < -topologyTolerance)
        {
            return false;
        }

        var height = Math.Sqrt(Math.Max(0, heightSquared));
        var mid = new PointF((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        var dirX = (b.X - a.X) / chord;
        var dirY = (b.Y - a.Y) / chord;
        var leftPerpX = -dirY;
        var leftPerpY = dirX;

        var candidateCenters = new[]
        {
            new PointF(mid.X + height * leftPerpX, mid.Y + height * leftPerpY),
            new PointF(mid.X - height * leftPerpX, mid.Y - height * leftPerpY)
        };

        var bestDistance = double.MaxValue;
        var found = false;

        foreach (var candidateCenter in candidateCenters)
        {
            var candidateStart = Math.Atan2(a.Y - candidateCenter.Y, a.X - candidateCenter.X);
            var endAngle = Math.Atan2(b.Y - candidateCenter.Y, b.X - candidateCenter.X);

            var minorSweepRadians = NormalizeRadians(endAngle - candidateStart);
            var majorSweepRadians = minorSweepRadians > 0
                ? minorSweepRadians - 2 * Math.PI
                : minorSweepRadians + 2 * Math.PI;

            foreach (var sweepRadians in new[] { minorSweepRadians, majorSweepRadians })
            {
                var midpoint = SectorGeometry.PointOnArc(
                    candidateCenter,
                    radius,
                    candidateStart + sweepRadians / 2);
                var distance = MathUtils.Distance(cursor, midpoint);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                center = candidateCenter;
                startAngleRadians = candidateStart;
                sweepDegrees = sweepRadians * 180.0 / Math.PI;
                found = true;
            }
        }

        return found && Math.Abs(sweepDegrees) > topologyTolerance;
    }

    public static double ComputeCentralAngleDegrees(double chord, double radius)
    {
        if (radius <= 0 || chord <= 0)
        {
            return 0;
        }

        var ratio = Math.Clamp(chord / (2 * radius), 0, 1);
        return 2 * Math.Asin(ratio) * 180.0 / Math.PI;
    }

    private static double ComputeSignedSagitta(PointF a, PointF b, PointF point)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 0)
        {
            return 0;
        }

        return ((point.X - a.X) * dy - (point.Y - a.Y) * dx) / Math.Sqrt(lengthSquared);
    }

    private static double NormalizeRadians(double radians)
    {
        while (radians <= -Math.PI)
        {
            radians += 2 * Math.PI;
        }

        while (radians > Math.PI)
        {
            radians -= 2 * Math.PI;
        }

        return radians;
    }
}
