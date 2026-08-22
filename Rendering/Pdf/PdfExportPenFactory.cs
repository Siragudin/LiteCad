using System.Windows.Media;

namespace LiteCad.Rendering.Pdf;

internal static class PdfExportPenFactory
{
    private const double DipToPoint = PdfExportLayout.PointsPerInch / PdfExportLayout.DipPerInch;

    public static Pen Create(PenStyle style, Color color, DoubleCollection? dashArray = null)
    {
        var brush = PdfStyledSolidColorBrush.CreateBrush(color);
        var thicknessPoints = Math.Max(
            PdfLineweightTable.ToPoints(style),
            PdfLineweightTable.MinVisiblePrintThicknessPoints);
        var pen = new Pen(brush, thicknessPoints / DipToPoint);
        if (dashArray is not null)
        {
            pen.DashStyle = new DashStyle(dashArray, 0);
        }

        PdfStyledSolidColorBrush.MarkPdfPen(pen, style);
        pen.Freeze();
        return pen;
    }
}
