using LiteCad.Core.Document;
using LiteCad.Core.Selection;
using LiteCad.Texts;
using LiteCad.UI;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class TextRenderer
{
    public void Render(
        DrawingContext context,
        CadDocument document,
        Selection selection,
        Camera camera,
        Size viewport,
        bool forScreenDisplay)
    {
        foreach (var note in document.Texts)
        {
            var isSelected = selection.SelectedTextIds.Contains(note.Id);
            var color = forScreenDisplay
                ? isSelected ? CanvasTheme.DimensionSelected : CanvasTheme.DimensionNormal
                : Color.FromRgb(0x15, 0x65, 0xC0);
            TextAnnotationDrawing.Draw(
                context,
                note,
                camera.Zoom,
                color,
                camera,
                viewport,
                isSelected);
        }
    }
}
