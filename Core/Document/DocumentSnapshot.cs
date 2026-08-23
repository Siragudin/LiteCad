using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Leaders;

namespace LiteCad.Core.Document;

public sealed class DocumentSnapshot
{
    private DocumentSnapshot(
        List<Vertex> vertices,
        List<Edge> edges,
        List<Polygon> userPolygons,
        List<Dimension> dimensions,
        List<Axis> axes,
        List<Leader> leaders,
        double? elevationBaseY,
        HashSet<string> suppressedFaceGeometryKeys,
        Dictionary<string, FaceFillStyle> faceFillStyles)
    {
        Vertices = vertices;
        Edges = edges;
        UserPolygons = userPolygons;
        Dimensions = dimensions;
        Axes = axes;
        Leaders = leaders;
        ElevationBaseY = elevationBaseY;
        SuppressedFaceGeometryKeys = suppressedFaceGeometryKeys;
        FaceFillStyles = faceFillStyles;
    }

    public List<Vertex> Vertices { get; }

    public List<Edge> Edges { get; }

    /// <summary>
    /// User-authored polygons only. Derived <see cref="PolygonType.Face"/> polygons are excluded.
    /// </summary>
    public List<Polygon> UserPolygons { get; }

    public List<Dimension> Dimensions { get; }

    public List<Axis> Axes { get; }

    public List<Leader> Leaders { get; }

    public double? ElevationBaseY { get; }

    public HashSet<string> SuppressedFaceGeometryKeys { get; }

    public Dictionary<string, FaceFillStyle> FaceFillStyles { get; }

    public static DocumentSnapshot Capture(CadDocument document)
    {
        var vertices = document.Vertices.Select(vertex => vertex.Clone()).ToList();
        var edges = document.Edges.Select(CloneEdge).ToList();
        var userPolygons = document.Polygons
            .Where(polygon => polygon.Type != PolygonType.Face)
            .Select(ClonePolygon)
            .ToList();
        var dimensions = document.Dimensions.Select(dimension => dimension.Clone()).ToList();
        var axes = document.Axes.Select(axis => axis.Clone()).ToList();
        var leaders = document.Leaders.Select(leader => leader.Clone()).ToList();
        var suppressedFaceGeometryKeys = new HashSet<string>(
            document.SuppressedFaceGeometryKeys,
            StringComparer.Ordinal);
        var faceFillStyles = document.FaceFillStyles.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.Ordinal);

        return new DocumentSnapshot(
            vertices,
            edges,
            userPolygons,
            dimensions,
            axes,
            leaders,
            document.ElevationBaseY,
            suppressedFaceGeometryKeys,
            faceFillStyles);
    }

    public void Restore(CadDocument document, double tolerance)
    {
        _ = tolerance;
        document.Vertices.Clear();
        document.Edges.Clear();
        document.Polygons.Clear();
        document.Dimensions.Clear();
        document.Axes.Clear();
        document.Leaders.Clear();
        document.ElevationBaseY = null;
        document.SuppressedFaceGeometryKeys.Clear();
        document.FaceFillStyles.Clear();

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

        foreach (var dimension in Dimensions)
        {
            document.Dimensions.Add(dimension.Clone());
        }

        foreach (var axis in Axes)
        {
            document.Axes.Add(axis.Clone());
        }

        foreach (var leader in Leaders)
        {
            document.Leaders.Add(leader.Clone());
        }

        document.ElevationBaseY = ElevationBaseY;

        foreach (var key in SuppressedFaceGeometryKeys)
        {
            document.SuppressedFaceGeometryKeys.Add(key);
        }

        foreach (var (key, style) in FaceFillStyles)
        {
            document.FaceFillStyles[key] = style.Clone();
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
