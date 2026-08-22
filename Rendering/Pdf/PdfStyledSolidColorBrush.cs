using System.Windows.Media;

namespace LiteCad.Rendering.Pdf;

/// <summary>
/// Marks PDF export pens via Pen.MiterLimit (WPF Pen/Brush types are sealed).
/// Canvas pens keep the default MiterLimit of 10; PDF pens use 100 + (int)PenStyle.
/// </summary>
internal static class PdfStyledSolidColorBrush
{
    public const double PdfPenMiterLimitBase = 100.0;

    public static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Tags a PDF export pen by writing <see cref="Pen.MiterLimit"/> to an out-of-band value
    /// (<see cref="PdfPenMiterLimitBase"/> + style ordinal). WPF <see cref="Pen"/> and
    /// <see cref="SolidColorBrush"/> are sealed, so a dedicated marker type cannot inherit from them.
    /// </summary>
    /// <remarks>
    /// <see cref="Pen.MiterLimit"/> is repurposed here as a marker channel, not to control miter joins.
    /// This is safe only while every PDF export pen is drawn with single-segment
    /// <see cref="DrawingContext.DrawLine"/> calls (not multi-segment
    /// PolyLineSegment, StreamGeometry, or PathGeometry strokes). If PolygonRenderer,
    /// FaceFillRenderer, or other multi-segment geometry is wired to
    /// <see cref="PdfExportPenFactory"/>, re-verify this invariant or switch marking to
    /// ConditionalWeakTable&lt;Pen, PenStyle&gt;.
    /// </remarks>
    public static void MarkPdfPen(Pen pen, PenStyle style)
        => pen.MiterLimit = PdfPenMiterLimitBase + (int)style;

    /// <summary>
    /// Returns whether <paramref name="pen"/> was tagged by <see cref="MarkPdfPen"/>.
    /// </summary>
    /// <remarks>
    /// Detection relies on the artificial <see cref="Pen.MiterLimit"/> marker described in
    /// <see cref="MarkPdfPen"/>. Valid only under the same single-<see cref="DrawingContext.DrawLine"/>
    /// invariant; multi-segment PDF strokes would require a different marking strategy.
    /// </remarks>
    public static bool IsPdfExportPen(Pen pen)
        => pen.MiterLimit >= PdfPenMiterLimitBase - 1e-6;

    public static PenStyle GetStyle(Pen pen)
        => (PenStyle)(int)Math.Round(pen.MiterLimit - PdfPenMiterLimitBase);
}
