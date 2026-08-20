using LiteCad.Core.Geometry;

namespace LiteCad.Dimensions;

public sealed class DimensionLayout
{
    public required PointF FirstAnchor { get; init; }

    public required PointF SecondAnchor { get; init; }

    public required PointF FirstExtensionEnd { get; init; }

    public required PointF SecondExtensionEnd { get; init; }

    public required PointF DimensionLineStart { get; init; }

    public required PointF DimensionLineEnd { get; init; }

    public required PointF TextPosition { get; init; }

    public required double MeasuredDistance { get; init; }

    public required double TextAngleRadians { get; init; }
}

public static class DimensionGeometry
{
    public static bool TryCreateLayout(
        PointF firstAnchor,
        PointF secondAnchor,
        double offset,
        double tolerance,
        out DimensionLayout layout)
        => TryCreateLayout(
            firstAnchor,
            secondAnchor,
            offset,
            tolerance,
            out layout,
            isOrthogonal: false,
            orthogonalIsHorizontal: false);

    public static bool TryCreateLayout(
        PointF firstAnchor,
        PointF secondAnchor,
        double offset,
        double tolerance,
        out DimensionLayout layout,
        bool isOrthogonal,
        bool orthogonalIsHorizontal)
    {
        layout = null!;
        if (isOrthogonal)
        {
            return orthogonalIsHorizontal
                ? TryCreateHorizontalOrthogonalLayout(firstAnchor, secondAnchor, offset, tolerance, out layout)
                : TryCreateVerticalOrthogonalLayout(firstAnchor, secondAnchor, offset, tolerance, out layout);
        }

        return TryCreateAlignedLayout(firstAnchor, secondAnchor, offset, tolerance, out layout);
    }

    public static bool TryCreateLayout(
        PointF firstAnchor,
        PointF secondAnchor,
        Dimension dimension,
        double tolerance,
        out DimensionLayout layout)
        => TryCreateLayout(
            firstAnchor,
            secondAnchor,
            dimension.Offset,
            tolerance,
            out layout,
            dimension.IsOrthogonal,
            dimension.OrthogonalIsHorizontal);

    public static bool ResolveOrthogonalIsHorizontal(PointF firstAnchor, PointF secondAnchor, PointF cursor)
    {
        var midX = (firstAnchor.X + secondAnchor.X) * 0.5f;
        var midY = (firstAnchor.Y + secondAnchor.Y) * 0.5f;
        return Math.Abs(cursor.X - midX) < Math.Abs(cursor.Y - midY);
    }

    public static double ComputeOrthogonalSignedOffset(
        PointF firstAnchor,
        PointF secondAnchor,
        PointF cursor,
        bool orthogonalIsHorizontal)
    {
        if (orthogonalIsHorizontal)
        {
            var midY = (firstAnchor.Y + secondAnchor.Y) * 0.5f;
            return cursor.Y - midY;
        }

        var midX = (firstAnchor.X + secondAnchor.X) * 0.5f;
        return cursor.X - midX;
    }

    private static bool TryCreateAlignedLayout(
        PointF firstAnchor,
        PointF secondAnchor,
        double offset,
        double tolerance,
        out DimensionLayout layout)
    {
        layout = null!;
        var measuredDistance = MathUtils.Distance(firstAnchor, secondAnchor);
        if (measuredDistance <= tolerance)
        {
            return false;
        }

        if (!TryGetUnitNormal(firstAnchor, secondAnchor, tolerance, out var normal))
        {
            return false;
        }

        var offsetVector = new PointF(normal.X * (float)offset, normal.Y * (float)offset);
        var firstExtensionEnd = Add(firstAnchor, offsetVector);
        var secondExtensionEnd = Add(secondAnchor, offsetVector);

        layout = new DimensionLayout
        {
            FirstAnchor = firstAnchor,
            SecondAnchor = secondAnchor,
            FirstExtensionEnd = firstExtensionEnd,
            SecondExtensionEnd = secondExtensionEnd,
            DimensionLineStart = firstExtensionEnd,
            DimensionLineEnd = secondExtensionEnd,
            TextPosition = Midpoint(firstExtensionEnd, secondExtensionEnd),
            MeasuredDistance = measuredDistance,
            TextAngleRadians = Math.Atan2(
                secondAnchor.Y - firstAnchor.Y,
                secondAnchor.X - firstAnchor.X)
        };

        return true;
    }

