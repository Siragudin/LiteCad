using LiteCad.Core.Document;
using LiteCad.Core.Geometry;

namespace LiteCad.Services;

public sealed class SnapService
{
    private sealed class DocumentSnapCache
    {
        public required int Fingerprint { get; init; }

        public required List<(SnapPoint Snap, SnapIdentity Identity)> StaticCandidates { get; init; }

        public required List<PointF> AlignmentPoints { get; init; }

        public required List<PointF> VerticalAlignmentPoints { get; init; }
    }

    private readonly record struct SnapCandidate(SnapPoint Snap, SnapIdentity Identity, double Distance);

    private DocumentSnapCache? _cache;

    public void InvalidateCache()
        => _cache = null;

    public SnapQueryResult Query(
        CadDocument document,
        PointF cursor,
        double tolerance,
        Guid? excludeEdgeId = null,
        bool includeOnEdge = false)
    {
        var candidates = BuildGeometricCandidates(document, cursor, tolerance, excludeEdgeId, includeOnEdge);
        var nearby = candidates
            .Where(candidate => candidate.Distance <= tolerance)
            .OrderBy(candidate => GetKindPriority(candidate.Identity.Kind))
            .ThenBy(candidate => candidate.Distance)
            .ToList();

        SnapPoint? best = nearby.Count > 0 ? nearby[0].Snap : null;
        var visible = nearby.Select(candidate => candidate.Snap).ToList();
        return new SnapQueryResult(new SnapResult(best, best.HasValue), visible);
    }

    public PointF ResolveDrawingSnap(
        CadDocument document,
        PointF cursor,
        PointF? drawingStart,
        double tolerance,
        bool orthoEnabled,
        bool includeOnEdge)
        => ResolveDrawingSnap(
            document,
            Query(document, cursor, tolerance, includeOnEdge: includeOnEdge),
            cursor,
            drawingStart,
            tolerance,
            orthoEnabled);

    public PointF ResolveDrawingSnap(
        CadDocument document,
        SnapQueryResult query,
        PointF cursor,
        PointF? drawingStart,
        double tolerance,
        bool orthoEnabled)
    {
        var resolved = query.Best.Resolve(cursor);

        if (drawingStart is not PointF start)
        {
            return resolved;
        }

        if (orthoEnabled)
        {
            resolved = Geometry2D.ApplyOrtho(start, resolved);
        }

        if (TrySnapDrawingAlignment(document, start, resolved, tolerance, out var aligned))
        {
            return aligned;
        }

        if (orthoEnabled)
        {
            resolved = Geometry2D.ApplyOrtho(start, query.Best.Resolve(cursor));
        }

        return resolved;
    }

    public SnapResult FindBestSnap(
        CadDocument document,
        PointF cursor,
        double tolerance,
        Guid? excludeEdgeId = null,
        bool includeOnEdge = true)
        => Query(document, cursor, tolerance, excludeEdgeId, includeOnEdge).Best;

    public IReadOnlyList<SnapPoint> GetVisibleSnaps(CadDocument document, PointF cursor, double tolerance)
        => Query(document, cursor, tolerance, includeOnEdge: false).Visible;

