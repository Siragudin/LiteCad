using LiteCad.Core.Document;
using LiteCad.Core.Geometry;

namespace LiteCad.Services;

public sealed class SnapService
{
    private readonly record struct SnapCandidate(SnapPoint Snap, SnapIdentity Identity, double Distance);

    public SnapResult FindBestSnap(
        CadDocument document,
        PointF cursor,
        double tolerance,
        Guid? excludeEdgeId = null,
        bool includeOnEdge = true)
    {
        var candidates = BuildGeometricCandidates(document, cursor, tolerance, excludeEdgeId, includeOnEdge);
        var ordered = candidates
            .Where(candidate => candidate.Distance <= tolerance)
            .OrderBy(candidate => GetKindPriority(candidate.Identity.Kind))
            .ThenBy(candidate => candidate.Distance)
            .ToList();

        SnapPoint? best = ordered.Count > 0 ? ordered[0].Snap : null;
        return new SnapResult(best, best.HasValue);
    }

    public IReadOnlyList<SnapPoint> GetVisibleSnaps(CadDocument document, PointF cursor, double tolerance)
    {
        return BuildGeometricCandidates(document, cursor, tolerance, excludeEdgeId: null, includeOnEdge: false)
            .Where(candidate => candidate.Distance <= tolerance)
            .Select(candidate => candidate.Snap)
            .ToList();
    }

    public bool TrySnapDrawingAlignment(
        CadDocument document,
        PointF start,
        PointF target,
        double tolerance,
        out PointF snappedEnd)
    {
        snappedEnd = target;

        var alignment = Geometry2D.GetOrthoAlignment(start, target, tolerance);
        if (alignment == OrthoAlignment.None)
        {
            return false;
        }

        var bestDelta = double.MaxValue;

        if (alignment == OrthoAlignment.Horizontal)
        {
            foreach (var point in CollectAlignmentReferencePoints(document, tolerance))
            {
                if (MathUtils.ArePointsEqual(point, start, tolerance))
                {
                    continue;
                }

                var xDelta = Math.Abs(target.X - point.X);
                if (xDelta > tolerance || xDelta >= bestDelta)
                {
                    continue;
                }

                snappedEnd = new PointF(point.X, start.Y);
                bestDelta = xDelta;
            }
        }
        else
        {
            foreach (var point in CollectVerticalAlignmentReferencePoints(document, tolerance))
            {
                if (MathUtils.ArePointsEqual(point, start, tolerance))
                {
                    continue;
                }

                var yDelta = Math.Abs(target.Y - point.Y);
                if (yDelta > tolerance || yDelta >= bestDelta)
                {
                    continue;
                }

                snappedEnd = new PointF(start.X, point.Y);
                bestDelta = yDelta;
            }
        }

        return bestDelta < double.MaxValue;
    }

    public SnapPoint? FindVisibleDrawingAlignmentSnap(
        CadDocument document,
        PointF start,
        PointF target,
        double tolerance)
    {
        if (!TrySnapDrawingAlignment(document, start, target, tolerance, out var snappedEnd))
        {
            return null;
        }

        return new SnapPoint(snappedEnd, SnapKind.Alignment);
    }

    private static IReadOnlyList<SnapCandidate> BuildGeometricCandidates(
        CadDocument document,
        PointF cursor,
        double tolerance,
        Guid? excludeEdgeId,
        bool includeOnEdge)
    {
        tolerance = TopologyTolerance.Resolve(tolerance);

        var raw = CollectCandidates(document, cursor, tolerance, excludeEdgeId, includeOnEdge);
        var canonical = CanonicalizeCandidates(document, raw, tolerance);
        var deduplicated = DeduplicateCandidates(canonical);
        return ResolveDistances(deduplicated, cursor);
    }

