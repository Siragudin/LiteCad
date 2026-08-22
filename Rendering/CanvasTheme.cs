using LiteCad.Infrastructure;
using System.Windows.Media;

namespace LiteCad.Rendering;

public static class CanvasTheme
{
    public static bool IsDark { get; private set; }

    public static Color CanvasBackground { get; private set; } = Color.FromRgb(0xFF, 0xFF, 0xFF);

    public static Color GridMinor { get; private set; } = Color.FromRgb(0xEC, 0xEC, 0xEC);

    public static Color GridMajor { get; private set; } = Color.FromRgb(0xD0, 0xD0, 0xD0);

    public static Color GridOrigin { get; private set; } = Color.FromRgb(0xB0, 0xB0, 0xB0);

    public static Color DefaultEdgeDisplay { get; private set; } = Color.FromRgb(0x22, 0x22, 0x22);

    public static Color DefaultFaceFillDisplay { get; private set; } = Color.FromRgb(0xF0, 0xF0, 0xF0);

    public static Color SelectionFill { get; private set; } = Color.FromArgb(0x40, 0x21, 0x96, 0xF3);

    public static Color SelectionStroke { get; private set; } = Color.FromRgb(0x21, 0x96, 0xF3);

    public static Color Snap { get; private set; } = Color.FromRgb(0x21, 0x96, 0xF3);

    public static Color DimensionNormal { get; private set; } = Color.FromRgb(0x15, 0x65, 0xC0);

    public static Color DimensionSelected { get; private set; } = Color.FromRgb(0x1E, 0x88, 0xE5);

    public static Color Preview { get; private set; } = Color.FromRgb(0x43, 0xA0, 0x47);

    public static Color PreviewForeground { get; private set; } = Color.FromRgb(0x22, 0x22, 0x22);

    public static Color PreviewGhost { get; private set; } = Color.FromArgb(0xB0, 0x21, 0x96, 0xF3);

    public static Color MarqueeWindowFill { get; private set; } = Color.FromArgb(0x30, 0x21, 0x96, 0xF3);

    public static Color MarqueeWindowStroke { get; private set; } = Color.FromRgb(0x21, 0x96, 0xF3);

    public static Color MarqueeCrossFill { get; private set; } = Color.FromArgb(0x30, 0x4C, 0xAF, 0x50);

    public static Color MarqueeCrossStroke { get; private set; } = Color.FromRgb(0x4C, 0xAF, 0x50);

    public static Color VertexHandleFill { get; private set; } = Colors.White;

    public static Color VertexHandleStroke { get; private set; } = Color.FromRgb(0x21, 0x96, 0xF3);

    public static Color VertexHandleHoverFill { get; private set; } = Color.FromRgb(0xE3, 0xF2, 0xFD);

    public static Color VertexHandleHoverStroke { get; private set; } = Color.FromRgb(0x19, 0x76, 0xD2);

    public static Color VertexHandleSelectedFill { get; private set; } = Color.FromRgb(0xBB, 0xDE, 0xFB);

    public static Color VertexHandleSelectedStroke { get; private set; } = Color.FromRgb(0x15, 0x65, 0xC0);

    public static event Action? Changed;

