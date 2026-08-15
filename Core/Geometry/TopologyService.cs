using LiteCad.Core.Document;

namespace LiteCad.Core.Geometry;

public static class TopologyService
{
    public static Vertex? FindVertex(CadDocument document, PointF position, double tolerance)
    {
        tolerance = TopologyTolerance.Resolve(tolerance);
        foreach (var vertex in document.Vertices)
        {
            if (MathUtils.ArePointsEqual(vertex.Position, position, tolerance))
            {
                return vertex;
            }
        }

        return null;
    }

    public static Guid FindOrCreateVertex(CadDocument document, PointF position, double tolerance)
    {
        tolerance = TopologyTolerance.ForMutation;
        var existing = FindVertex(document, position, tolerance);
        if (existing is not null)
        {
            return existing.Id;
        }

        var vertex = new Vertex(position);
        document.Vertices.Add(vertex);
        return vertex.Id;
    }

    public static Vertex GetVertex(CadDocument document, Guid vertexId)
    {
        var vertex = document.Vertices.FirstOrDefault(item => item.Id == vertexId);
        if (vertex is null)
        {
            throw new InvalidOperationException($"Vertex {vertexId} was not found.");
        }

        return vertex;
    }

    public static bool TryGetVertex(CadDocument document, Guid vertexId, out Vertex vertex)
    {
        vertex = document.Vertices.FirstOrDefault(item => item.Id == vertexId)!;
        return vertex is not null;
    }

    public static PointF GetVertexPosition(CadDocument document, Guid vertexId)
        => GetVertex(document, vertexId).Position;

    public static PointF GetEdgeStartPoint(CadDocument document, Edge edge)
        => GetVertexPosition(document, edge.StartVertexId);

    public static PointF GetEdgeEndPoint(CadDocument document, Edge edge)
        => GetVertexPosition(document, edge.EndVertexId);

    public static Edge CreateEdge(CadDocument document, Guid startVertexId, Guid endVertexId, Edge template)
    {
        if (startVertexId == endVertexId)
        {
            throw new InvalidOperationException("Cannot create a zero-length edge.");
        }

        _ = GetVertex(document, startVertexId);
        _ = GetVertex(document, endVertexId);

        var edge = template.CloneGeometry();
        edge.StartVertexId = startVertexId;
        edge.EndVertexId = endVertexId;
        document.Edges.Add(edge);
        return edge;
    }

    public static void DeleteEdge(CadDocument document, Edge edge)
        => document.Edges.Remove(edge);

    public static void MoveVertex(CadDocument document, Guid vertexId, PointF newPosition)
        => GetVertex(document, vertexId).Position = newPosition;

    public static void SplitEdge(CadDocument document, Edge edge, PointF point, double tolerance)
    {
        tolerance = TopologyTolerance.ForMutation;
        var start = GetEdgeStartPoint(document, edge);
        var end = GetEdgeEndPoint(document, edge);

        if (!Geometry2D.IsPointOnSegmentInterior(point, start, end, tolerance))
        {
            return;
        }

        var splitVertexId = FindOrCreateVertex(document, point, tolerance);
        var startVertexId = edge.StartVertexId;
        var endVertexId = edge.EndVertexId;

        DeleteEdge(document, edge);

        CreateEdge(document, startVertexId, splitVertexId, edge.CloneGeometry());
        CreateEdge(document, splitVertexId, endVertexId, edge.CloneGeometry());
    }

    public static Edge CreateEdgeFromPoints(
        CadDocument document,
        PointF start,
        PointF end,
        Edge template,
        double tolerance)
    {
        var startVertexId = FindOrCreateVertex(document, start, tolerance);
        var endVertexId = FindOrCreateVertex(document, end, tolerance);
        return CreateEdge(document, startVertexId, endVertexId, template);
    }

    public static void PruneUnusedVertices(CadDocument document)
    {
        var used = new HashSet<Guid>();
        foreach (var edge in document.Edges)
        {
            used.Add(edge.StartVertexId);
            used.Add(edge.EndVertexId);
        }

        document.Vertices.RemoveAll(vertex => !used.Contains(vertex.Id));
    }
}
