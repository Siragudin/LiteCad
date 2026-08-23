using LiteCad.Core.Geometry;

namespace LiteCad.Leaders;

public readonly record struct LeaderLayout(
    PointF Target,
    PointF Elbow,
    PointF LandingEnd,
    PointF TextPosition,
    PointF BaseStart,
    PointF BaseEnd,
    PointF ChevronLeft,
    PointF ChevronRight,
    int Side);

public static class LeaderGeometry
{
    public const double BaseHalfLengthScreen = 6.0;

    public const double ChevronHeightScreen = 8.0;

    public const double StemHeightScreen = 28.0;

    public const double ShelfLengthScreen = 36.0;

    public const double TextGapAboveShelfScreen = 3.0;

    public static LeaderLayout CreateLayout(PointF target, PointF textPosition, double zoom = 1.0)
    {
        var world = 1.0 / Math.Max(zoom, 1e-6);
        var baseHalf = BaseHalfLengthScreen * world;
        var chevronHeight = ChevronHeightScreen * world;
        var stemHeight = StemHeightScreen * world;
        var shelfLength = ShelfLengthScreen * world;
        var side = ResolveSide(target, textPosition);

        var elbow = new PointF(target.X, target.Y + stemHeight);
        var landing = new PointF(target.X + side * shelfLength, elbow.Y);
        var text = new PointF((elbow.X + landing.X) * 0.5, elbow.Y);

        return new LeaderLayout(
            target,
            elbow,
            landing,
            text,
            new PointF(target.X - baseHalf, target.Y),
            new PointF(target.X + baseHalf, target.Y),
            new PointF(target.X - baseHalf, target.Y + chevronHeight),
            new PointF(target.X + baseHalf, target.Y + chevronHeight),
            side);
    }

    public static PointF ConstrainHorizontal(PointF measurePoint, PointF placement)
        => new(placement.X, measurePoint.Y);

    public static int ResolveSide(PointF measurePoint, PointF placement)
        => placement.X < measurePoint.X - 1e-9 ? -1 : 1;

    public static PointF CreateSideHint(PointF graphicOrigin, int side)
        => new(graphicOrigin.X + (side >= 0 ? 1 : -1), graphicOrigin.Y);

    public static IEnumerable<(PointF Start, PointF End)> GetSegments(LeaderLayout layout)
    {
        yield return (layout.BaseStart, layout.BaseEnd);
        yield return (layout.Target, layout.ChevronLeft);
        yield return (layout.Target, layout.ChevronRight);
        yield return (layout.Target, layout.Elbow);
        yield return (layout.Elbow, layout.LandingEnd);
    }
}
