using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Dimensions;
using LiteCad.Services;
using LiteCad.UI;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class DimensionRenderer
{
    public void Render(
        DrawingContext context,
        CadDocument document,
        Selection selection,
        Camera camera,
        Size viewport,
        LinearDisplayUnit linearUnit)
    {
        DimensionService.RemoveInvalid(document, TopologyTolerance.ForMutation);

        foreach (var dimension in document.Dimensions)
        {
            if (!DimensionGeometry.TryGetAnchorPoints(document, dimension, out var firstAnchor, out var secondAnchor)
                || !DimensionGeometry.TryCreateLayout(
                    firstAnchor,
                    secondAnchor,
                    dimension,
                    TopologyTolerance.ForMutation,
                    out var layout))
            {
                continue;
            }

            var isSelected = selection.SelectedDimensionIds.Contains(dimension.Id);
            var color = isSelected ? Color.FromRgb(0x1E, 0x88, 0xE5) : Color.FromRgb(0x15, 0x65, 0xC0);
            var distanceText = UnitDisplayFormatter.FormatLinear(layout.MeasuredDistance, linearUnit);
            DimensionAnnotationDrawing.Draw(
                context,
                layout,
                dimension.Offset,
                camera.Zoom,
                color,
                isSelected,
                camera,
                viewport,
                distanceText: distanceText,
                extensionStyle: dimension.ExtensionStyle,
                textWorldHeight: DimensionAnnotationDrawing.GetWorldTextHeight(dimension));
        }
    }
}
