using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class ExtendTool : ToolBase
{
    private Guid? _hoveredEdgeId;
    private ExtendPlan? _previewPlan;

    public override ToolId Id => ToolId.Extend;

    public override void OnActivated()
    {
        ResetHover();
        Context?.SetStatus(Strings.Input_Extend_Idle);
    }

    public override void OnDeactivated()
    {
        ResetHover();
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
            CancelTool();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (_hoveredEdgeId is null
            || !ExtendOperations.TryBuildPlan(
                Context.Session.Document,
                _hoveredEdgeId.Value,
                world,
                Context.SnapTolerance,
                out var plan))
        {
            e.Handled = true;
            return;
        }

        Context.RecordUndo();
        if (ExtendOperations.ApplyExtend(Context.Session.Document, plan))
        {
            Context.SetStatus(Strings.Status_ExtendCompleted);
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

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (Context is null)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelTool();
            e.Handled = true;
        }
    }

    public override void RenderOverlay(DrawingContext context, Camera camera, Size viewport)
    {
        if (Context is null || _hoveredEdgeId is null)
        {
            return;
        }

        var document = Context.Session.Document;
        var zoom = camera.Zoom;
        DrawEdgeHighlight(context, document, _hoveredEdgeId.Value, zoom, Color.FromRgb(0x1E, 0x88, 0xE5), 2.0);

        if (_previewPlan is null)
        {
            return;
        }

        var previewPen = RenderStyles.CreateScreenPen(
            new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47)),
            1.5,
            zoom,
            [4, 3]);
        var from = _previewPlan.ExtendPoint;
        var to = _previewPlan.TargetPoint;
        context.DrawLine(
            previewPen,
            new Point(from.X, from.Y),
            new Point(to.X, to.Y));
    }

    private void UpdateHover(PointF world)
    {
        if (Context is null)
        {
            ResetHover();
            return;
        }

        if (!TryPickEdgeAt(world, out var edgeId))
        {
            ResetHover();
            return;
        }

        _hoveredEdgeId = edgeId;
        _previewPlan = ExtendOperations.TryBuildPlan(
            Context.Session.Document,
            edgeId,
            world,
            Context.SnapTolerance,
            out var plan)
            ? plan
            : null;
    }

    private void ResetHover()
    {
        _hoveredEdgeId = null;
        _previewPlan = null;
    }

    private void CancelTool()
    {
        ResetHover();
        Context?.SetStatus(Strings.Status_ExtendCancelled);
        Context?.ActivateSelectionTool();
        Context?.RequestRedraw();
    }

    private bool TryPickEdgeAt(PointF world, out Guid edgeId)
    {
        edgeId = Guid.Empty;
        if (Context is null)
        {
            return false;
        }

        var document = Context.Session.Document;
        var tolerance = Context.SnapTolerance;
        Edge? closestEdge = null;
        var closestDistance = tolerance;

        foreach (var edge in document.Edges)
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            if (!Geometry2D.TryProjectPointOnSegment(world, start, end, out _, out var distance, tolerance)
                || distance > closestDistance)
            {
                continue;
            }

            closestEdge = edge;
            closestDistance = distance;
        }

        if (closestEdge is null)
        {
            return false;
        }

        edgeId = closestEdge.Id;
        return true;
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
        context.DrawLine(pen, new Point(start.X, start.Y), new Point(end.X, end.Y));
    }
}
