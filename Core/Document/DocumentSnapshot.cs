using LiteCad.Core.Geometry;

namespace LiteCad.Core.Document;

public sealed class DocumentSnapshot
{
    private DocumentSnapshot(
        List<Vertex> vertices,
        List<Edge> edges,
        List<Polygon> userPolygons,
        HashSet<string> suppressedFaceGeometryKeys)
    {
        Vertices = vertices;
        Edges = edges;
        UserPolygons = userPolygons;
        SuppressedFaceGeometryKeys = suppressedFaceGeometryKeys;
    }

    public List<Vertex> Vertices { get; }

    public List<Edge> Edges { get; }

    /// <summary>
    /// User-authored polygons only. Derived <see cref="PolygonType.Face"/> polygons are excluded.
    /// </summary>
    public List<Polygon> UserPolygons { get; }

    public HashSet<string> SuppressedFaceGeometryKeys { get; }

    public static DocumentSnapshot Capture(CadDocument document)
    {
        var vertices = document.Vertices.Select(vertex => vertex.Clone()).ToList();
        var edges = document.Edges.Select(CloneEdge).ToList();
        var userPolygons = document.Polygons
            .Where(polygon => polygon.Type != PolygonType.Face)
            .Select(ClonePolygon)
            .ToList();
        var suppressedFaceGeometryKeys = new HashSet<string>(
            document.SuppressedFaceGeometryKeys,
            StringComparer.Ordinal);

        return new DocumentSnapshot(vertices, edges, userPolygons, suppressedFaceGeometryKeys);
    }

    public void Restore(CadDocument document, double tolerance)
    {
        _ = tolerance;
        document.Vertices.Clear();
        document.Edges.Clear();
        document.Polygons.Clear();
        document.SuppressedFaceGeometryKeys.Clear();

        foreach (var vertex in Vertices)
        {
            document.Vertices.Add(vertex.Clone());
        }

        foreach (var edge in Edges)
        {
            document.Edges.Add(CloneEdge(edge));
        }

        foreach (var polygon in UserPolygons)
        {
            document.Polygons.Add(ClonePolygon(polygon));
        }

        foreach (var key in SuppressedFaceGeometryKeys)
        {
            document.SuppressedFaceGeometryKeys.Add(key);
        }

        PolygonBuilder.SyncFaces(document, TopologyTolerance.ForMutation);
    }

    private static Edge CloneEdge(Edge edge)
    {
        var clone = new Edge(edge.Id, edge.StartVertexId, edge.EndVertexId)
        {
            IsAxis = edge.IsAxis,
            Color = edge.Color,
            Thickness = edge.Thickness,
            LineType = edge.LineType,
            OrthoAlignment = edge.OrthoAlignment
        };

        return clone;
    }

    private static Polygon ClonePolygon(Polygon polygon)
    {
        var clone = new Polygon { Type = polygon.Type };
        clone.OuterLoop.Edges.AddRange(polygon.OuterLoop.Edges);
        foreach (var innerLoop in polygon.InnerLoops)
        {
            var loopClone = new Loop();
            loopClone.Edges.AddRange(innerLoop.Edges);
            clone.InnerLoops.Add(loopClone);
        }

        return clone;
    }
}
