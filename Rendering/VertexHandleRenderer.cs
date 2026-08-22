using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public static class VertexHandleRenderer
{
    public const double HandleRadiusScreenPixels = 5.0;

    public static double GetHandleWorldRadius(double zoom)
        => HandleRadiusScreenPixels / zoom;

    public static void DrawHandles(
        DrawingContext context,
        CadDocument document,
        double zoom,
        Guid? hoveredVertexId,
        IReadOnlyCollection<Guid> selectedVertexIds)
    {
        var selected = selectedVertexIds as HashSet<Guid> ?? selectedVertexIds.ToHashSet();

        foreach (var vertex in document.Vertices)
        {
            var isHovered = hoveredVertexId == vertex.Id;
            var isSelected = selected.Contains(vertex.Id);
            DrawHandle(context, vertex.Position, zoom, isHovered, isSelected);
        }
    }

    public static void DrawHandle(
        DrawingContext context,
        PointF worldPosition,
        double zoom,
        bool isHovered,
        bool isSelected)
    {
        var radius = GetHandleWorldRadius(zoom);
        if (isHovered)
        {
            radius *= 1.2;
        }

        var fill = isSelected
            ? CanvasTheme.CreateFrozenBrush(CanvasTheme.VertexHandleSelectedFill)
            : isHovered
                ? CanvasTheme.CreateFrozenBrush(CanvasTheme.VertexHandleHoverFill)
                : CanvasTheme.CreateFrozenBrush(CanvasTheme.VertexHandleFill);
        var strokeColor = isSelected
            ? CanvasTheme.VertexHandleSelectedStroke
            : isHovered
                ? CanvasTheme.VertexHandleHoverStroke
                : CanvasTheme.VertexHandleStroke;
        var pen = RenderStyles.CreateScreenPen(CanvasTheme.CreateFrozenBrush(strokeColor), 1.5, zoom);

        context.DrawEllipse(
            fill,
            pen,
            new Point(worldPosition.X, worldPosition.Y),
            radius,
            radius);
    }

    public static bool TryPickVertex(
        CadDocument document,
        PointF world,
        double zoom,
        double tolerance,
        out Guid vertexId)
    {
        var pickRadius = GetHandleWorldRadius(zoom) + tolerance;
        Vertex? closest = null;
        var closestDistance = pickRadius;

        foreach (var vertex in document.Vertices)
        {
            var distance = MathUtils.Distance(world, vertex.Position);
            if (distance > closestDistance)
            {
                continue;
            }

            closest = vertex;
            closestDistance = distance;
        }

        if (closest is null)
        {
            vertexId = Guid.Empty;
            return false;
        }

        vertexId = closest.Id;
        return true;
    }
}
