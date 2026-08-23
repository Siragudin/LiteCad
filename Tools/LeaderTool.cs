using LiteCad.Core.Geometry;
using LiteCad.Leaders;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.UI;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class LeaderTool : ToolBase
{
    private readonly List<SnapPoint> _visibleSnaps = [];
    private bool _hasTarget;
    private bool _hasCursor;
    private PointF _target;
    private PointF _previewPoint;

    public override ToolId Id => ToolId.Leader;

    public override void OnActivated()
    {
        ResetState();
        SetIdleStatus();
    }

    public override void OnDeactivated()
    {
        ResetState();
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
            HandleRightClick();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var snapped = ResolvePlacement(world);
        if (_hasTarget)
        {
            CommitElevation(snapped);
            e.Handled = true;
            return;
        }

        _hasTarget = true;
        _target = snapped;
        _previewPoint = snapped;
        Context.SetStatus(Strings.Input_Leader_SelectText);
        Context.RequestRedraw();
        e.Handled = true;
    }

    public override void OnMouseUp(MouseButtonEventArgs e, PointF world)
    {
        if (Context is null || e.ChangedButton != MouseButton.Left || !_hasTarget)
        {
            return;
        }

        var snapped = ResolvePlacement(world);
        _previewPoint = snapped;
        if (HasDraggedFromTarget(snapped))
        {
            CommitElevation(snapped);
            e.Handled = true;
        }
    }

    public override void OnMouseMove(MouseEventArgs e, PointF world)
    {
        if (Context is null)
        {
            return;
        }

        var snapped = ResolvePlacement(world);
        _hasCursor = true;
        _previewPoint = snapped;
        _visibleSnaps.Clear();
        _visibleSnaps.AddRange(
            Context.Session.SnapService.GetVisibleSnaps(
                Context.Session.Document,
                world,
                Context.SnapTolerance));
        Context.RequestRedraw();
        e.Handled = true;
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HandleRightClick();
            e.Handled = true;
        }
    }

    public override void RenderOverlay(DrawingContext context, Camera camera, Size viewport)
    {
        if (Context is null || (!_hasTarget && !_hasCursor))
        {
            return;
        }

        var zoom = camera.Zoom;
        foreach (var snap in _visibleSnaps)
        {
            SnapRenderer.DrawSnapPoint(context, snap, zoom);
        }

        var previewColor = PreviewLineRenderer.GetAnnotationPreviewColor();
        var measure = _hasTarget ? _target : _previewPoint;
        var origin = _previewPoint;
        var preview = new Leader(
            Guid.Empty,
            LeaderKind.Elevation,
            origin,
            LeaderGeometry.CreateSideHint(origin, LeaderGeometry.ResolveSide(measure, origin)),
            PreviewText(measure));
        LeaderAnnotationDrawing.Draw(context, preview, zoom, previewColor, camera, viewport, isSelected: false);
    }

    private string PreviewText(PointF point)
    {
        if (Context is null)
        {
            return "0";
        }

        var baseY = Context.Session.Document.ElevationBaseY ?? point.Y;
        return ElevationFormatting.Format(ElevationFormatting.ComputeDelta(baseY, point.Y));
    }

    private void CommitElevation(PointF landing)
    {
        if (Context is null || !_hasTarget)
        {
            return;
        }

        var wasFirst = !Context.Session.Document.ElevationBaseY.HasValue;
        Context.RecordUndo();
        LeaderService.CreateElevation(Context.Session.Document, _target, landing);
        Context.SetStatus(wasFirst ? Strings.Status_ZeroElevationSet : Strings.Status_LeaderCreated);
        ResetState();
        SetIdleStatus();
        Context.RequestRedraw();
    }

    private void HandleRightClick()
    {
        if (Context is null)
        {
            return;
        }

        var hadPending = _hasTarget;
        ResetState();
        Context.SetStatus(hadPending ? Strings.Status_LeaderCancelled : Strings.Status_SelectionCleared);
        Context.RequestRedraw();
    }

    private void SetIdleStatus()
    {
        if (Context is null)
        {
            return;
        }

        Context.SetStatus(Context.Session.Document.ElevationBaseY.HasValue
            ? Strings.Input_Leader_SelectElevation
            : Strings.Input_Leader_SetZero);
    }

    private bool HasDraggedFromTarget(PointF point)
    {
        if (Context is null)
        {
            return false;
        }

        var threshold = 8.0 / Math.Max(Context.Session.Camera.Zoom, 1e-6);
        return Math.Abs(point.X - _target.X) > threshold;
    }

    private PointF ResolvePlacement(PointF world)
    {
        var snapped = ResolveSnap(world);
        return _hasTarget
            ? LeaderGeometry.ConstrainHorizontal(_target, snapped)
            : snapped;
    }

    private PointF ResolveSnap(PointF world)
    {
        if (Context is null)
        {
            return world;
        }

        return Context.Session.SnapService.ResolveDrawingSnap(
            Context.Session.Document,
            world,
            _hasTarget ? _target : null,
            Context.SnapTolerance,
            orthoEnabled: false,
            includeOnEdge: true);
    }

    private void ResetState()
    {
        _hasTarget = false;
        _hasCursor = false;
        _target = PointF.Zero;
        _previewPoint = PointF.Zero;
        _visibleSnaps.Clear();
    }
}