    public bool TrySnapDrawingAlignment(
        CadDocument document,
        PointF start,
        PointF target,
        double tolerance,
        out PointF snappedEnd)
    {
        snappedEnd = target;
        tolerance = TopologyTolerance.Resolve(tolerance);

        var alignment = Geometry2D.GetOrthoAlignment(start, target, tolerance);
        if (alignment == OrthoAlignment.None)
        {
            return false;
        }

        var cache = GetOrBuildCache(document, tolerance);
        var referencePoints = alignment == OrthoAlignment.Horizontal
            ? cache.AlignmentPoints
            : cache.VerticalAlignmentPoints;

        var bestDelta = double.MaxValue;

        foreach (var point in referencePoints)
        {
            if (MathUtils.ArePointsEqual(point, start, tolerance))
            {
                continue;
            }

            if (alignment == OrthoAlignment.Horizontal)
            {
                var xDelta = Math.Abs(target.X - point.X);
                if (xDelta > tolerance || xDelta >= bestDelta)
                {
                    continue;
                }

                snappedEnd = new PointF(point.X, start.Y);
                bestDelta = xDelta;
            }
            else
            {
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

    public static bool TryResolveMeasurementAnchor(
        CadDocument document,
        SnapPoint snap,
        double tolerance,
        out Guid vertexId,
        out PointF anchor)
    {
        vertexId = Guid.Empty;
        anchor = snap.Position;

        switch (snap.Kind)
        {
            case SnapKind.Endpoint when snap.VertexId is Guid existingVertexId:
                vertexId = existingVertexId;
                return true;
            case SnapKind.Endpoint:
            case SnapKind.Intersection:
            case SnapKind.AxisIntersection:
                vertexId = TopologyService.FindOrCreateVertex(document, snap.Position, tolerance);
                anchor = TopologyService.GetVertexPosition(document, vertexId);
                return true;
            default:
                return false;
        }
    }

    private IReadOnlyList<SnapCandidate> BuildGeometricCandidates(
        CadDocument document,
        PointF cursor,
        double tolerance,
        Guid? excludeEdgeId,
        bool includeOnEdge)
    {
        tolerance = TopologyTolerance.Resolve(tolerance);
        var cache = GetOrBuildCache(document, tolerance);

        var candidates = new List<(SnapPoint Snap, SnapIdentity Identity)>(cache.StaticCandidates);
        if (includeOnEdge)
        {
            AddOnEdgeCandidates(document, cursor, tolerance, excludeEdgeId, candidates);
        }

        return ResolveDistances(candidates, cursor);
    }

    private DocumentSnapCache GetOrBuildCache(CadDocument document, double tolerance)
    {
        var fingerprint = ComputeDocumentFingerprint(document);
        if (_cache?.Fingerprint == fingerprint)
        {
            return _cache;
        }

        _cache = BuildDocumentSnapCache(document, tolerance, fingerprint);
        return _cache;
    }

    private static DocumentSnapCache BuildDocumentSnapCache(
        CadDocument document,
        double tolerance,
        int fingerprint)
    {
        var raw = CollectStaticCandidates(document, tolerance);
        var canonical = CanonicalizeCandidates(document, raw, tolerance);
        var byIdentity = DeduplicateCandidates(canonical);
        var staticCandidates = DeduplicateByPosition(byIdentity, tolerance);

        return new DocumentSnapCache
        {
            Fingerprint = fingerprint,
            StaticCandidates = staticCandidates,
            AlignmentPoints = BuildAlignmentReferencePoints(document, tolerance),
            VerticalAlignmentPoints = BuildVerticalAlignmentReferencePoints(document, tolerance)
        };
    }

    private static List<(SnapPoint Snap, SnapIdentity Identity)> CollectStaticCandidates(
        CadDocument document,
        double tolerance)
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
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            var midpoint = MathUtils.Midpoint(start, end);

            candidates.Add((
                new SnapPoint(midpoint, SnapKind.Midpoint, edge.Id),
                SnapIdentity.ForMidpoint(edge.Id)));
        }

        foreach (var axis in document.Axes)
        {
            candidates.Add((
                new SnapPoint(axis.Start, SnapKind.Endpoint),
                SnapIdentity.ForAxisEndpoint(axis.Id, isStart: true)));
            candidates.Add((
                new SnapPoint(axis.End, SnapKind.Endpoint),
                SnapIdentity.ForAxisEndpoint(axis.Id, isStart: false)));

            var axisMidpoint = MathUtils.Midpoint(axis.Start, axis.End);
            candidates.Add((
                new SnapPoint(axisMidpoint, SnapKind.Midpoint),
                SnapIdentity.ForAxisMidpoint(axis.Id)));
        }

        AddLinearContactCandidates(document, candidates, tolerance);
        return candidates;
    }

    private static void AddOnEdgeCandidates(
        CadDocument document,
        PointF cursor,
        double tolerance,
        Guid? excludeEdgeId,
        List<(SnapPoint Snap, SnapIdentity Identity)> candidates)
    {
        foreach (var edge in document.Edges)
        {
            if (excludeEdgeId.HasValue && edge.Id == excludeEdgeId.Value)
            {
                continue;
            }

            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            if (!Geometry2D.TryProjectPointOnSegment(cursor, start, end, out var projection, out var distance, tolerance) ||
                distance > tolerance ||
                !Geometry2D.IsPointOnSegmentInterior(projection, start, end, tolerance))
            {
                continue;
            }

            var parameterKey = QuantizeEdgeParameter(start, end, projection, tolerance);
            candidates.Add((
                new SnapPoint(projection, SnapKind.OnEdge, edge.Id),
                SnapIdentity.ForOnEdge(edge.Id, parameterKey)));
        }

        foreach (var axis in document.Axes)
        {
            if (!Geometry2D.TryProjectPointOnSegment(cursor, axis.Start, axis.End, out var projection, out var distance, tolerance) ||
                distance > tolerance ||
                !Geometry2D.IsPointOnSegmentInterior(projection, axis.Start, axis.End, tolerance))
            {
                continue;
            }

            var parameterKey = QuantizeEdgeParameter(axis.Start, axis.End, projection, tolerance);
            candidates.Add((
                new SnapPoint(projection, SnapKind.OnEdge),
                SnapIdentity.ForOnAxis(axis.Id, parameterKey)));
        }
    }

    private static void AddLinearContactCandidates(
        CadDocument document,
        List<(SnapPoint Snap, SnapIdentity Identity)> candidates,
        double tolerance)
    {
        var edgeEdgeGroups = new Dictionary<string, (PointF Position, HashSet<Guid> EdgeIds)>(StringComparer.Ordinal);
        var axisEdgeGroups = new Dictionary<string, (PointF Position, HashSet<Guid> ObjectIds)>(StringComparer.Ordinal);
        var axisAxisGroups = new Dictionary<string, (PointF Position, HashSet<Guid> ObjectIds)>(StringComparer.Ordinal);

        foreach (var edgeA in document.Edges)
        {
            var aStart = TopologyService.GetEdgeStartPoint(document, edgeA);
            var aEnd = TopologyService.GetEdgeEndPoint(document, edgeA);

            foreach (var edgeB in document.Edges)
            {
                if (edgeA.Id.CompareTo(edgeB.Id) >= 0)
                {
                    continue;
                }

                var bStart = TopologyService.GetEdgeStartPoint(document, edgeB);
                var bEnd = TopologyService.GetEdgeEndPoint(document, edgeB);

                foreach (var contact in CollectSegmentContactPoints(aStart, aEnd, bStart, bEnd, tolerance))
                {
                    if (TopologyService.FindVertex(document, contact, tolerance) is not null)
                    {
                        continue;
                    }

                    RegisterEdgeContactGroup(edgeEdgeGroups, contact, edgeA.Id, edgeB.Id, tolerance);
                }
            }
        }

        foreach (var axis in document.Axes)
        {
            foreach (var edge in document.Edges)
            {
                var edgeStart = TopologyService.GetEdgeStartPoint(document, edge);
                var edgeEnd = TopologyService.GetEdgeEndPoint(document, edge);

                foreach (var contact in CollectSegmentContactPoints(axis.Start, axis.End, edgeStart, edgeEnd, tolerance))
                {
                    if (TopologyService.FindVertex(document, contact, tolerance) is not null)
                    {
                        continue;
                    }

                    RegisterIntersectionGroup(axisEdgeGroups, contact, axis.Id, edge.Id, tolerance);
                }
            }

            foreach (var otherAxis in document.Axes)
            {
                if (axis.Id.CompareTo(otherAxis.Id) >= 0)
                {
                    continue;
                }

                foreach (var contact in CollectSegmentContactPoints(
                             axis.Start,
                             axis.End,
                             otherAxis.Start,
                             otherAxis.End,
                             tolerance))
                {
                    RegisterIntersectionGroup(axisAxisGroups, contact, axis.Id, otherAxis.Id, tolerance);
                }
            }
        }

        foreach (var group in edgeEdgeGroups.Values)
        {
            candidates.Add((
                new SnapPoint(group.Position, SnapKind.Intersection),
                SnapIdentity.ForIntersection(group.EdgeIds)));
        }

        foreach (var group in axisEdgeGroups.Values)
        {
            candidates.Add((
                new SnapPoint(group.Position, SnapKind.Intersection),
                SnapIdentity.ForIntersection(group.ObjectIds)));
        }

        foreach (var group in axisAxisGroups.Values)
        {
            candidates.Add((
                new SnapPoint(group.Position, SnapKind.AxisIntersection),
                SnapIdentity.ForAxisIntersection(group.ObjectIds)));
        }
    }

    private static IEnumerable<PointF> CollectSegmentContactPoints(
        PointF aStart,
        PointF aEnd,
        PointF bStart,
        PointF bEnd,
        double tolerance)
    {
        var contacts = new List<PointF>();

        if (Geometry2D.TryGetSegmentIntersection(aStart, aEnd, bStart, bEnd, out var intersection, tolerance))
        {
            AddUniqueContactPoint(contacts, intersection, tolerance);
        }

        TryAddEndpointOnSegmentContact(aStart, aEnd, aStart, contacts, tolerance);
        TryAddEndpointOnSegmentContact(aStart, aEnd, aEnd, contacts, tolerance);
        TryAddEndpointOnSegmentContact(bStart, bEnd, bStart, contacts, tolerance);
        TryAddEndpointOnSegmentContact(bStart, bEnd, bEnd, contacts, tolerance);

        return contacts;
    }

    private static void TryAddEndpointOnSegmentContact(
        PointF hostStart,
        PointF hostEnd,
        PointF endpoint,
        List<PointF> contacts,
        double tolerance)
    {
        if (MathUtils.Distance(hostStart, hostEnd) <= tolerance)
        {
            if (MathUtils.ArePointsEqual(endpoint, hostStart, tolerance))
            {
                AddUniqueContactPoint(contacts, hostStart, tolerance);
            }

            return;
        }

        if (!Geometry2D.TryProjectPointOnSegment(endpoint, hostStart, hostEnd, out var projection, out var distance, tolerance) ||
            distance > tolerance)
        {
            return;
        }

        AddUniqueContactPoint(contacts, projection, tolerance);
    }

    private static void AddUniqueContactPoint(List<PointF> contacts, PointF point, double tolerance)
    {
        foreach (var existing in contacts)
        {
            if (MathUtils.ArePointsEqual(existing, point, tolerance))
            {
                return;
            }
        }

        contacts.Add(point);
    }

    private static void RegisterEdgeContactGroup(
        Dictionary<string, (PointF Position, HashSet<Guid> EdgeIds)> groups,
        PointF contact,
        Guid firstEdgeId,
        Guid secondEdgeId,
        double tolerance)
    {
        var groupKey = FindIntersectionGroupKey(groups, contact, tolerance);
        if (!groups.TryGetValue(groupKey, out var group))
        {
            group = (contact, []);
            groups[groupKey] = group;
        }

        group.EdgeIds.Add(firstEdgeId);
        group.EdgeIds.Add(secondEdgeId);
        groups[groupKey] = group;
    }

    private static void RegisterIntersectionGroup(
        Dictionary<string, (PointF Position, HashSet<Guid> ObjectIds)> groups,
        PointF intersection,
        Guid firstId,
        Guid secondId,
        double tolerance)
    {
        var groupKey = FindGroupedIntersectionKey(groups, intersection, tolerance);
        if (!groups.TryGetValue(groupKey, out var group))
        {
            group = (intersection, []);
            groups[groupKey] = group;
        }

        group.ObjectIds.Add(firstId);
        group.ObjectIds.Add(secondId);
        groups[groupKey] = group;
    }

    private static string FindGroupedIntersectionKey(
        Dictionary<string, (PointF Position, HashSet<Guid> ObjectIds)> groups,
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

        return $"{intersection.X:R}:{intersection.Y:R}";
    }

    private static List<PointF> BuildAlignmentReferencePoints(CadDocument document, double tolerance)
    {
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

        foreach (var axis in document.Axes)
        {
            AddUniquePoint(points, axis.Start, tolerance);
            AddUniquePoint(points, axis.End, tolerance);
            AddUniquePoint(points, MathUtils.Midpoint(axis.Start, axis.End), tolerance);
        }

        foreach (var edgeA in document.Edges)
        {
            var aStart = TopologyService.GetEdgeStartPoint(document, edgeA);
            var aEnd = TopologyService.GetEdgeEndPoint(document, edgeA);

            foreach (var edgeB in document.Edges)
            {
                if (edgeA.Id.CompareTo(edgeB.Id) >= 0)
                {
                    continue;
                }

                var bStart = TopologyService.GetEdgeStartPoint(document, edgeB);
                var bEnd = TopologyService.GetEdgeEndPoint(document, edgeB);

                foreach (var contact in CollectSegmentContactPoints(aStart, aEnd, bStart, bEnd, tolerance))
                {
                    if (TopologyService.FindVertex(document, contact, tolerance) is not null)
                    {
                        continue;
                    }

                    AddUniquePoint(points, contact, tolerance);
                }
            }
        }

        foreach (var axis in document.Axes)
        {
            foreach (var edge in document.Edges)
            {
                var edgeStart = TopologyService.GetEdgeStartPoint(document, edge);
                var edgeEnd = TopologyService.GetEdgeEndPoint(document, edge);

                foreach (var contact in CollectSegmentContactPoints(axis.Start, axis.End, edgeStart, edgeEnd, tolerance))
                {
                    if (TopologyService.FindVertex(document, contact, tolerance) is not null)
                    {
                        continue;
                    }

                    AddUniquePoint(points, contact, tolerance);
                }
            }

            foreach (var otherAxis in document.Axes)
            {
                if (axis.Id.CompareTo(otherAxis.Id) >= 0)
                {
                    continue;
                }

                foreach (var contact in CollectSegmentContactPoints(
                             axis.Start,
                             axis.End,
                             otherAxis.Start,
                             otherAxis.End,
                             tolerance))
                {
                    AddUniquePoint(points, contact, tolerance);
                }
            }
        }

        return points;
    }

    private static List<PointF> BuildVerticalAlignmentReferencePoints(CadDocument document, double tolerance)
    {
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
                var vertex = TopologyService.FindVertex(document, point, tolerance);
                AddUniquePoint(points, vertex?.Position ?? point, tolerance);
            }
        }

