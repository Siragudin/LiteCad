using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;

namespace LiteCad.Services;

public enum OffsetValidationResult
{
    Valid,
    ZeroDistance,
    Degenerate,
    SelfIntersection,
    UnsupportedFace
}

public sealed class OffsetLoopResult
{
    public required OffsetValidationResult Validation { get; init; }

    public IReadOnlyList<PointF> Points { get; init; } = [];
}

public static class OffsetOperations
{
    public static bool CanOffsetFace(Polygon polygon)
        => polygon.Type == PolygonType.Face
            && polygon.OuterLoop.Edges.Count >= 3
            && polygon.InnerLoops.Count == 0;

    public static IReadOnlyList<PointF> GetFaceBoundary(CadDocument document, Polygon face)
        => PolygonGeometry.GetBoundaryPoints(document, face.OuterLoop);

    public static double ComputeSignedDistance(
        CadDocument document,
        Polygon face,
        PointF cursor,
        double tolerance)
    {
        var boundary = GetFaceBoundary(document, face);
        if (boundary.Count < 3)
        {
            return 0;
        }

        var inside = PolygonGeometry.ContainsPoint(document, face, cursor, tolerance);
        var unsigned = ComputeMinimumBoundaryDistance(boundary, cursor, tolerance);
        return inside ? -unsigned : unsigned;
    }

    public static OffsetLoopResult ComputeOffsetLoop(
        IReadOnlyList<PointF> boundary,
        double signedDistance,
        double tolerance)
    {
        if (boundary.Count < 3)
        {
            return new OffsetLoopResult { Validation = OffsetValidationResult.UnsupportedFace };
        }

        if (Math.Abs(signedDistance) <= tolerance)
        {
            return new OffsetLoopResult { Validation = OffsetValidationResult.ZeroDistance };
        }

        var offsetPoints = BuildOffsetPoints(boundary, signedDistance, tolerance);
        if (offsetPoints.Count < 3)
        {
            return new OffsetLoopResult { Validation = OffsetValidationResult.Degenerate };
        }

        if (Math.Abs(MathUtils.SignedPolygonArea(offsetPoints)) <= tolerance * tolerance)
        {
            return new OffsetLoopResult { Validation = OffsetValidationResult.Degenerate };
        }

        if (HasZeroLengthEdge(offsetPoints, tolerance))
        {
            return new OffsetLoopResult { Validation = OffsetValidationResult.Degenerate };
        }

        if (HasSelfIntersection(offsetPoints, tolerance))
        {
            return new OffsetLoopResult { Validation = OffsetValidationResult.SelfIntersection };
        }

        return new OffsetLoopResult
        {
            Validation = OffsetValidationResult.Valid,
            Points = offsetPoints
        };
    }

    public static OffsetLoopResult ComputeFaceOffset(
        CadDocument document,
        Polygon face,
        double signedDistance,
        double tolerance)
    {
        if (!CanOffsetFace(face))
        {
            return new OffsetLoopResult { Validation = OffsetValidationResult.UnsupportedFace };
        }

        return ComputeOffsetLoop(GetFaceBoundary(document, face), signedDistance, tolerance);
    }

