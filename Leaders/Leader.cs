using LiteCad.Core.Geometry;

namespace LiteCad.Leaders;

public sealed class Leader
{
    public Leader(
        Guid id,
        LeaderKind kind,
        PointF target,
        PointF textPosition,
        string text)
    {
        Id = id;
        Kind = kind;
        Target = target;
        TextPosition = textPosition;
        Text = text ?? string.Empty;
    }

    public Guid Id { get; }

    public LeaderKind Kind { get; }

    public PointF Target { get; set; }

    public PointF TextPosition { get; set; }

    public string Text { get; set; }

    public Leader Clone()
        => new(Id, Kind, Target, TextPosition, Text);
}
