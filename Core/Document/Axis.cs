using LiteCad.Core.Geometry;

namespace LiteCad.Core.Document;

public sealed class Axis
{
    public Axis(PointF start, PointF end)
        : this(Guid.NewGuid(), start, end)
    {
    }

    public Axis(Guid id, PointF start, PointF end)
    {
        Id = id;
        Start = start;
        End = end;
    }

    public Guid Id { get; }

    public PointF Start { get; set; }

    public PointF End { get; set; }

    public Axis Clone()
        => new(Id, Start, End);
}
