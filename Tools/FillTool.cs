using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class FillTool : ToolBase
{
    private Polygon? _hoveredFace;

    public override ToolId Id => ToolId.Fill;

    public override void OnActivated()
    {
        _hoveredFace = null;
        Context?.SetStatus(string.Format(Strings.Status_ToolActive, Name));
    }

    public override void OnDeactivated()
    {
        _hoveredFace = null;
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
            _hoveredFace = null;
            Context.SetStatus(string.Format(Strings.Status_ToolActive, Name));
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (!FacePickOperations.TryPickFacePolygonAt(
                Context.Session.Document,
                world,
                Context.SnapTolerance,
                out var face))
        {
            e.Handled = true;
            return;
        }

        var options = Context.Session.FillToolOptions;
        var style = new FaceFillStyle
        {
            FillColor = options.FillColor,
            FillPattern = options.FillPattern
        };

        var currentFill = FaceFillService.GetFill(Context.Session.Document, face);
        if (currentFill.FillColor == style.FillColor && currentFill.FillPattern == style.FillPattern)
        {
            e.Handled = true;
            return;
        }

        Context.RecordUndo();
        FaceFillService.TryApplyFill(Context.Session.Document, face, style);
        Context.SetStatus(Strings.Status_FaceFillApplied);
        Context.RequestRedraw();
        e.Handled = true;
    }

    public override void OnMouseMove(MouseEventArgs e, PointF world)
    {
        if (Context is null)
        {
            return;
        }

        _hoveredFace = FacePickOperations.TryPickFacePolygonAt(
            Context.Session.Document,
            world,
            Context.SnapTolerance,
            out var face)
            ? face
            : null;

        RequestOverlayRedraw();
        e.Handled = true;
    }

    public override void RenderOverlay(DrawingContext context, Camera camera, Size viewport)
    {
        if (Context is null || _hoveredFace is null)
        {
            return;
        }

        var geometry = PolygonRenderer.CreateGeometry(Context.Session.Document, _hoveredFace);
        if (geometry is null)
        {
            return;
        }

        var fill = CanvasTheme.CreateFrozenBrush(CanvasTheme.SelectionFill);
        var stroke = RenderStyles.CreateScreenPen(CanvasTheme.CreateFrozenBrush(CanvasTheme.SelectionStroke), 1.5, camera.Zoom);
        context.DrawGeometry(fill, stroke, geometry);
    }
}
