namespace LiteCad.Core.Document;

public sealed class Polygon
{
    public Guid Id { get; } = Guid.NewGuid();

    public PolygonType Type { get; set; } = PolygonType.Face;

    public Loop OuterLoop { get; set; } = new();

    public List<Loop> InnerLoops { get; } = [];

    /// <summary>
    /// Read-only outer boundary edge ids derived from <see cref="OuterLoop"/>.
    /// </summary>
    public IReadOnlyList<Guid> EdgeIds
        => OuterLoop.Edges.Select(reference => reference.EdgeId).ToArray();

    /// <summary>
    /// Read-only hole edge-id lists derived from <see cref="InnerLoops"/>.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<Guid>> HoleEdgeIds
        => InnerLoops
            .Select(loop => (IReadOnlyList<Guid>)loop.Edges.Select(reference => reference.EdgeId).ToArray())
            .ToArray();
}