    private static List<(SnapPoint Snap, SnapIdentity Identity)> CollectCandidates(
        CadDocument document,
        PointF cursor,
        double tolerance,
        Guid? excludeEdgeId,
        bool includeOnEdge)
    {
        var candidates = new List<(SnapPoint Snap, SnapIdentity Identity)>();

        foreach (var vertex in document.Vertices)
        {
            candidates.Add((
                new SnapPoint(vertex.Position, SnapKind.Endpoint, vertexId: vertex.Id),
                SnapIdentity.ForVertex(vertex.Id)));
        }

        foreach (var edge in document.Edges)
        {
            if (excludeEdgeId.HasValue && edge.Id == excludeEdgeId.Value)
            {
                continue;
            }

            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            var midpoint = MathUtils.Midpoint(start, end);

            candidates.Add((
                new SnapPoint(midpoint, SnapKind.Midpoint, edge.Id),
                SnapIdentity.ForMidpoint(edge.Id)));

            if (includeOnEdge &&
                Geometry2D.TryProjectPointOnSegment(cursor, start, end, out var projection, out var distance, tolerance) &&
                distance <= tolerance &&
                Geometry2D.IsPointOnSegmentInterior(projection, start, end, tolerance))
            {
                var parameterKey = QuantizeEdgeParameter(start, end, projection, tolerance);
                candidates.Add((
                    new SnapPoint(projection, SnapKind.OnEdge, edge.Id),
                    SnapIdentity.ForOnEdge(edge.Id, parameterKey)));
            }
        }

        var intersectionGroups = new Dictionary<string, (PointF Position, HashSet<Guid> EdgeIds)>(StringComparer.Ordinal);

        foreach (var edgeA in document.Edges)
        {
            foreach (var edgeB in document.Edges)
            {
                if (edgeA.Id.CompareTo(edgeB.Id) >= 0)
                {
                    continue;
                }

                if (!IntersectionService.TryGetSegmentIntersection(
                        document,
                        edgeA,
                        edgeB,
                        out var intersection,
                        tolerance))
                {
                    continue;
                }

                if (TopologyService.FindVertex(document, intersection, tolerance) is not null)
                {
                    continue;
                }

                var groupKey = FindIntersectionGroupKey(intersectionGroups, intersection, tolerance);
                if (!intersectionGroups.TryGetValue(groupKey, out var group))
                {
                    group = (intersection, []);
                    intersectionGroups[groupKey] = group;
                }

                group.EdgeIds.Add(edgeA.Id);
                group.EdgeIds.Add(edgeB.Id);
                intersectionGroups[groupKey] = group;
            }
        }

        foreach (var group in intersectionGroups.Values)
        {
            var identity = SnapIdentity.ForIntersection(group.EdgeIds);
            candidates.Add((
                new SnapPoint(group.Position, SnapKind.Intersection),
                identity));
        }

        return candidates;
    }

    private static List<(SnapPoint Snap, SnapIdentity Identity)> CanonicalizeCandidates(
        CadDocument document,
        IReadOnlyList<(SnapPoint Snap, SnapIdentity Identity)> candidates,
        double tolerance)
    {
        var canonical = new List<(SnapPoint Snap, SnapIdentity Identity)>(candidates.Count);

        foreach (var (snap, identity) in candidates)
        {
            var vertex = TopologyService.FindVertex(document, snap.Position, tolerance);
            if (vertex is not null)
            {
                canonical.Add((
                    new SnapPoint(vertex.Position, SnapKind.Endpoint, vertexId: vertex.Id),
                    SnapIdentity.ForVertex(vertex.Id)));
                continue;
            }

            canonical.Add((snap, identity));
        }

        return canonical;
    }

    private static List<(SnapPoint Snap, SnapIdentity Identity)> DeduplicateCandidates(
        IReadOnlyList<(SnapPoint Snap, SnapIdentity Identity)> candidates)
    {
        var bestByIdentity = new Dictionary<SnapIdentity, (SnapPoint Snap, SnapIdentity Identity)>();

        foreach (var candidate in candidates)
        {
            if (!bestByIdentity.TryGetValue(candidate.Identity, out var existing) ||
                GetKindPriority(candidate.Identity.Kind) < GetKindPriority(existing.Identity.Kind))
            {
                bestByIdentity[candidate.Identity] = candidate;
                continue;
            }

            if (GetKindPriority(candidate.Identity.Kind) == GetKindPriority(existing.Identity.Kind))
            {
                bestByIdentity[candidate.Identity] = candidate;
            }
        }

        return bestByIdentity.Values.ToList();
    }

