using LiteCad.Core.Geometry;

namespace LiteCad.Texts;

public sealed class TextNote
{
    public const double MinTextSize = 8.0;

    public const double DefaultTextSize = 12.0;

    public TextNote(
        Guid id,
        TextNoteKind kind,
        PointF origin,
        PointF? arrowTip,
        string text,
        double textSize)
    {
        Id = id;
        Kind = kind;
        Origin = origin;
        ArrowTip = arrowTip;
        Text = text ?? string.Empty;
        TextSize = NormalizeTextSize(textSize);
    }

    public Guid Id { get; }

    public TextNoteKind Kind { get; }

    public PointF Origin { get; set; }

    public PointF? ArrowTip { get; set; }

    public string Text { get; set; }

    public double TextSize { get; set; }

    public static double NormalizeTextSize(double textSize)
        => double.IsFinite(textSize) && textSize >= MinTextSize ? textSize : DefaultTextSize;

    public TextNote Clone()
        => new(Id, Kind, Origin, ArrowTip, Text, TextSize);
}
