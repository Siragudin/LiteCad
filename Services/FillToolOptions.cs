using LiteCad.Core.Document;
using System.Windows.Media;

namespace LiteCad.Services;

public sealed class FillToolOptions
{
    public Color FillColor { get; set; } = FaceFillStyle.DefaultFillColor;

    public FaceFillPattern FillPattern { get; set; } = FaceFillPattern.Solid;
}
