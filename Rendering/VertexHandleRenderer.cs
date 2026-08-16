using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public static class VertexHandleRenderer
{
    public const double HandleRadiusScreenPixels = 5.0;

    private static readonly SolidColorBrush DefaultFill = CreateBrush(0xFF, 0xFF, 0xFF);
    private static readonly SolidColorBrush DefaultStroke = CreateBrush(0x21, 0x96, 0xF3);
    private static readonly SolidColorBrush HoverFill = CreateBrush(0xE3, 0xF2, 0xFD);
    private static readonly SolidColorBrush HoverStroke = CreateBrush(0x19, 0x76, 0xD2);
    private static readonly SolidColorBrush SelectedFill = CreateBrush(0xBB, 0xDE, 0xFB);
    private static readonly SolidColorBrush SelectedStroke = CreateBrush(0x15, 0x65, 0xC0);

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

        var fill = isSelected ? SelectedFill : isHovered ? HoverFill : DefaultFill;
        var stroke = isSelected ? SelectedStroke : isHovered ? HoverStroke : DefaultStroke;
        var pen = RenderStyles.CreateScreenPen(stroke, 1.5, zoom);

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

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
