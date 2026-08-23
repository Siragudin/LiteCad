using LiteCad.Core.Geometry;
using LiteCad.Texts;
using LiteCad.UI;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public static class TextAnnotationDrawing
{
    public static void Draw(
        DrawingContext context,
        TextNote note,
        double zoom,
        Color color,
        Camera camera,
        Size viewport,
        bool isSelected,
        bool showCaret = false)
    {
        var brush = new SolidColorBrush(color);
        var pen = RenderStyles.CreateScreenPen(
            brush,
            isSelected ? TextGeometry.SelectedLineThicknessScreen : TextGeometry.LineThicknessScreen,
            zoom);
        var layout = TextGeometry.CreateLayout(note, zoom);

        foreach (var (start, end) in TextGeometry.GetSegments(layout))
        {
            context.DrawLine(pen, new Point(start.X, start.Y), new Point(end.X, end.Y));
        }

        DrawTextAndCaret(context, note.Text, note.TextSize, layout, color, camera, viewport, showCaret, zoom);
    }

    public static void DrawPreviewShaft(
        DrawingContext context,
        PointF tip,
        PointF origin,
        double zoom,
        Color color)
    {
        var brush = new SolidColorBrush(color);
        var pen = RenderStyles.CreateScreenPen(brush, TextGeometry.LineThicknessScreen, zoom);
        foreach (var (start, end) in TextGeometry.GetPreviewShaft(tip, origin, zoom))
        {
            context.DrawLine(pen, new Point(start.X, start.Y), new Point(end.X, end.Y));
        }
    }

    public static void DrawDraft(
        DrawingContext context,
        TextNoteKind kind,
        PointF origin,
        PointF? arrowTip,
        string text,
        double textSize,
        double zoom,
        Color color,
        Camera camera,
        Size viewport,
        bool showCaret)
    {
        var draft = new TextNote(Guid.Empty, kind, origin, arrowTip, text, textSize);
        Draw(context, draft, zoom, color, camera, viewport, isSelected: false, showCaret);
    }

    private static void DrawTextAndCaret(
        DrawingContext context,
        string text,
        double textSize,
        TextNoteLayout layout,
        Color color,
        Camera camera,
        Size viewport,
        bool showCaret,
        double zoom)
    {
        var screenFontSize = TextNote.NormalizeTextSize(textSize);
        var brush = new SolidColorBrush(color);
        var formatted = TextGeometry.CreateFormattedText(text, screenFontSize, brush);
        var originScreen = camera.WorldToScreen(layout.Origin, viewport);
        var originX = originScreen.X;
        var originY = originScreen.Y - formatted.Height - TextGeometry.TextGapAboveLineScreen;

        context.PushTransform(new MatrixTransform(camera.GetScreenToWorldMatrix(viewport)));
        context.PushTransform(new TranslateTransform(originX, originY));
        if (!string.IsNullOrEmpty(text))
        {
            context.DrawText(formatted, new Point(0, 0));
        }

        if (showCaret)
        {
            var caret = TextGeometry.GetCaretScreenOffset(text, screenFontSize);
            var caretPen = RenderStyles.CreateScreenPen(brush, TextGeometry.CaretThicknessScreen, zoom);
            context.DrawLine(caretPen, new Point(caret.X, caret.Y), new Point(caret.X, caret.Y + caret.Height));
        }

        context.Pop();
        context.Pop();
    }
}
