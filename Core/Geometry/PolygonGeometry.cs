using LiteCad.Core.Document;

namespace LiteCad.Core.Geometry;

public static class PolygonGeometry
{
    public static IReadOnlyList<PointF> GetBoundaryPoints(CadDocument document, Loop loop)
    {
        if (loop.Edges.Count == 0)
        {
            return Array.Empty<PointF>();
        }

        var edgeMap = BuildEdgeMap(document);
        var points = new List<PointF>(loop.Edges.Count);

        foreach (var reference in loop.Edges)
        {
            if (!edgeMap.TryGetValue(reference.EdgeId, out var edge))
            {
                return Array.Empty<PointF>();
            }

            var vertexId = reference.Forward ? edge.StartVertexId : edge.EndVertexId;
            points.Add(TopologyService.GetVertexPosition(document, vertexId));
        }

        return points;
    }

    public static IReadOnlyList<PointF> GetOuterBoundaryPoints(
        CadDocument document,
        Polygon polygon,
        double tolerance = MathUtils.DefaultTolerance)
        => GetBoundaryPoints(document, polygon.OuterLoop);

    public static double GetSignedArea(CadDocument document, Loop loop)
        => MathUtils.SignedPolygonArea(GetBoundaryPoints(document, loop));

    public static double GetArea(
        CadDocument document,
        Polygon polygon,
        double tolerance = MathUtils.DefaultTolerance)
    {
        var outerArea = Math.Abs(GetSignedArea(document, polygon.OuterLoop));
        var holeArea = polygon.InnerLoops.Sum(
            hole => Math.Abs(GetSignedArea(document, hole)));

        return Math.Max(0, outerArea - holeArea);
    }

    public static double GetPerimeter(
        CadDocument document,
        Polygon polygon,
        double tolerance = MathUtils.DefaultTolerance)
    {
        var edgeMap = BuildEdgeMap(document);
        double perimeter = SumLoopEdgeLengths(document, polygon.OuterLoop, edgeMap);

        foreach (var hole in polygon.InnerLoops)
        {
            perimeter += SumLoopEdgeLengths(document, hole, edgeMap);
        }

        return perimeter;
    }

    public static (PointF Min, PointF Max)? GetBounds(
        CadDocument document,
        Polygon polygon,
        double tolerance = MathUtils.DefaultTolerance)
    {
        var points = GetOuterBoundaryPoints(document, polygon, tolerance);
        if (points.Count == 0)
        {
            return null;
        }

        var minX = points.Min(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxX = points.Max(point => point.X);
        var maxY = points.Max(point => point.Y);
        return (new PointF(minX, minY), new PointF(maxX, maxY));
    }

    public static bool ContainsPoint(
        CadDocument document,
        Polygon polygon,
        PointF point,
        double tolerance = MathUtils.DefaultTolerance)
    {
        var outer = GetOuterBoundaryPoints(document, polygon, tolerance);
        if (outer.Count < 3 || !MathUtils.PointInPolygon(point, outer))
        {
            return false;
        }

        foreach (var hole in polygon.InnerLoops)
        {
            var holePoints = GetBoundaryPoints(document, hole);
            if (holePoints.Count >= 3 && MathUtils.PointInPolygon(point, holePoints))
            {
                return false;
            }
        }

        return true;
    }

    public static IEnumerable<Guid> GetAllEdgeIds(Polygon polygon)
    {
        foreach (var reference in polygon.OuterLoop.Edges)
        {
            yield return reference.EdgeId;
        }

        foreach (var hole in polygon.InnerLoops)
        {
            foreach (var reference in hole.Edges)
            {
                yield return reference.EdgeId;
            }
        }
    }

    private static double SumLoopEdgeLengths(
        CadDocument document,
        Loop loop,
        Dictionary<Guid, Edge> edgeMap)
    {
        double length = 0;
        foreach (var reference in loop.Edges)
        {
            if (edgeMap.TryGetValue(reference.EdgeId, out var edge))
            {
                length += MathUtils.Distance(
                    TopologyService.GetEdgeStartPoint(document, edge),
                    TopologyService.GetEdgeEndPoint(document, edge));
            }
        }

        return length;
    }

    private static Dictionary<Guid, Edge> BuildEdgeMap(CadDocument document)
        => document.Edges.ToDictionary(edge => edge.Id);
}
