using System.Windows.Media;

namespace LiteCad.Core.Document;

public sealed class FaceFillStyle
{
    public static readonly Color DefaultFillColor = Color.FromRgb(0xF0, 0xF0, 0xF0);

    public static FaceFillStyle Default { get; } = new()
    {
        FillColor = DefaultFillColor,
        FillPattern = FaceFillPattern.Solid
    };

    public Color FillColor { get; set; } = DefaultFillColor;

    public FaceFillPattern FillPattern { get; set; } = FaceFillPattern.Solid;

    public FaceFillStyle Clone()
        => new()
        {
            FillColor = FillColor,
            FillPattern = FillPattern
        };
}
