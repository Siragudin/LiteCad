using LiteCad.Core.Document;
using System.Windows.Media;

namespace LiteCad.Rendering;

public static class DocumentDisplayColors
{
    public static readonly Color DefaultEdgeColor = Color.FromRgb(0x22, 0x22, 0x22);

    public static Color ResolveEdgeColor(Color storedColor, bool forScreenDisplay)
    {
        if (!forScreenDisplay || !IsDefaultEdgeColor(storedColor))
        {
            return storedColor;
        }

        return CanvasTheme.DefaultEdgeDisplay;
    }

    public static Color ResolveFillColor(Color storedColor, bool forScreenDisplay)
    {
        if (!forScreenDisplay || !IsDefaultFillColor(storedColor))
        {
            return storedColor;
        }

        return CanvasTheme.DefaultFaceFillDisplay;
    }

    public static bool IsDefaultEdgeColor(Color color)
        => color.R == DefaultEdgeColor.R
           && color.G == DefaultEdgeColor.G
           && color.B == DefaultEdgeColor.B;

    public static bool IsDefaultFillColor(Color color)
        => color.R == FaceFillStyle.DefaultFillColor.R
           && color.G == FaceFillStyle.DefaultFillColor.G
           && color.B == FaceFillStyle.DefaultFillColor.B;
}
