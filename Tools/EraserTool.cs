using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class EraserTool : ToolBase
{
    private SelectionPick? _hoveredPick;

    public override ToolId Id => ToolId.Eraser;

    public SelectionPick? HoveredTarget => _hoveredPick;

    public override void OnActivated()
    {
        _hoveredPick = null;
        Context?.SetStatus(string.Format(Strings.Status_ToolActive, Name));
    }

    public override void OnDeactivated()
    {
        _hoveredPick = null;
        Context?.RequestRedraw();
    }

    public override void OnMouseDown(MouseButtonEventArgs e, PointF world)
    {
        if (Context is null)
        {
            return;
        }

        if (e.ChangedButton == MouseButton.Right)
        {
            _hoveredPick = null;
            Context.SetStatus(string.Format(Strings.Status_ToolActive, Name));
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (Context.Session.Edit.DeleteAt(Context.Session, world, Context.SnapTolerance))
        {
            Context.SetStatus(Strings.Status_Deleted);
        }

        UpdateHover(world);
        Context.RequestRedraw();
        e.Handled = true;
    }

    public override void OnMouseMove(MouseEventArgs e, PointF world)
    {
        if (Context is null)
        {
            return;
        }

        UpdateHover(world);
        Context.RequestRedraw();
        e.Handled = true;
    }

    public override void RenderOverlay(DrawingContext context, Camera camera, Size viewport)
    {
        if (Context is null || _hoveredPick is null)
        {
            return;
        }

        var document = Context.Session.Document;
        var zoom = camera.Zoom;

        switch (_hoveredPick.Value.Kind)
        {
            case SelectionPickKind.Vertex:
                var vertex = document.Vertices.FirstOrDefault(item => item.Id == _hoveredPick.Value.Id);
                if (vertex is not null)
                {
                    VertexHandleRenderer.DrawHandle(context, vertex.Position, zoom, isHovered: true, isSelected: false);
                }

                break;

            case SelectionPickKind.Edge:
                DrawEdgeHighlight(
                    context,
                    document,
                    _hoveredPick.Value.Id,
                    zoom,
                    Color.FromRgb(0xE5, 0x39, 0x35),
                    2.0);
                break;

            case SelectionPickKind.Polygon:
                DrawPolygonHighlight(context, document, _hoveredPick.Value.Id, zoom);
                break;
        }
    }

    private void UpdateHover(PointF world)
    {
        if (Context is null)
        {
            _hoveredPick = null;
            return;
        }

        _hoveredPick = SelectionPickOperations.PickAt(
            Context.Session.Document,
            world,
            Context.SnapTolerance,
            Context.SelectionPickTolerance);
    }

    private static void DrawEdgeHighlight(
        DrawingContext context,
        CadDocument document,
        Guid edgeId,
        double zoom,
        Color color,
        double thickness)
    {
        var edge = document.Edges.FirstOrDefault(item => item.Id == edgeId);
        if (edge is null)
        {
            return;
        }

        var start = TopologyService.GetEdgeStartPoint(document, edge);
        var end = TopologyService.GetEdgeEndPoint(document, edge);
        var pen = RenderStyles.CreateScreenPen(new SolidColorBrush(color), thickness, zoom, [3, 2]);
        context.DrawLine(
            pen,
            new Point(start.X, start.Y),
            new Point(end.X, end.Y));
    }

    private static void DrawPolygonHighlight(
        DrawingContext context,
        CadDocument document,
        Guid polygonId,
        double zoom)
    {
        var polygon = document.Polygons.FirstOrDefault(item => item.Id == polygonId);
        if (polygon is null)
        {
            return;
        }

        var points = PolygonGeometry.GetOuterBoundaryPoints(document, polygon);
        if (points.Count < 3)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, points[0].Y), isFilled: true, isClosed: true);
            for (var index = 1; index < points.Count; index++)
            {
                ctx.LineTo(new Point(points[index].X, points[index].Y), isStroked: true, isSmoothJoin: false);
            }
        }

        geometry.Freeze();

        var fill = new SolidColorBrush(Color.FromArgb(0x40, 0xE5, 0x39, 0x35));
        fill.Freeze();
        var stroke = RenderStyles.CreateScreenPen(
            new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)),
            1.5,
            zoom,
            [4, 2]);

        context.DrawGeometry(fill, stroke, geometry);
    }
}