    private static bool TryCreateHorizontalOrthogonalLayout(
        PointF firstAnchor,
        PointF secondAnchor,
        double offset,
        double tolerance,
        out DimensionLayout layout)
    {
        layout = null!;
        var measuredDistance = Math.Abs(secondAnchor.X - firstAnchor.X);
        if (measuredDistance <= tolerance)
        {
            return false;
        }

        var midY = (firstAnchor.Y + secondAnchor.Y) * 0.5f;
        var dimensionLineY = midY + (float)offset;
        var firstExtensionEnd = new PointF(firstAnchor.X, dimensionLineY);
        var secondExtensionEnd = new PointF(secondAnchor.X, dimensionLineY);

        layout = new DimensionLayout
        {
            FirstAnchor = firstAnchor,
            SecondAnchor = secondAnchor,
            FirstExtensionEnd = firstExtensionEnd,
            SecondExtensionEnd = secondExtensionEnd,
            DimensionLineStart = firstExtensionEnd,
            DimensionLineEnd = secondExtensionEnd,
            TextPosition = Midpoint(firstExtensionEnd, secondExtensionEnd),
            MeasuredDistance = measuredDistance,
            TextAngleRadians = 0
        };

        return true;
    }

    private static bool TryCreateVerticalOrthogonalLayout(
        PointF firstAnchor,
        PointF secondAnchor,
        double offset,
        double tolerance,
        out DimensionLayout layout)
    {
        layout = null!;
        var measuredDistance = Math.Abs(secondAnchor.Y - firstAnchor.Y);
        if (measuredDistance <= tolerance)
        {
            return false;
        }

        var midX = (firstAnchor.X + secondAnchor.X) * 0.5f;
        var dimensionLineX = midX + (float)offset;
        var firstExtensionEnd = new PointF(dimensionLineX, firstAnchor.Y);
        var secondExtensionEnd = new PointF(dimensionLineX, secondAnchor.Y);

        layout = new DimensionLayout
        {
            FirstAnchor = firstAnchor,
            SecondAnchor = secondAnchor,
            FirstExtensionEnd = firstExtensionEnd,
            SecondExtensionEnd = secondExtensionEnd,
            DimensionLineStart = firstExtensionEnd,
            DimensionLineEnd = secondExtensionEnd,
            TextPosition = Midpoint(firstExtensionEnd, secondExtensionEnd),
            MeasuredDistance = measuredDistance,
            TextAngleRadians = -Math.PI / 2
        };

        return true;
    }

    public static double ComputeSignedOffset(PointF firstAnchor, PointF secondAnchor, PointF cursor)
    {
        if (!TryGetUnitNormal(firstAnchor, secondAnchor, MathUtils.DefaultTolerance, out var normal))
        {
            return 0;
        }

        var vector = new PointF(cursor.X - firstAnchor.X, cursor.Y - firstAnchor.Y);
        return vector.X * normal.X + vector.Y * normal.Y;
    }

    public static bool TryGetAnchorPoints(
        Core.Document.CadDocument document,
        Dimension dimension,
        out PointF firstAnchor,
        out PointF secondAnchor)
    {
        firstAnchor = PointF.Zero;
        secondAnchor = PointF.Zero;

        var first = document.Vertices.FirstOrDefault(vertex => vertex.Id == dimension.FirstVertexId);
        var second = document.Vertices.FirstOrDefault(vertex => vertex.Id == dimension.SecondVertexId);
        if (first is null || second is null)
        {
            return false;
        }

        firstAnchor = first.Position;
        secondAnchor = second.Position;
        return true;
    }

    private static bool TryGetUnitNormal(
        PointF firstAnchor,
        PointF secondAnchor,
        double tolerance,
        out PointF normal)
    {
        normal = PointF.Zero;
        var dx = secondAnchor.X - firstAnchor.X;
        var dy = secondAnchor.Y - firstAnchor.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= tolerance)
        {
            return false;
        }

        normal = new PointF(-dy / (float)length, dx / (float)length);
        return true;
    }

    private static PointF Add(PointF left, PointF right)
        => new(left.X + right.X, left.Y + right.Y);

    private static PointF Midpoint(PointF left, PointF right)
        => new((left.X + right.X) * 0.5f, (left.Y + right.Y) * 0.5f);
}
