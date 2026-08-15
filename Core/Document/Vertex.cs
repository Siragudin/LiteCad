using LiteCad.Core.Geometry;

namespace LiteCad.Core.Document;

public sealed class Vertex
{
    public Guid Id { get; }

    public PointF Position { get; set; }

    public Vertex(PointF position)
        : this(Guid.NewGuid(), position)
    {
    }

    public Vertex(Guid id, PointF position)
    {
        Id = id;
        Position = position;
    }

    public Vertex Clone()
        => new(Id, Position);
}