    public static Guid? ExecuteObjectOffset(
        CadDocument document,
        Selection selection,
        Polygon sourceFace,
        double signedDistance,
        double tolerance)
    {
        var computation = ComputeFaceOffset(document, sourceFace, signedDistance, tolerance);
        if (computation.Validation != OffsetValidationResult.Valid || computation.Points.Count < 3)
        {
            return null;
        }

        tolerance = TopologyTolerance.ForMutation;
        var template = GetEdgeTemplate(document, sourceFace);
        var faceIdentitiesBefore = CaptureFaceIdentities(document);
        var edgeIdsBefore = document.Edges.Select(edge => edge.Id).ToHashSet();
        var count = computation.Points.Count;

        for (var index = 0; index < count; index++)
        {
            var start = computation.Points[index];
            var end = computation.Points[(index + 1) % count];
            if (MathUtils.Distance(start, end) <= tolerance)
            {
                continue;
            }

            EdgeOperations.AddSegment(document, start, end, template, tolerance);
        }

        var newEdgeIds = document.Edges
            .Where(edge => !edgeIdsBefore.Contains(edge.Id))
            .Select(edge => edge.Id)
            .ToHashSet();

        if (newEdgeIds.Count == 0)
        {
            return null;
        }

        PolygonBuilder.SyncFaces(document, tolerance);

        if (!TryFindNewOffsetFace(document, faceIdentitiesBefore, newEdgeIds, out var newFace))
        {
            return null;
        }

        selection.Clear();
        selection.SelectedPolygonIds.Add(newFace.Id);
        foreach (var reference in newFace.OuterLoop.Edges)
        {
            selection.SelectedEdgeIds.Add(reference.EdgeId);
        }

        return newFace.Id;
    }

    private static HashSet<string> CaptureFaceIdentities(CadDocument document)
        => document.Polygons
            .Where(polygon => polygon.Type == PolygonType.Face)
            .Select(polygon => FaceIdentity.Create(document, polygon))
            .ToHashSet(StringComparer.Ordinal);

    private static bool TryFindNewOffsetFace(
        CadDocument document,
        IReadOnlySet<string> faceIdentitiesBefore,
        IReadOnlySet<Guid> newEdgeIds,
        out Polygon newFace)
    {
        newFace = null!;
        var candidates = document.Polygons
            .Where(polygon => polygon.Type == PolygonType.Face)
            .Where(polygon => !faceIdentitiesBefore.Contains(FaceIdentity.Create(document, polygon)))
            .ToList();

        if (candidates.Count == 0)
        {
            return false;
        }

        newFace = candidates.FirstOrDefault(
            polygon => polygon.OuterLoop.Edges.Count > 0
                && polygon.OuterLoop.Edges.All(reference => newEdgeIds.Contains(reference.EdgeId)))
            ?? candidates[0];

        return true;
    }

    private static Edge GetEdgeTemplate(CadDocument document, Polygon face)
    {
        var edgeId = face.OuterLoop.Edges[0].EdgeId;
        var edge = document.Edges.First(item => item.Id == edgeId);
        return edge.CloneGeometry();
    }

    private static double ComputeMinimumBoundaryDistance(
        IReadOnlyList<PointF> boundary,
        PointF cursor,
        double tolerance)
    {
        var minimum = double.MaxValue;
        for (var index = 0; index < boundary.Count; index++)
        {
            var start = boundary[index];
            var end = boundary[(index + 1) % boundary.Count];
            if (!Geometry2D.TryProjectPointOnSegment(cursor, start, end, out _, out var distance, tolerance))
            {
                continue;
            }

            minimum = Math.Min(minimum, distance);
        }

        return minimum == double.MaxValue ? 0 : minimum;
    }

