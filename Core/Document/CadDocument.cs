namespace LiteCad.Core.Document;

public sealed class CadDocument
{
    public List<Vertex> Vertices { get; } = [];

    public List<Edge> Edges { get; } = [];

    public List<Polygon> Polygons { get; } = [];

    /// <summary>
    /// Geometry-based keys for faces the user explicitly removed.
    /// Not tied to Edge.Id — derived faces can restore when topology is rebuilt.
    /// </summary>
    public HashSet<string> SuppressedFaceGeometryKeys { get; } = new(StringComparer.Ordinal);
}
