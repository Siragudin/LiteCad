using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering.Pdf;

/// <summary>
/// Single source of truth for world → sheet content → page transforms.
/// Coordinates are in device-independent pixels (DIP) on the PDF page unless noted.
/// </summary>
internal static class PdfSheetTransform
{
    public static Matrix GetWorldToContentMatrix(PdfExportLayout layout)
        => layout.GetEffectiveWorldToContentMatrix();

    public static Matrix GetContentToPageDipMatrix(PdfDrawingView view, PdfExportLayout layout)
    {
        var matrix = Matrix.Identity;
        if (Math.Abs(view.Scale - 1.0) > 1e-12)
        {
            matrix.Scale(view.Scale, view.Scale);
        }

        matrix.Translate(view.Position.X, view.Position.Y);
        matrix.Translate(layout.MarginDip, layout.MarginDip);
        return matrix;
    }

    public static Matrix GetWorldToPageDipMatrix(PdfSheet sheet, PdfExportLayout layout)
    {
        var worldToContent = GetWorldToContentMatrix(layout);
        var contentToPage = GetContentToPageDipMatrix(sheet.PrimaryView, layout);
        return worldToContent * contentToPage;
    }

    public static Matrix GetWorldToPagePointMatrix(PdfSheet sheet, PdfExportLayout layout)
    {
        var dipToPoint = PdfExportLayout.PointsPerInch / PdfExportLayout.DipPerInch;
        var worldToPage = GetWorldToPageDipMatrix(sheet, layout);
        worldToPage.Scale(dipToPoint, dipToPoint);
        return worldToPage;
    }

    public static Point TransformWorldToPageDip(PdfSheet sheet, PdfExportLayout layout, Point worldPoint)
        => GetWorldToPageDipMatrix(sheet, layout).Transform(worldPoint);

    public static Point TransformWorldToPagePoint(PdfSheet sheet, PdfExportLayout layout, Point worldPoint)
        => GetWorldToPagePointMatrix(sheet, layout).Transform(worldPoint);

    public static Point TransformContentToPageDip(PdfDrawingView view, PdfExportLayout layout, Point contentPoint)
        => GetContentToPageDipMatrix(view, layout).Transform(contentPoint);

    public static Rect TransformWorldBoundsToPageDip(PdfSheet sheet, PdfExportLayout layout, Rect worldBounds)
        => PdfExportLayout.TransformBounds(worldBounds, point => TransformWorldToPageDip(sheet, layout, point));
}