    private static List<PointF> BuildOffsetPoints(
        IReadOnlyList<PointF> boundary,
        double signedDistance,
        double tolerance)
    {
        var count = boundary.Count;
        var windingSign = Math.Sign(MathUtils.SignedPolygonArea(boundary));
        if (windingSign == 0)
        {
            windingSign = 1;
        }

        var result = new List<PointF>(count);

        for (var index = 0; index < count; index++)
        {
            var previousIndex = (index - 1 + count) % count;
            var vertex = boundary[index];
            var previous = boundary[previousIndex];
            var next = boundary[(index + 1) % count];

            GetOutwardNormal(previous, vertex, windingSign, out var previousNormalX, out var previousNormalY);
            GetOutwardNormal(vertex, next, windingSign, out var currentNormalX, out var currentNormalY);

            var previousOffsetPoint = new PointF(
                vertex.X + previousNormalX * signedDistance,
                vertex.Y + previousNormalY * signedDistance);
            var currentOffsetPoint = new PointF(
                vertex.X + currentNormalX * signedDistance,
                vertex.Y + currentNormalY * signedDistance);

            var previousDirection = new PointF(vertex.X - previous.X, vertex.Y - previous.Y);
            var currentDirection = new PointF(next.X - vertex.X, next.Y - vertex.Y);

            if (TryIntersectLines(
                    previousOffsetPoint,
                    new PointF(
                        previousOffsetPoint.X + previousDirection.X,
                        previousOffsetPoint.Y + previousDirection.Y),
                    currentOffsetPoint,
                    new PointF(
                        currentOffsetPoint.X + currentDirection.X,
                        currentOffsetPoint.Y + currentDirection.Y),
                    out var miter,
                    tolerance))
            {
                result.Add(miter);
                continue;
            }

            if (!MathUtils.ArePointsEqual(previousOffsetPoint, currentOffsetPoint, tolerance))
            {
                result.Add(previousOffsetPoint);
                result.Add(currentOffsetPoint);
            }
            else
            {
                result.Add(previousOffsetPoint);
            }
        }

        return RemoveDuplicatePoints(result, tolerance);
    }

    private static List<PointF> RemoveDuplicatePoints(IReadOnlyList<PointF> points, double tolerance)
    {
        if (points.Count == 0)
        {
            return [];
        }

        var filtered = new List<PointF> { points[0] };
        for (var index = 1; index < points.Count; index++)
        {
            if (!MathUtils.ArePointsEqual(points[index], filtered[^1], tolerance))
            {
                filtered.Add(points[index]);
            }
        }

        if (filtered.Count > 1
            && MathUtils.ArePointsEqual(filtered[0], filtered[^1], tolerance))
        {
            filtered.RemoveAt(filtered.Count - 1);
        }

        return filtered;
    }

    private static void GetOutwardNormal(
        PointF start,
        PointF end,
        int windingSign,
        out float normalX,
        out float normalY)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 0)
        {
            normalX = 0;
            normalY = 0;
            return;
        }

        var unitX = (float)(dx / length);
        var unitY = (float)(dy / length);
        if (windingSign >= 0)
        {
            normalX = unitY;
            normalY = -unitX;
            return;
        }

        normalX = -unitY;
        normalY = unitX;
    }

    private static bool TryIntersectLines(
        PointF a1,
        PointF a2,
        PointF b1,
        PointF b2,
        out PointF intersection,
        double tolerance)
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
        intersection = new PointF(a1.X + t * d1X, a1.Y + t * d1Y);
        return true;
    }

    private static bool HasZeroLengthEdge(IReadOnlyList<PointF> points, double tolerance)
    {
        for (var index = 0; index < points.Count; index++)
        {
            var start = points[index];
            var end = points[(index + 1) % points.Count];
            if (MathUtils.Distance(start, end) <= tolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSelfIntersection(IReadOnlyList<PointF> points, double tolerance)
    {
        var count = points.Count;
        if (count < 4)
        {
            return false;
        }

        for (var first = 0; first < count; first++)
        {
            var a1 = points[first];
            var a2 = points[(first + 1) % count];

            for (var second = first + 1; second < count; second++)
            {
                if (AreAdjacentSegments(count, first, second))
                {
                    continue;
                }

                var b1 = points[second];
                var b2 = points[(second + 1) % count];
                if (TryIntersectLines(a1, a2, b1, b2, out var intersection, tolerance)
                    && IsPointOnSegment(intersection, a1, a2, tolerance)
                    && IsPointOnSegment(intersection, b1, b2, tolerance))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AreAdjacentSegments(int count, int first, int second)
    {
        if (first == second)
        {
            return true;
        }

        if ((first + 1) % count == second || (second + 1) % count == first)
        {
            return true;
        }

        return false;
    }

    private static bool IsPointOnSegment(PointF point, PointF start, PointF end, double tolerance)
        => Geometry2D.TryProjectPointOnSegment(point, start, end, out _, out var distance, tolerance)
            && distance <= tolerance;
}