        return points;
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

    private static List<(SnapPoint Snap, SnapIdentity Identity)> DeduplicateByPosition(
        IReadOnlyList<(SnapPoint Snap, SnapIdentity Identity)> candidates,
        double tolerance)
    {
        var bestByPosition = new List<(SnapPoint Snap, SnapIdentity Identity)>();

        foreach (var candidate in candidates)
        {
            var existingIndex = -1;
            for (var i = 0; i < bestByPosition.Count; i++)
            {
                if (MathUtils.ArePointsEqual(bestByPosition[i].Snap.Position, candidate.Snap.Position, tolerance))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex < 0)
            {
                bestByPosition.Add(candidate);
                continue;
            }

            var existing = bestByPosition[existingIndex];
            if (ShouldPreferSnapCandidate(candidate, existing))
            {
                bestByPosition[existingIndex] = candidate;
            }
        }

        return bestByPosition;
    }

    private static bool ShouldPreferSnapCandidate(
        (SnapPoint Snap, SnapIdentity Identity) candidate,
        (SnapPoint Snap, SnapIdentity Identity) existing)
    {
        var candidatePriority = GetKindPriority(candidate.Identity.Kind);
        var existingPriority = GetKindPriority(existing.Identity.Kind);
        if (candidatePriority != existingPriority)
        {
            return candidatePriority < existingPriority;
        }

        if (candidate.Snap.VertexId.HasValue && !existing.Snap.VertexId.HasValue)
        {
            return true;
        }

        if (!candidate.Snap.VertexId.HasValue && existing.Snap.VertexId.HasValue)
        {
            return false;
        }

        return false;
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

    private static int ComputeDocumentFingerprint(CadDocument document)
    {
        unchecked
        {
            var hash = document.Vertices.Count;
            hash = (hash * 31) + document.Edges.Count;
            hash = (hash * 31) + document.Axes.Count;
            hash = (hash * 31) + document.Dimensions.Count;

            foreach (var vertex in document.Vertices)
            {
                hash = HashPoint(hash, vertex.Position);
            }

            foreach (var edge in document.Edges)
            {
                hash = (hash * 31) + edge.Id.GetHashCode();
                hash = (hash * 31) + edge.StartVertexId.GetHashCode();
                hash = (hash * 31) + edge.EndVertexId.GetHashCode();
            }

            foreach (var axis in document.Axes)
            {
                hash = (hash * 31) + axis.Id.GetHashCode();
                hash = HashPoint(hash, axis.Start);
                hash = HashPoint(hash, axis.End);
            }

            return hash;
        }
    }

    private static int HashPoint(int hash, PointF point)
    {
        unchecked
        {
            hash = (hash * 31) + point.X.GetHashCode();
            hash = (hash * 31) + point.Y.GetHashCode();
            return hash;
        }
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
            SnapKind.AxisIntersection => 1,
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
