using System.Windows;

namespace LiteCad.Rendering.Pdf;

internal static class PdfViewTransform
{
    public static Point Apply(PdfDrawingView view, Point contentPoint)
        => new(
            view.Position.X + view.Scale * contentPoint.X,
            view.Position.Y + view.Scale * contentPoint.Y);

    public static Rect TransformBounds(Rect contentBounds, PdfDrawingView view)
        => new(
            view.Position.X + view.Scale * contentBounds.Left,
            view.Position.Y + view.Scale * contentBounds.Top,
            view.Scale * contentBounds.Width,
            view.Scale * contentBounds.Height);

    public static Point ToPageDip(PdfExportLayout layout, PdfDrawingView view, Point contentPoint)
        => PdfSheetTransform.TransformContentToPageDip(view, layout, contentPoint);
}
