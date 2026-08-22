using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class LineTool : ToolBase
{
    private readonly List<SnapPoint> _visibleSnaps = [];
    private bool _hasStart;
    private bool _hasDirection;
    private PointF _startPoint;
    private PointF _previewEnd;
    private double _directionX;
    private double _directionY;

    public override ToolId Id => ToolId.Line;

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
            HandleRightClick();
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
            Context.SetStatus(Strings.Input_Line_SelectEndPoint);
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
            Context.Session.LineToolOptions.OrthoEnabled,
            includeOnEdge: true);

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
            Context.SetStatus(Strings.Status_LineCancelled);
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
            Context.SetStatus(Strings.Input_Line_SetDirectionThenTypeLength);
            return false;
        }

        var currentLength = Math.Sqrt(_directionX * _directionX + _directionY * _directionY);
        if (currentLength <= Context.SnapTolerance)
        {
            Context.SetStatus(Strings.Input_Line_SetDirectionThenTypeLength);
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

        var options = Context.Session.LineToolOptions;
        var previewPen = PreviewLineRenderer.CreatePen(
            zoom,
            options.Color,
            options.Thickness,
            GetDashArray(options.LineType));
        PreviewLineRenderer.Draw(context, _startPoint, _previewEnd, previewPen);
    }

    private void CommitSegment(PointF endPoint)
    {
        if (Context is null)
        {
            return;
        }

        var topologyTolerance = TopologyTolerance.ForMutation;
        var options = Context.Session.LineToolOptions;
        var alignment = GetAlignment(_startPoint, endPoint);

        Context.RecordUndo();

        var template = Edge.CreateStyleTemplate();
        template.Color = options.Color;
        template.Thickness = options.Thickness;
        template.LineType = options.LineType;
        template.OrthoAlignment = alignment;

        EdgeOperations.AddSegment(Context.Session.Document, _startPoint, endPoint, template, topologyTolerance);
        PolygonBuilder.SyncFaces(Context.Session.Document, topologyTolerance);
        Context.Session.SnapService.InvalidateCache();

        _startPoint = endPoint;
        _previewEnd = endPoint;
        _hasDirection = false;
        _directionX = 0;
        _directionY = 0;
        Context.ResetLengthInput(null);
        Context.SetStatus(Strings.Input_Line_SelectNextPoint);
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
            Context.Session.LineToolOptions.OrthoEnabled && _hasStart,
            includeOnEdge: true);
    }

    private void HandleRightClick()
    {
        if (Context is null)
        {
            return;
        }

        var hadPreview = _hasStart;
        if (_hasStart)
        {
            Context.SetLengthInputEnabled(false);
            ResetPreview();
            Context.SetLength(null);
        }

        Context.Session.Selection.Clear();
        Context.SetStatus(hadPreview ? Strings.Status_LineCancelled : Strings.Status_SelectionCleared);
        Context.RequestRedraw();
    }

    private OrthoAlignment GetAlignment(PointF start, PointF end)
    {
        if (Context is null || !Context.Session.LineToolOptions.OrthoEnabled)
        {
            return OrthoAlignment.None;
        }

        return Geometry2D.GetOrthoAlignment(start, end, Context.SnapTolerance);
    }

    private static DoubleCollection? GetDashArray(EdgeLineType lineType) => lineType switch
    {
        EdgeLineType.Dashed => [8, 4],
        EdgeLineType.Dotted => [2, 4],
        _ => null
    };

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
