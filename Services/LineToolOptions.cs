using LiteCad.Core.Document;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Services;

public sealed class LineToolOptions
{
    public Color Color { get; set; } = Color.FromRgb(0x22, 0x22, 0x22);

    public double Thickness { get; set; } = 1.5;

    public EdgeLineType LineType { get; set; } = EdgeLineType.Solid;

    public bool OrthoEnabled { get; set; }
}
