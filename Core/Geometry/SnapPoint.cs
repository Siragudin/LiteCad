using LiteCad.Core.Document;

namespace LiteCad.Core.Geometry;

public enum SnapKind
{
    Endpoint,
    Midpoint,
    Intersection,
    OnEdge,
    Alignment
}

public readonly struct SnapPoint(
    PointF position,
    SnapKind kind,
    Guid? edgeId = null,
    Guid? vertexId = null)
{
    public PointF Position { get; } = position;

    public SnapKind Kind { get; } = kind;

    public Guid? EdgeId { get; } = edgeId;

    public Guid? VertexId { get; } = vertexId;
}
