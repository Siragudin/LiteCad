using LiteCad.Core.Document;
using LiteCad.Core.Geometry;

namespace LiteCad.Services;

public sealed class FaceFillMigrationEntry
{
    public required string OldIdentity { get; init; }

    public required FaceFillStyle Style { get; init; }

    public required IReadOnlyList<PointF> OldBoundaryPoints { get; init; }

    public required bool HasExplicitFill { get; init; }
}

public static class FaceFillMigration
{
    public static HashSet<string> CaptureFaceIdentities(CadDocument document)
        => document.Polygons
            .Where(polygon => polygon.Type == PolygonType.Face)
            .Select(polygon => FaceIdentity.Create(document, polygon))
            .Where(key => key.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

    public static List<FaceFillMigrationEntry> CaptureAffectedFaceFills(
        CadDocument document,
        IReadOnlySet<Guid> affectedEdgeIds)
    {
        var entries = new List<FaceFillMigrationEntry>();

        foreach (var polygon in document.Polygons)
        {
            if (polygon.Type != PolygonType.Face || polygon.OuterLoop.Edges.Count < 3)
            {
                continue;
            }

            if (!IsFaceFullyBoundedByEdges(polygon, affectedEdgeIds))
            {
                continue;
            }

            var identity = FaceIdentity.Create(document, polygon);
            if (string.IsNullOrEmpty(identity))
            {
                continue;
            }

            entries.Add(new FaceFillMigrationEntry
            {
                OldIdentity = identity,
                Style = FaceFillService.GetFill(document, polygon),
                OldBoundaryPoints = PolygonGeometry.GetBoundaryPoints(document, polygon.OuterLoop),
                HasExplicitFill = document.FaceFillStyles.ContainsKey(identity)
            });
        }

        return entries;
    }

    public static void ApplyMoveOrRotate(
        CadDocument document,
        IReadOnlyList<FaceFillMigrationEntry> entries,
        Func<PointF, PointF> transformPoint)
    {
        var tolerance = TopologyTolerance.ForMutation;

        foreach (var entry in entries)
        {
            if (!entry.HasExplicitFill)
            {
                continue;
            }

            var expectedBoundary = entry.OldBoundaryPoints.Select(transformPoint).ToList();
            if (!TryFindFaceByBoundary(document, expectedBoundary, tolerance, out var newFace))
            {
                continue;
            }

            var newIdentity = FaceIdentity.Create(document, newFace);
            if (string.IsNullOrEmpty(newIdentity))
            {
                continue;
            }

            document.FaceFillStyles[newIdentity] = entry.Style.Clone();
            document.FaceFillStyles.Remove(entry.OldIdentity);
        }
    }

    public static void ApplyCopyOrMirror(
        CadDocument document,
        IReadOnlyList<FaceFillMigrationEntry> entries,
        Func<PointF, PointF> transformPoint,
        IReadOnlySet<string> identitiesBefore)
    {
        var tolerance = TopologyTolerance.ForMutation;

        foreach (var entry in entries)
        {
            if (!entry.HasExplicitFill)
            {
                continue;
            }

            var expectedBoundary = entry.OldBoundaryPoints.Select(transformPoint).ToList();
            if (!TryFindNewFaceByBoundary(document, expectedBoundary, identitiesBefore, tolerance, out var newFace))
            {
                continue;
            }

            var newIdentity = FaceIdentity.Create(document, newFace);
            if (string.IsNullOrEmpty(newIdentity))
            {
                continue;
            }

            document.FaceFillStyles[newIdentity] = entry.Style.Clone();
        }
    }

    private static bool IsFaceFullyBoundedByEdges(Polygon polygon, IReadOnlySet<Guid> edgeIds)
        => polygon.OuterLoop.Edges.All(reference => edgeIds.Contains(reference.EdgeId));

    private static bool TryFindFaceByBoundary(
        CadDocument document,
        IReadOnlyList<PointF> expectedBoundary,
        double tolerance,
        out Polygon face)
    {
        face = null!;
        Polygon? match = null;

        foreach (var polygon in document.Polygons)
        {
            if (polygon.Type != PolygonType.Face)
            {
                continue;
            }

            var boundary = PolygonGeometry.GetBoundaryPoints(document, polygon.OuterLoop);
            if (!BoundariesMatch(expectedBoundary, boundary, tolerance))
            {
                continue;
            }

            match = polygon;
            break;
        }

        if (match is null)
        {
            return false;
        }

        face = match;
        return true;
    }

    private static bool TryFindNewFaceByBoundary(
        CadDocument document,
        IReadOnlyList<PointF> expectedBoundary,
        IReadOnlySet<string> identitiesBefore,
        double tolerance,
        out Polygon face)
    {
        face = null!;
        Polygon? match = null;
        var closestArea = double.MaxValue;

        foreach (var polygon in document.Polygons)
        {
            if (polygon.Type != PolygonType.Face)
            {
                continue;
            }

            var identity = FaceIdentity.Create(document, polygon);
            if (identitiesBefore.Contains(identity))
            {
                continue;
            }

            var boundary = PolygonGeometry.GetBoundaryPoints(document, polygon.OuterLoop);
            if (!BoundariesMatch(expectedBoundary, boundary, tolerance))
            {
                continue;
            }

            var area = PolygonGeometry.GetArea(document, polygon, tolerance);
            if (area >= closestArea)
            {
                continue;
            }

            match = polygon;
            closestArea = area;
        }

        if (match is null)
        {
            return false;
        }

        face = match;
        return true;
    }

    private static bool BoundariesMatch(
        IReadOnlyList<PointF> expected,
        IReadOnlyList<PointF> actual,
        double tolerance)
    {
        if (expected.Count < 3 || expected.Count != actual.Count)
        {
            return false;
        }

        for (var offset = 0; offset < actual.Count; offset++)
        {
            if (PointsMatchAtOffset(expected, actual, offset, tolerance, reverse: false)
                || PointsMatchAtOffset(expected, actual, offset, tolerance, reverse: true))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PointsMatchAtOffset(
        IReadOnlyList<PointF> expected,
        IReadOnlyList<PointF> actual,
        int offset,
        double tolerance,
        bool reverse)
    {
        for (var i = 0; i < expected.Count; i++)
        {
            var actualIndex = reverse
                ? (offset - i + actual.Count) % actual.Count
                : (offset + i) % actual.Count;

            if (!MathUtils.ArePointsEqual(expected[i], actual[actualIndex], tolerance))
            {
                return false;
            }
        }

        return true;
    }
}
