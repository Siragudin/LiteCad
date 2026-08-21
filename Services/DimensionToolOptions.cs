using LiteCad.Dimensions;

namespace LiteCad.Services;

public sealed class DimensionToolOptions
{
    public DimensionExtensionStyle ExtensionStyle { get; set; } = DimensionExtensionStyle.Full;

    public bool OrthoEnabled { get; set; }

    public double TextSize { get; set; } = Dimension.DefaultTextSize;
}
