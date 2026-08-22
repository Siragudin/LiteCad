using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class AxisTool : ToolBase
{
    private readonly List<SnapPoint> _visibleSnaps = [];
    private bool _hasStart;
    private bool _hasDirection;
    private PointF _startPoint;
    private PointF _previewEnd;
    private double _directionX;
    private double _directionY;

    public override ToolId Id => ToolId.Axis;

    public override void OnDeactivated()
    {
        Context?.SetLengthInputEnabled(false);
        ResetPreview();
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
            Context.SetLengthInputEnabled(false);
            ResetPreview();
            Context.SetLength(null);
            Context.SetStatus(Strings.Status_AxisCancelled);
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var snapped = ResolveSnap(world);

        if (!_hasStart)
        {
            _hasStart = true;
            _startPoint = snapped;
            _previewEnd = snapped;
            Context.SetLengthInputEnabled(true);
            Context.SetStatus(Strings.Input_Axis_SelectEndPoint);
            Context.ResetLengthInput(null);
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        CommitSegment(snapped);
        e.Handled = true;
    }

    public override void OnMouseMove(MouseEventArgs e, PointF world)
    {
        if (Context is null)
        {
            return;
        }

        var result = DrawingLinePreviewSupport.UpdateFromMouseMove(
            Context.Session.SnapService,
            Context.Session.Document,
            world,
            Context.SnapTolerance,
            _hasStart,
            _startPoint,
            Context.Session.AxisToolOptions.OrthoEnabled,
            includeOnEdge: !_hasStart);

        _visibleSnaps.Clear();
        foreach (var snap in result.VisibleSnaps)
        {
            _visibleSnaps.Add(snap);
        }

        if (_hasStart)
        {
            _previewEnd = result.PreviewEnd;
            UpdateDirection();
            Context.SetLength(MathUtils.Distance(_startPoint, _previewEnd));
        }
        else
        {
            Context.SetLength(null);
        }

        Context.RequestRedraw();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (Context is null)
        {
            return;
        }

        if (e.Key == Key.Escape && _hasStart)
        {
            Context.SetLengthInputEnabled(false);
            ResetPreview();
            Context.SetLength(null);
            Context.SetStatus(Strings.Status_AxisCancelled);
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        if (_hasStart && Context.ProcessLengthKey(e))
        {
            e.Handled = true;
        }
    }

    public override bool TryApplyLength(double length)
    {
        if (!_hasStart || Context is null || length <= 0)
        {
            return false;
        }

        if (!_hasDirection)
        {
            Context.SetStatus(Strings.Input_Axis_SetDirectionThenTypeLength);
            return false;
        }

        var currentLength = Math.Sqrt(_directionX * _directionX + _directionY * _directionY);
        if (currentLength <= Context.SnapTolerance)
        {
            Context.SetStatus(Strings.Input_Axis_SetDirectionThenTypeLength);
            return false;
        }

        var scale = length / currentLength;
        var endPoint = new PointF(
            _startPoint.X + _directionX * scale,
            _startPoint.Y + _directionY * scale);

        CommitSegment(endPoint);
        return true;
    }

    public override void RenderOverlay(DrawingContext context, Camera camera, Size viewport)
    {
        if (Context is null)
        {
            return;
        }

        var zoom = camera.Zoom;
        foreach (var snap in _visibleSnaps)
        {
            SnapRenderer.DrawSnapPoint(context, snap, zoom);
        }

        if (!_hasStart)
        {
            return;
        }

        PreviewLineRenderer.Draw(
            context,
            _startPoint,
            _previewEnd,
            PreviewLineRenderer.CreatePen(
                zoom,
                Color.FromRgb(0x15, 0x65, 0xC0),
                1.5,
                [12, 4, 2, 4]));
    }

    private void CommitSegment(PointF endPoint)
    {
        if (Context is null)
        {
            return;
        }

        var topologyTolerance = TopologyTolerance.ForMutation;
        Context.RecordUndo();

        if (AxisService.Create(Context.Session.Document, _startPoint, endPoint, topologyTolerance) is null)
        {
            Context.SetStatus(Strings.Status_AxisTooShort);
            return;
        }

        Context.Session.SnapService.InvalidateCache();

        _startPoint = endPoint;
        _previewEnd = endPoint;
        _hasDirection = false;
        _directionX = 0;
        _directionY = 0;
        Context.ResetLengthInput(null);
        Context.SetStatus(Strings.Input_Axis_SelectNextPoint);
        Context.RequestRedraw();
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
            _hasStart ? _startPoint : null,
            Context.SnapTolerance,
            Context.Session.AxisToolOptions.OrthoEnabled && _hasStart,
            includeOnEdge: !_hasStart);
    }

    private void ResetPreview()
    {
        _hasStart = false;
        _hasDirection = false;
        _directionX = 0;
        _directionY = 0;
        _visibleSnaps.Clear();
    }

    private void UpdateDirection()
    {
        if (Context is null)
        {
            return;
        }

        _directionX = _previewEnd.X - _startPoint.X;
        _directionY = _previewEnd.Y - _startPoint.Y;
        var length = Math.Sqrt(_directionX * _directionX + _directionY * _directionY);
        _hasDirection = length > Context.SnapTolerance;
    }
}
