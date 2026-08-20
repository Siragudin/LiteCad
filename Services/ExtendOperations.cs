using LiteCad.Core.Document;
using LiteCad.Core.Geometry;

namespace LiteCad.Services;

public sealed class ExtendPlan
{
    public required Guid EdgeId { get; init; }

    public required bool ExtendStartVertex { get; init; }

    public required PointF AnchorPoint { get; init; }

    public required PointF ExtendPoint { get; init; }

    public required PointF TargetPoint { get; init; }

    public Guid? IntersectedEdgeId { get; init; }
}

public static class ExtendOperations
{
    private const double DirectionAmbiguityRatio = 0.15;

    public static bool TryBuildPlan(
        CadDocument document,
        Guid edgeId,
        PointF cursorWorld,
        double tolerance,
        out ExtendPlan plan)
    {
        plan = null!;
        tolerance = TopologyTolerance.ForMutation;

        var edge = document.Edges.FirstOrDefault(item => item.Id == edgeId);
        if (edge is null)
        {
            return false;
        }

        var start = TopologyService.GetEdgeStartPoint(document, edge);
        var end = TopologyService.GetEdgeEndPoint(document, edge);
        var edgeLength = MathUtils.Distance(start, end);
        if (edgeLength <= tolerance)
        {
            return false;
        }

        if (!TryResolveExtendEnd(start, end, cursorWorld, edgeLength, tolerance, out var extendStartVertex))
        {
            return false;
        }

        var anchor = extendStartVertex ? end : start;
        var extendPoint = extendStartVertex ? start : end;

        if (!TryFindNearestForwardIntersection(
                document,
                edgeId,
                anchor,
                extendPoint,
                tolerance,
                out var target,
                out var intersectedEdgeId))
        {
            return false;
        }

        if (MathUtils.Distance(extendPoint, target) <= tolerance)
        {
            return false;
        }

        plan = new ExtendPlan
        {
            EdgeId = edgeId,
            ExtendStartVertex = extendStartVertex,
            AnchorPoint = anchor,
            ExtendPoint = extendPoint,
            TargetPoint = target,
            IntersectedEdgeId = intersectedEdgeId
        };

        return true;
    }

    public static bool ApplyExtend(CadDocument document, ExtendPlan plan)
    {
        var tolerance = TopologyTolerance.ForMutation;
        var edge = document.Edges.FirstOrDefault(item => item.Id == plan.EdgeId);
        if (edge is null)
        {
            return false;
        }

        if (MathUtils.Distance(plan.ExtendPoint, plan.TargetPoint) <= tolerance)
        {
            return false;
        }

        var didSplit = false;
        if (plan.IntersectedEdgeId is Guid intersectedEdgeId)
        {
            var intersectedEdge = document.Edges.FirstOrDefault(item => item.Id == intersectedEdgeId);
            if (intersectedEdge is not null)
            {
                var segStart = TopologyService.GetEdgeStartPoint(document, intersectedEdge);
                var segEnd = TopologyService.GetEdgeEndPoint(document, intersectedEdge);
                if (Geometry2D.IsPointOnSegmentInterior(plan.TargetPoint, segStart, segEnd, tolerance))
                {
                    TopologyService.SplitEdge(document, intersectedEdge, plan.TargetPoint, tolerance);
                    didSplit = true;
                }
            }
        }

        if (didSplit)
        {
            PolygonBuilder.InvalidateSuppressionOnEdgeTopologyChange(document);
        }

        var targetVertexId = TopologyService.FindOrCreateVertex(document, plan.TargetPoint, tolerance);
        if (plan.ExtendStartVertex)
        {
            edge.StartVertexId = targetVertexId;
        }
        else
        {
            edge.EndVertexId = targetVertexId;
        }

        TopologyService.PruneUnusedVertices(document);
        PolygonBuilder.SyncFaces(document, tolerance);
        return true;
    }

    private static bool TryResolveExtendEnd(
        PointF start,
        PointF end,
        PointF cursorWorld,
        double edgeLength,
        double tolerance,
        out bool extendStartVertex)
    {
        extendStartVertex = false;
        var distanceToStart = MathUtils.Distance(cursorWorld, start);
        var distanceToEnd = MathUtils.Distance(cursorWorld, end);
        var ambiguity = edgeLength * DirectionAmbiguityRatio;
        if (Math.Abs(distanceToStart - distanceToEnd) <= ambiguity)
        {
            return false;
        }

        extendStartVertex = distanceToStart < distanceToEnd;
        return true;
    }

    private static bool TryFindNearestForwardIntersection(
        CadDocument document,
        Guid sourceEdgeId,
        PointF anchor,
        PointF extendPoint,
        double tolerance,
        out PointF target,
        out Guid? intersectedEdgeId)
    {
        target = PointF.Zero;
        intersectedEdgeId = null;

        var direction = NormalizeOutwardDirection(anchor, extendPoint, tolerance);
        var directionLength = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        if (directionLength <= tolerance)
        {
            return false;
        }

        var bestDistance = double.PositiveInfinity;
        var found = false;

        foreach (var edge in document.Edges)
        {
            if (edge.Id == sourceEdgeId)
            {
                continue;
            }

            var segStart = TopologyService.GetEdgeStartPoint(document, edge);
            var segEnd = TopologyService.GetEdgeEndPoint(document, edge);

            if (!TryGetForwardIntersection(
                    anchor,
                    extendPoint,
                    direction,
                    segStart,
                    segEnd,
                    tolerance,
                    out var intersection,
                    out var rayDistance))
            {
                continue;
            }

            if (rayDistance >= bestDistance)
            {
                continue;
            }

            bestDistance = rayDistance;
            target = intersection;
            intersectedEdgeId = edge.Id;
            found = true;
        }

        return found;
    }

