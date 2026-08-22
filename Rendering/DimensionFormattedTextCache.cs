using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

/// <summary>
/// Reuses a single <see cref="FormattedText"/> while preview text metrics stay unchanged.
/// </summary>
public sealed class DimensionFormattedTextCache
{
    private static readonly Typeface PreviewTypeface = new("Segoe UI");

    private string? _lastText;
    private double _lastScreenFontSize;
    private Color _lastColor;
    private double _lastPixelsPerDip;
    private FormattedText? _cached;

    public int CreateCount { get; private set; }

    public int ReuseCount { get; private set; }

    public FormattedText GetOrCreate(string text, double screenFontSize, Color color, double pixelsPerDip)
    {
        if (_cached is not null
            && _lastText == text
            && Math.Abs(_lastScreenFontSize - screenFontSize) <= 1e-9
            && _lastColor == color
            && Math.Abs(_lastPixelsPerDip - pixelsPerDip) <= 1e-9)
        {
            ReuseCount++;
            return _cached;
        }

        _lastText = text;
        _lastScreenFontSize = screenFontSize;
        _lastColor = color;
        _lastPixelsPerDip = pixelsPerDip;
        _cached = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            PreviewTypeface,
            screenFontSize,
            CanvasTheme.CreateFrozenBrush(color),
            pixelsPerDip)
        {
            TextAlignment = TextAlignment.Center
        };
        CreateCount++;
        return _cached;
    }

    public void Invalidate()
    {
        _cached = null;
        _lastText = null;
    }

    public static double ResolvePixelsPerDip()
    {
        try
        {
            return VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip;
        }
        catch
        {
            return 1.0;
        }
    }
}
