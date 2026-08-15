namespace LiteCad.Core.Geometry;

public readonly struct Vector2(double x, double y)
{
    public double X { get; } = x;
    public double Y { get; } = y;

    public static Vector2 Zero => new(0, 0);

    public double Length => Math.Sqrt(X * X + Y * Y);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);

    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);

    public static Vector2 operator *(Vector2 v, double scalar) => new(v.X * scalar, v.Y * scalar);
}