    private static PointF NormalizeOutwardDirection(PointF anchor, PointF extendPoint, double tolerance)
    {
        var dx = extendPoint.X - anchor.X;
        var dy = extendPoint.Y - anchor.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= tolerance)
        {
            return PointF.Zero;
        }

        return new PointF(dx / (float)length, dy / (float)length);
    }

    private static bool TryGetForwardIntersection(
        PointF anchor,
        PointF extendPoint,
        PointF direction,
        PointF segStart,
        PointF segEnd,
        double tolerance,
        out PointF intersection,
        out double rayDistance)
    {
        intersection = PointF.Zero;
        rayDistance = double.PositiveInfinity;

        if (TryGetRaySegmentIntersection(extendPoint, direction, segStart, segEnd, tolerance, out intersection, out rayDistance))
        {
            return IsBeyondExtendPoint(anchor, extendPoint, intersection, tolerance);
        }

        if (Geometry2D.AreSegmentsCollinear(extendPoint, GetPointAhead(extendPoint, direction, 1), segStart, segEnd, tolerance)
            && TryGetCollinearRayIntersection(extendPoint, direction, segStart, segEnd, tolerance, out intersection, out rayDistance))
        {
            return IsBeyondExtendPoint(anchor, extendPoint, intersection, tolerance);
        }

        intersection = PointF.Zero;
        rayDistance = double.PositiveInfinity;
        return false;
    }

    private static bool IsBeyondExtendPoint(PointF anchor, PointF extendPoint, PointF candidate, double tolerance)
    {
        if (Geometry2D.IsPointOnSegmentInterior(candidate, anchor, extendPoint, tolerance))
        {
            return false;
        }

        var parameter = Geometry2D.GetSegmentParameter(candidate, anchor, extendPoint, tolerance);
        return parameter > 1 + tolerance / Math.Max(MathUtils.Distance(anchor, extendPoint), tolerance);
    }

    private static PointF GetPointAhead(PointF origin, PointF direction, double distance)
        => new(origin.X + direction.X * (float)distance, origin.Y + direction.Y * (float)distance);

    private static bool TryGetRaySegmentIntersection(
        PointF rayOrigin,
        PointF rayDirection,
        PointF segStart,
        PointF segEnd,
        double tolerance,
        out PointF intersection,
        out double rayDistance)
    {
        intersection = PointF.Zero;
        rayDistance = double.PositiveInfinity;

        var segDx = segEnd.X - segStart.X;
        var segDy = segEnd.Y - segStart.Y;
        var denominator = rayDirection.X * segDy - rayDirection.Y * segDx;
        if (Math.Abs(denominator) <= tolerance * tolerance)
        {
            return false;
        }

        var offsetX = segStart.X - rayOrigin.X;
        var offsetY = segStart.Y - rayOrigin.Y;
        var rayT = (offsetX * segDy - offsetY * segDx) / denominator;
        var segU = (offsetX * rayDirection.Y - offsetY * rayDirection.X) / denominator;

        if (rayT <= tolerance || segU < -tolerance || segU > 1 + tolerance)
        {
            return false;
        }

        intersection = new PointF(
            rayOrigin.X + rayDirection.X * (float)rayT,
            rayOrigin.Y + rayDirection.Y * (float)rayT);
        rayDistance = rayT;
        return true;
    }

    private static bool TryGetCollinearRayIntersection(
        PointF rayOrigin,
        PointF rayDirection,
        PointF segStart,
        PointF segEnd,
        double tolerance,
        out PointF intersection,
        out double rayDistance)
    {
        intersection = PointF.Zero;
        rayDistance = double.PositiveInfinity;

        var tStart = ProjectOntoRay(segStart, rayOrigin, rayDirection);
        var tEnd = ProjectOntoRay(segEnd, rayOrigin, rayDirection);
        var tMin = Math.Min(tStart, tEnd);
        var tMax = Math.Max(tStart, tEnd);

        if (tMax <= tolerance)
        {
            return false;
        }

        var hitT = Math.Max(tolerance, tMin);
        if (hitT > tMax + tolerance)
        {
            return false;
        }

        rayDistance = hitT;
        intersection = new PointF(
            rayOrigin.X + rayDirection.X * (float)hitT,
            rayOrigin.Y + rayDirection.Y * (float)hitT);
        return true;
    }

    private static double ProjectOntoRay(PointF point, PointF rayOrigin, PointF rayDirection)
    {
        var dx = point.X - rayOrigin.X;
        var dy = point.Y - rayOrigin.Y;
        return dx * rayDirection.X + dy * rayDirection.Y;
    }
}
