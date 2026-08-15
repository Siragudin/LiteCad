namespace LiteCad.Core.Geometry;

public readonly struct PointF(double x, double y)
{
    public double X { get; } = x;
    public double Y { get; } = y;

    public static PointF Zero => new(0, 0);

    public static PointF operator +(PointF a, Vector2 v) => new(a.X + v.X, a.Y + v.Y);

    public static PointF operator -(PointF a, PointF b) => new(a.X - b.X, a.Y - b.Y);

    public static Vector2 operator -(PointF a, Vector2 v) => new(a.X - v.X, a.Y - v.Y);
}
