using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Dimensions;
using LiteCad.Services;
using LiteCad.Tools;
using LiteCad.UI;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class DimensionRenderer
{
    private sealed record CommittedCacheKey(
        int DimensionCount,
        int VertexCount,
        int EdgeCount,
        double Zoom,
        double ViewportWidth,
        double ViewportHeight,
        LinearDisplayUnit LinearUnit,
        string SelectedDimensionIds);

    private DrawingGroup? _committedCache;
    private CommittedCacheKey? _committedCacheKey;

    public void Render(
        DrawingContext context,
        CadDocument document,
        Selection selection,
        Camera camera,
        Size viewport,
        LinearDisplayUnit linearUnit,
        ITool? activeTool,
        bool forScreenDisplay = true)
    {
        var isOffsetPreview = activeTool is DimensionTool { HasOffsetPhase: true };

        if (!isOffsetPreview)
        {
            DimensionService.RemoveInvalid(document, TopologyTolerance.ForMutation);
            _committedCache = null;
            _committedCacheKey = null;
            RenderDimensions(context, document, selection, camera, viewport, linearUnit, forScreenDisplay);
            return;
        }

        var cacheKey = CreateCacheKey(document, selection, camera, viewport, linearUnit);
        if (_committedCache is not null
            && _committedCacheKey == cacheKey)
        {
            context.DrawDrawing(_committedCache);
            return;
        }

        var group = new DrawingGroup();
        using (var cacheContext = group.Open())
        {
            RenderDimensions(cacheContext, document, selection, camera, viewport, linearUnit, forScreenDisplay);
        }

        group.Freeze();
        _committedCache = group;
        _committedCacheKey = cacheKey;
        context.DrawDrawing(group);
    }

    private static CommittedCacheKey CreateCacheKey(
        CadDocument document,
        Selection selection,
        Camera camera,
        Size viewport,
        LinearDisplayUnit linearUnit)
    {
        var selectedIds = string.Join(
            ",",
            selection.SelectedDimensionIds.OrderBy(id => id));

        return new CommittedCacheKey(
            document.Dimensions.Count,
            document.Vertices.Count,
            document.Edges.Count,
            camera.Zoom,
            viewport.Width,
            viewport.Height,
            linearUnit,
            selectedIds);
    }

    private static void RenderDimensions(
        DrawingContext context,
        CadDocument document,
        Selection selection,
        Camera camera,
        Size viewport,
        LinearDisplayUnit linearUnit,
        bool forScreenDisplay)
    {
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
            var color = forScreenDisplay
                ? isSelected ? CanvasTheme.DimensionSelected : CanvasTheme.DimensionNormal
                : isSelected ? Color.FromRgb(0x1E, 0x88, 0xE5) : Color.FromRgb(0x15, 0x65, 0xC0);
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
