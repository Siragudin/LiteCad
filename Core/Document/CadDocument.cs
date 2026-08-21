using LiteCad.Dimensions;

namespace LiteCad.Core.Document;

public sealed class CadDocument
{
    public List<Vertex> Vertices { get; } = [];

    public List<Edge> Edges { get; } = [];

    public List<Polygon> Polygons { get; } = [];

    public List<Dimension> Dimensions { get; } = [];

    public List<Axis> Axes { get; } = [];

    /// <summary>
    /// Geometry-based keys for faces the user explicitly removed.
    /// Not tied to Edge.Id — derived faces can restore when topology is rebuilt.
    /// </summary>
    public HashSet<string> SuppressedFaceGeometryKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Visual fill styles for derived faces, keyed by stable face geometry identity.
    /// Not part of topology — metadata only.
    /// </summary>
    public Dictionary<string, FaceFillStyle> FaceFillStyles { get; } = new(StringComparer.Ordinal);
}
