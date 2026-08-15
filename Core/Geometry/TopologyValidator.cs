using LiteCad.Core.Document;
using System.Text;

namespace LiteCad.Core.Geometry;

public static class TopologyValidator
{
    public static IReadOnlyList<string> Validate(CadDocument document, double tolerance = MathUtils.DefaultTolerance)
    {
        tolerance = TopologyTolerance.ForMutation;
        var errors = new List<string>();
        var edgeMap = document.Edges.ToDictionary(edge => edge.Id);
        var vertexMap = document.Vertices.ToDictionary(vertex => vertex.Id);

        foreach (var edge in document.Edges)
        {
            if (!vertexMap.ContainsKey(edge.StartVertexId))
            {
                errors.Add($"Edge {edge.Id} references missing StartVertexId {edge.StartVertexId}.");
            }

            if (!vertexMap.ContainsKey(edge.EndVertexId))
            {
                errors.Add($"Edge {edge.Id} references missing EndVertexId {edge.EndVertexId}.");
            }

            if (edge.StartVertexId == edge.EndVertexId)
            {
                errors.Add($"Edge {edge.Id} has identical start and end vertex.");
            }
        }

        foreach (var vertex in document.Vertices)
        {
            if (double.IsNaN(vertex.Position.X) || double.IsNaN(vertex.Position.Y) ||
                double.IsInfinity(vertex.Position.X) || double.IsInfinity(vertex.Position.Y))
            {
                errors.Add($"Vertex {vertex.Id} has invalid position.");
            }
        }

        ValidateDuplicateEdges(document, errors, tolerance);

        foreach (var polygon in document.Polygons)
        {
            if (polygon.Type == PolygonType.Face)
            {
                if (polygon.OuterLoop.Edges.Count == 0)
                {
                    errors.Add($"Face polygon {polygon.Id} has empty OuterLoop.");
                }
                else
                {
                    ValidateLoop(document, polygon.OuterLoop, edgeMap, vertexMap, errors, tolerance, $"Face {polygon.Id} OuterLoop");
                }

                for (var i = 0; i < polygon.InnerLoops.Count; i++)
                {
                    ValidateLoop(document, polygon.InnerLoops[i], edgeMap, vertexMap, errors, tolerance, $"Face {polygon.Id} InnerLoop[{i}]");
                }
            }
            else if (polygon.OuterLoop.Edges.Count > 0)
            {
                ValidateLoop(document, polygon.OuterLoop, edgeMap, vertexMap, errors, tolerance, "OuterLoop");
                for (var i = 0; i < polygon.InnerLoops.Count; i++)
                {
                    ValidateLoop(document, polygon.InnerLoops[i], edgeMap, vertexMap, errors, tolerance, $"InnerLoop[{i}]");
                }
            }
        }

        return errors;
    }

    public static void AssertValid(CadDocument document, double tolerance = MathUtils.DefaultTolerance)
    {
        var errors = Validate(document, tolerance);
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Invalid topology:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    public static bool IsValid(CadDocument document, double tolerance = MathUtils.DefaultTolerance)
        => Validate(document, tolerance).Count == 0;

    private static void ValidateDuplicateEdges(
        CadDocument document,
        List<string> errors,
        double tolerance)
    {
        for (var i = 0; i < document.Edges.Count; i++)
        {
            for (var j = i + 1; j < document.Edges.Count; j++)
            {
                var a = document.Edges[i];
                var b = document.Edges[j];
                var aStart = TopologyService.GetEdgeStartPoint(document, a);
                var aEnd = TopologyService.GetEdgeEndPoint(document, a);
                var bStart = TopologyService.GetEdgeStartPoint(document, b);
                var bEnd = TopologyService.GetEdgeEndPoint(document, b);

                if (SegmentsEqual(aStart, aEnd, bStart, bEnd, tolerance))
                {
                    errors.Add($"Duplicate edge topology between {a.Id} and {b.Id}.");
                }
            }
        }
    }

    private static void ValidateLoop(
        CadDocument document,
        Loop loop,
        Dictionary<Guid, Edge> edgeMap,
        Dictionary<Guid, Vertex> vertexMap,
        List<string> errors,
        double tolerance,
        string label)
    {
        if (loop.Edges.Count == 0)
        {
            errors.Add($"{label} is empty.");
            return;
        }

        Guid? previousEnd = null;
        var firstStart = (Guid?)null;

        for (var i = 0; i < loop.Edges.Count; i++)
        {
            var reference = loop.Edges[i];
            if (!edgeMap.TryGetValue(reference.EdgeId, out var edge))
            {
                errors.Add($"{label} references missing edge {reference.EdgeId}.");
                continue;
            }

            var startVertexId = reference.Forward ? edge.StartVertexId : edge.EndVertexId;
            var endVertexId = reference.Forward ? edge.EndVertexId : edge.StartVertexId;

            if (!vertexMap.ContainsKey(startVertexId) || !vertexMap.ContainsKey(endVertexId))
            {
                errors.Add($"{label} edge {edge.Id} references missing vertex.");
                continue;
            }

            if (i == 0)
            {
                firstStart = startVertexId;
            }
            else if (previousEnd != startVertexId)
            {
                errors.Add($"{label} is not continuous at directed edge index {i}.");
            }

            previousEnd = endVertexId;
        }

        if (firstStart.HasValue && previousEnd != firstStart)
        {
            errors.Add($"{label} is not closed.");
        }
    }

    private static bool SegmentsEqual(
        PointF aStart,
        PointF aEnd,
        PointF bStart,
        PointF bEnd,
        double tolerance)
    {
        return (Geometry2D.ArePointsSame(aStart, bStart, tolerance) &&
                Geometry2D.ArePointsSame(aEnd, bEnd, tolerance)) ||
               (Geometry2D.ArePointsSame(aStart, bEnd, tolerance) &&
                Geometry2D.ArePointsSame(aEnd, bStart, tolerance));
    }
}
