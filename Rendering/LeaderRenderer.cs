using LiteCad.Core.Document;
using LiteCad.Core.Selection;
using LiteCad.Leaders;
using LiteCad.UI;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class LeaderRenderer
{
    public void Render(
        DrawingContext context,
        CadDocument document,
        Selection selection,
        Camera camera,
        Size viewport,
        bool forScreenDisplay)
    {
        foreach (var leader in document.Leaders)
        {
            var isSelected = selection.SelectedLeaderIds.Contains(leader.Id);
            var color = forScreenDisplay
                ? isSelected ? CanvasTheme.DimensionSelected : CanvasTheme.DimensionNormal
                : Color.FromRgb(0x15, 0x65, 0xC0);
            LeaderAnnotationDrawing.Draw(
                context,
                leader,
                camera.Zoom,
                color,
                camera,
                viewport,
                isSelected);
        }
    }
}
