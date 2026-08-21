namespace LiteCad.Core.Geometry;

public static class MathUtils
{
    public const double DefaultTolerance = 1e-4;

    public static double Distance(PointF a, PointF b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static bool ArePointsEqual(PointF a, PointF b, double tolerance = DefaultTolerance)
        => Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance;

    public static PointF Midpoint(PointF a, PointF b)
        => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);

    public static double SignedPolygonArea(IReadOnlyList<PointF> vertices)
    {
        if (vertices.Count < 3)
        {
            return 0;
        }

        double area = 0;
        for (var i = 0; i < vertices.Count; i++)
        {
            var j = (i + 1) % vertices.Count;
            area += vertices[i].X * vertices[j].Y;
            area -= vertices[j].X * vertices[i].Y;
        }

        return area / 2.0;
    }

    public static double PolygonArea(IReadOnlyList<PointF> vertices)
        => Math.Abs(SignedPolygonArea(vertices));

    public static bool PointInPolygon(PointF point, IReadOnlyList<PointF> vertices)
    {
        var inside = false;
        for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
        {
            var xi = vertices[i].X;
            var yi = vertices[i].Y;
            var xj = vertices[j].X;
            var yj = vertices[j].Y;

            var intersects = yi > point.Y != yj > point.Y &&
                             point.X < (xj - xi) * (point.Y - yi) / (yj - yi + double.Epsilon) + xi;
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public const double SnapTolerancePixels = 12.0;

    public const double SelectionPickPixels = 8.0;

    public static double SnapToleranceWorld(double zoom)
        => Math.Max(DefaultTolerance, SnapTolerancePixels / zoom);

    public static double SelectionPickToleranceWorld(double zoom)
        => Math.Max(DefaultTolerance, SelectionPickPixels / zoom);
}