    public static void Apply(string theme)
    {
        IsDark = AppTheme.IsDark(theme);
        if (IsDark)
        {
            CanvasBackground = Color.FromRgb(0x1E, 0x1E, 0x1E);
            GridMinor = Color.FromRgb(0x33, 0x33, 0x33);
            GridMajor = Color.FromRgb(0x44, 0x44, 0x44);
            GridOrigin = Color.FromRgb(0x55, 0x55, 0x55);
            DefaultEdgeDisplay = Color.FromRgb(0xE0, 0xE0, 0xE0);
            DefaultFaceFillDisplay = Color.FromRgb(0x3A, 0x3A, 0x3A);
            SelectionFill = Color.FromArgb(0x40, 0x37, 0x94, 0xFF);
            SelectionStroke = Color.FromRgb(0x37, 0x94, 0xFF);
            Snap = Color.FromRgb(0x37, 0x94, 0xFF);
            DimensionNormal = Color.FromRgb(0x5B, 0xA3, 0xFF);
            DimensionSelected = Color.FromRgb(0x82, 0xBF, 0xFF);
            Preview = Color.FromRgb(0xF5, 0xF5, 0xF5);
            PreviewForeground = Color.FromRgb(0xF5, 0xF5, 0xF5);
            PreviewGhost = Color.FromArgb(0xD0, 0xF5, 0xF5, 0xF5);
            MarqueeWindowFill = Color.FromArgb(0x30, 0x37, 0x94, 0xFF);
            MarqueeWindowStroke = Color.FromRgb(0x37, 0x94, 0xFF);
            MarqueeCrossFill = Color.FromArgb(0x30, 0x66, 0xBB, 0x6A);
            MarqueeCrossStroke = Color.FromRgb(0x66, 0xBB, 0x6A);
            VertexHandleFill = Color.FromRgb(0x2D, 0x2D, 0x30);
            VertexHandleStroke = Color.FromRgb(0x37, 0x94, 0xFF);
            VertexHandleHoverFill = Color.FromRgb(0x26, 0x4F, 0x78);
            VertexHandleHoverStroke = Color.FromRgb(0x82, 0xBF, 0xFF);
            VertexHandleSelectedFill = Color.FromRgb(0x37, 0x94, 0xFF);
            VertexHandleSelectedStroke = Color.FromRgb(0x82, 0xBF, 0xFF);
        }
        else
        {
            CanvasBackground = Color.FromRgb(0xFF, 0xFF, 0xFF);
            GridMinor = Color.FromRgb(0xEC, 0xEC, 0xEC);
            GridMajor = Color.FromRgb(0xD0, 0xD0, 0xD0);
            GridOrigin = Color.FromRgb(0xB0, 0xB0, 0xB0);
            DefaultEdgeDisplay = Color.FromRgb(0x22, 0x22, 0x22);
            DefaultFaceFillDisplay = Color.FromRgb(0xF0, 0xF0, 0xF0);
            SelectionFill = Color.FromArgb(0x40, 0x21, 0x96, 0xF3);
            SelectionStroke = Color.FromRgb(0x21, 0x96, 0xF3);
            Snap = Color.FromRgb(0x21, 0x96, 0xF3);
            DimensionNormal = Color.FromRgb(0x15, 0x65, 0xC0);
            DimensionSelected = Color.FromRgb(0x1E, 0x88, 0xE5);
            Preview = Color.FromRgb(0x43, 0xA0, 0x47);
            PreviewForeground = Color.FromRgb(0x22, 0x22, 0x22);
            PreviewGhost = Color.FromArgb(0xB0, 0x21, 0x96, 0xF3);
            MarqueeWindowFill = Color.FromArgb(0x30, 0x21, 0x96, 0xF3);
            MarqueeWindowStroke = Color.FromRgb(0x21, 0x96, 0xF3);
            MarqueeCrossFill = Color.FromArgb(0x30, 0x4C, 0xAF, 0x50);
            MarqueeCrossStroke = Color.FromRgb(0x4C, 0xAF, 0x50);
            VertexHandleFill = Colors.White;
            VertexHandleStroke = Color.FromRgb(0x21, 0x96, 0xF3);
            VertexHandleHoverFill = Color.FromRgb(0xE3, 0xF2, 0xFD);
            VertexHandleHoverStroke = Color.FromRgb(0x19, 0x76, 0xD2);
            VertexHandleSelectedFill = Color.FromRgb(0xBB, 0xDE, 0xFB);
            VertexHandleSelectedStroke = Color.FromRgb(0x15, 0x65, 0xC0);
        }

        Changed?.Invoke();
    }

    public static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
