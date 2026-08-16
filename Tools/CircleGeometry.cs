using LiteCad.Core.Geometry;

namespace LiteCad.Tools;

public static class CircleGeometry
{
    public const int SegmentCount = 36;
    public const double DegreesPerSegment = 10.0;

    public static PointF[] ComputePoints(PointF center, double radius)
    {
        var points = new PointF[SegmentCount];
        for (var i = 0; i < SegmentCount; i++)
        {
            var angleRadians = i * DegreesPerSegment * Math.PI / 180.0;
            points[i] = new PointF(
                center.X + (float)(Math.Cos(angleRadians) * radius),
                center.Y + (float)(Math.Sin(angleRadians) * radius));
        }

        return points;
    }

    public static double ComputeRadius(PointF center, PointF pointOnCircle)
        => MathUtils.Distance(center, pointOnCircle);
}
