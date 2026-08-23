using LiteCad.Core.Geometry;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Texts;

public readonly record struct TextNoteLayout(
    PointF Origin,
    PointF? ArrowTip,
    PointF UnderlineEnd,
    PointF ChevronLeft,
    PointF ChevronRight,
    Size TextScreenSize);

public static class TextGeometry
{
    public const double LineThicknessScreen = 1.5;

    public const double CaretThicknessScreen = 0.9;

    public const double SelectedLineThicknessScreen = 2.0;

    public const double ArrowLengthScreen = 10.0;

    public const double ArrowHalfWidthScreen = 4.0;

    public const double TextGapAboveLineScreen = 3.0;

    private static readonly Typeface Typeface = new(
        new FontFamily("Segoe UI"),
        FontStyles.Normal,
        FontWeights.Normal,
        FontStretches.Normal);

    public static FormattedText CreateFormattedText(string text, double screenFontSize, Brush brush)
    {
        var normalized = NormalizeNewlines(text);
        if (string.IsNullOrEmpty(normalized))
        {
            normalized = " ";
        }
        else if (normalized.EndsWith('\n'))
        {
            // FormattedText ignores a trailing newline; keep an extra line for caret.
            normalized += "\u200B";
        }

        return new FormattedText(
            normalized,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface,
            screenFontSize,
            brush,
            GetPixelsPerDip());
    }

    public static Size MeasureScreen(string text, double screenFontSize)
    {
        var formatted = CreateFormattedText(text, screenFontSize, Brushes.Black);
        if (string.IsNullOrEmpty(text))
        {
            return new Size(0, formatted.Height);
        }

        return new Size(formatted.WidthIncludingTrailingWhitespace, formatted.Height);
    }

    public static Rect GetCaretScreenOffset(string text, double screenFontSize)
    {
        var formatted = CreateFormattedText(text, screenFontSize, Brushes.Black);
        if (string.IsNullOrEmpty(text))
        {
            return new Rect(0, 0, 0, formatted.Height);
        }

        var lines = NormalizeNewlines(text).Split('\n');
        var last = lines[^1];
        var lastFormatted = CreateFormattedText(string.IsNullOrEmpty(last) ? "\u200B" : last, screenFontSize, Brushes.Black);
        var y = Math.Max(0, formatted.Height - lastFormatted.Height);
        var x = string.IsNullOrEmpty(last) ? 0 : lastFormatted.WidthIncludingTrailingWhitespace;
        return new Rect(x, y, 0, lastFormatted.Height);
    }

    public static TextNoteLayout CreateLayout(TextNote note, double zoom)
        => CreateLayout(note.Kind, note.Origin, note.ArrowTip, note.Text, note.TextSize, zoom);

    public static TextNoteLayout CreateLayout(
        TextNoteKind kind,
        PointF origin,
        PointF? arrowTip,
        string text,
        double textSize,
        double zoom)
    {
        var world = 1.0 / Math.Max(zoom, 1e-6);
        var screenSize = MeasureScreen(text, TextNote.NormalizeTextSize(textSize));
        var underlineEnd = new PointF(origin.X + (float)(screenSize.Width * world), origin.Y);
        var chevronLeft = origin;
        var chevronRight = origin;
        if (kind == TextNoteKind.Arrow && arrowTip is PointF tip)
        {
            var shaft = new PointF(origin.X - tip.X, origin.Y - tip.Y);
            var length = Math.Sqrt(shaft.X * shaft.X + shaft.Y * shaft.Y);
            if (length > 1e-9)
            {
                var alongX = shaft.X / length;
                var alongY = shaft.Y / length;
                var back = ArrowLengthScreen * world;
                var half = ArrowHalfWidthScreen * world;
                var baseX = tip.X + alongX * back;
                var baseY = tip.Y + alongY * back;
                chevronLeft = new PointF((float)(baseX - alongY * half), (float)(baseY + alongX * half));
                chevronRight = new PointF((float)(baseX + alongY * half), (float)(baseY - alongX * half));
            }
        }

        return new TextNoteLayout(origin, arrowTip, underlineEnd, chevronLeft, chevronRight, screenSize);
    }

    public static IEnumerable<(PointF Start, PointF End)> GetSegments(TextNoteLayout layout)
    {
        if (layout.ArrowTip is PointF tip)
        {
            yield return (tip, layout.Origin);
            yield return (tip, layout.ChevronLeft);
            yield return (tip, layout.ChevronRight);
            yield return (layout.Origin, layout.UnderlineEnd);
        }
    }

    public static IEnumerable<(PointF Start, PointF End)> GetPreviewShaft(PointF tip, PointF origin, double zoom)
    {
        var layout = CreateLayout(TextNoteKind.Arrow, origin, tip, string.Empty, TextNote.DefaultTextSize, zoom);
        yield return (tip, origin);
        yield return (tip, layout.ChevronLeft);
        yield return (tip, layout.ChevronRight);
    }

    public static double GetPixelsPerDip()
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

    private static string NormalizeNewlines(string text)
        => (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
}
