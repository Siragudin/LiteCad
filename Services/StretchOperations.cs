using LiteCad.Core.Document;
using LiteCad.Core.Geometry;

namespace LiteCad.Services;

public sealed class StretchPlan
{
    public required Guid EdgeId { get; init; }

    public required Guid StartVertexId { get; init; }

    public required Guid EndVertexId { get; init; }

    public required PointF StartPosition { get; init; }

    public required PointF EndPosition { get; init; }

    public required PointF Normal { get; init; }
}

public static class StretchOperations
{
    public static bool TryCreatePlan(CadDocument document, Guid edgeId, double tolerance, out StretchPlan plan)
    {
        plan = null!;

        var edge = document.Edges.FirstOrDefault(item => item.Id == edgeId);
        if (edge is null)
        {
            return false;
        }

        var start = TopologyService.GetEdgeStartPoint(document, edge);
        var end = TopologyService.GetEdgeEndPoint(document, edge);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= tolerance)
        {
            return false;
        }

        if (!ValidateEndpoint(document, edge, edge.StartVertexId, dx, dy, length, tolerance)
            || !ValidateEndpoint(document, edge, edge.EndVertexId, dx, dy, length, tolerance))
        {
            return false;
        }

        var normal = new PointF((float)(-dy / length), (float)(dx / length));
        plan = new StretchPlan
        {
            EdgeId = edge.Id,
            StartVertexId = edge.StartVertexId,
            EndVertexId = edge.EndVertexId,
            StartPosition = start,
            EndPosition = end,
            Normal = normal
        };

        return true;
    }

    public static PointF ProjectDeltaOntoNormal(PointF delta, PointF normal)
    {
        var scalar = delta.X * normal.X + delta.Y * normal.Y;
        return new PointF(normal.X * scalar, normal.Y * scalar);
    }

    public static void ApplyStretch(CadDocument document, StretchPlan plan, PointF delta)
    {
        var move = ProjectDeltaOntoNormal(delta, plan.Normal);

        TopologyService.MoveVertex(
            document,
            plan.StartVertexId,
            new PointF(plan.StartPosition.X + move.X, plan.StartPosition.Y + move.Y));

        TopologyService.MoveVertex(
            document,
            plan.EndVertexId,
            new PointF(plan.EndPosition.X + move.X, plan.EndPosition.Y + move.Y));

        PolygonBuilder.SyncFaces(document, TopologyTolerance.ForMutation);
    }

    private static bool ValidateEndpoint(
        CadDocument document,
        Edge selectedEdge,
        Guid endpointVertexId,
        double edgeDx,
        double edgeDy,
        double edgeLength,
        double tolerance)
    {
        var sideEdges = document.Edges
            .Where(edge => edge.Id != selectedEdge.Id
                && (edge.StartVertexId == endpointVertexId || edge.EndVertexId == endpointVertexId))
            .ToList();

        if (sideEdges.Count == 0)
        {
            return false;
        }

        double? referenceDx = null;
        double? referenceDy = null;
        double? referenceLength = null;

        foreach (var sideEdge in sideEdges)
        {
            var otherVertexId = sideEdge.StartVertexId == endpointVertexId
                ? sideEdge.EndVertexId
                : sideEdge.StartVertexId;

            var endpoint = TopologyService.GetVertexPosition(document, endpointVertexId);
            var other = TopologyService.GetVertexPosition(document, otherVertexId);
            var sideDx = other.X - endpoint.X;
            var sideDy = other.Y - endpoint.Y;
            var sideLength = Math.Sqrt(sideDx * sideDx + sideDy * sideDy);
            if (sideLength <= tolerance)
            {
                return false;
            }

            var dot = Math.Abs(edgeDx * sideDx + edgeDy * sideDy);
            if (dot > tolerance * edgeLength * sideLength)
            {
                return false;
            }

            if (referenceDx is null)
            {
                referenceDx = sideDx;
                referenceDy = sideDy;
                referenceLength = sideLength;
                continue;
            }

            if (!Geometry2D.AreDirectionsParallel(
                    referenceDx.Value,
                    referenceDy.Value,
                    referenceLength!.Value,
                    sideDx,
                    sideDy,
                    sideLength,
                    tolerance))
            {
                return false;
            }
        }

        return true;
    }
}