    private static IReadOnlyList<SnapCandidate> ResolveDistances(
        IReadOnlyList<(SnapPoint Snap, SnapIdentity Identity)> candidates,
        PointF cursor)
    {
        return candidates
            .Select(candidate => new SnapCandidate(
                candidate.Snap,
                candidate.Identity,
                MathUtils.Distance(cursor, candidate.Snap.Position)))
            .ToList();
    }

    private static IEnumerable<PointF> CollectAlignmentReferencePoints(CadDocument document, double tolerance)
    {
        tolerance = TopologyTolerance.Resolve(tolerance);
        var points = new List<PointF>();

        foreach (var vertex in document.Vertices)
        {
            AddUniquePoint(points, vertex.Position, tolerance);
        }

        foreach (var edge in document.Edges)
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            var midpoint = MathUtils.Midpoint(start, end);

            if (TopologyService.FindVertex(document, midpoint, tolerance) is null)
            {
                AddUniquePoint(points, midpoint, tolerance);
            }
        }

        foreach (var edgeA in document.Edges)
        {
            foreach (var edgeB in document.Edges)
            {
                if (edgeA.Id.CompareTo(edgeB.Id) >= 0)
                {
                    continue;
                }

                if (!IntersectionService.TryGetSegmentIntersection(
                        document,
                        edgeA,
                        edgeB,
                        out var intersection,
                        tolerance))
                {
                    continue;
                }

                if (TopologyService.FindVertex(document, intersection, tolerance) is not null)
                {
                    continue;
                }

                AddUniquePoint(points, intersection, tolerance);
            }
        }

        return points;
    }

    private static IEnumerable<PointF> CollectVerticalAlignmentReferencePoints(CadDocument document, double tolerance)
    {
        tolerance = TopologyTolerance.Resolve(tolerance);
        var points = new List<PointF>();

        foreach (var edge in document.Edges)
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            if (Geometry2D.GetOrthoAlignment(start, end, tolerance) != OrthoAlignment.Vertical)
            {
                continue;
            }

            foreach (var point in new[] { start, end, MathUtils.Midpoint(start, end) })
            {
                if (TopologyService.FindVertex(document, point, tolerance) is null)
                {
                    AddUniquePoint(points, point, tolerance);
                }
                else
                {
                    AddUniquePoint(points, TopologyService.FindVertex(document, point, tolerance)!.Position, tolerance);
                }
            }
        }

        return points;
    }

    private static void AddUniquePoint(List<PointF> points, PointF point, double tolerance)
    {
        foreach (var existing in points)
        {
            if (MathUtils.ArePointsEqual(existing, point, tolerance))
            {
                return;
            }
        }

        points.Add(point);
    }

    private static string FindIntersectionGroupKey(
        Dictionary<string, (PointF Position, HashSet<Guid> EdgeIds)> groups,
        PointF intersection,
        double tolerance)
    {
        foreach (var (key, group) in groups)
        {
            if (MathUtils.ArePointsEqual(group.Position, intersection, tolerance))
            {
                return key;
            }
        }

        return $"{intersection.X}:{intersection.Y}";
    }

    private static long QuantizeEdgeParameter(PointF start, PointF end, PointF point, double tolerance)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= tolerance * tolerance)
        {
            return 0;
        }

        var t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared;
        var scale = 1.0 / Math.Max(tolerance, MathUtils.DefaultTolerance);
        return (long)Math.Round(t * scale);
    }

    private static int GetKindPriority(SnapKind kind)
        => kind switch
        {
            SnapKind.Endpoint => 0,
            SnapKind.Intersection => 1,
            SnapKind.Midpoint => 2,
            SnapKind.OnEdge => 3,
            SnapKind.Alignment => 4,
            _ => 5
        };
}

public readonly struct SnapResult(SnapPoint? snap, bool hasSnap)
{
    public SnapPoint? Snap { get; } = snap;

    public bool HasSnap { get; } = hasSnap;

    public PointF Resolve(PointF fallback) => Snap?.Position ?? fallback;
}
