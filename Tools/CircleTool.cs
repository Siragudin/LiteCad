using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class CircleTool : ToolBase
{
    private readonly List<SnapPoint> _visibleSnaps = [];
    private bool _hasCenter;
    private PointF _center;
    private PointF _radiusPoint;

    public override ToolId Id => ToolId.Circle;

    public override void OnActivated()
    {
        Context?.SetStatus(Strings.Input_Circle_SelectCenter);
    }

    public override void OnDeactivated()
    {
        Context?.SetLineInputModeEnabled(false, LineInputLabelMode.Length);
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
            Cancel(Strings.Status_CircleCancelled);
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var snapped = ResolveSnap(world);

        if (!_hasCenter)
        {
            _hasCenter = true;
            _center = snapped;
            _radiusPoint = snapped;
            Context.SetLineInputModeEnabled(true, LineInputLabelMode.Radius);
            Context.SetStatus(Strings.Input_Circle_SelectRadius);
            Context.ResetLengthInput(null);
            Context.SetArea(null);
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        CommitCircle(ResolveRadius(snapped));
        e.Handled = true;
    }

    public override void OnMouseMove(MouseEventArgs e, PointF world)
    {
        if (Context is null)
        {
            return;
        }

        var tolerance = Context.SnapTolerance;
        _visibleSnaps.Clear();
        foreach (var snap in Context.Session.SnapService.GetVisibleSnaps(Context.Session.Document, world, tolerance))
        {
            _visibleSnaps.Add(snap);
        }

        if (_hasCenter)
        {
            _radiusPoint = ResolveSnap(world);
            Context.SetLength(CircleGeometry.ComputeRadius(_center, _radiusPoint));
        }
        else
        {
            Context.SetLength(null);
            Context.SetArea(null);
        }

        Context.RequestRedraw();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (Context is null)
        {
            return;
        }

        if (e.Key == Key.Escape && _hasCenter)
        {
            Cancel(Strings.Status_CircleCancelled);
            e.Handled = true;
            return;
        }

        if (_hasCenter && Context.ProcessLengthKey(e))
        {
            e.Handled = true;
        }
    }

    public override bool TryApplyLength(double length)
    {
        if (!_hasCenter || Context is null || length <= 0)
        {
            return false;
        }

        if (!HasRadiusDirection())
        {
            Context.SetStatus(Strings.Input_Circle_SetDirectionThenTypeRadius);
            return false;
        }

        CommitCircle(length);
        return true;
    }

    public override bool TryApplyLengthInput(string input)
    {
        if (!_hasCenter || Context is null)
        {
            return false;
        }

        if (!TryParseRadius(input, out var radius))
        {
            return false;
        }

        return TryApplyLength(radius);
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

        if (!_hasCenter || !TryGetPreviewRadius(out var radius))
        {
            return;
        }

        var points = CircleGeometry.ComputePoints(_center, radius);
        var previewPen = CreatePreviewPen(zoom);
        for (var i = 0; i < CircleGeometry.SegmentCount; i++)
        {
            var start = points[i];
            var end = points[(i + 1) % CircleGeometry.SegmentCount];
            context.DrawLine(previewPen, ToPoint(start), ToPoint(end));
        }
    }

    internal bool HasCenter => _hasCenter;

    internal PointF Center => _center;

    internal bool TryGetPreviewRadius(out double radius)
    {
        if (!_hasCenter)
        {
            radius = 0;
            return false;
        }

        radius = CircleGeometry.ComputeRadius(_center, _radiusPoint);
        var topologyTolerance = TopologyTolerance.ForMutation;
        return radius > topologyTolerance;
    }

    private void CommitCircle(double radius)
    {
        if (Context is null || !_hasCenter)
        {
            return;
        }

        var topologyTolerance = TopologyTolerance.ForMutation;
        if (radius <= topologyTolerance)
        {
            Context.SetStatus(Strings.Error_CircleTooSmall);
            return;
        }

        Context.RecordUndo();

        var template = CreateTemplate();
        var document = Context.Session.Document;
        var points = CircleGeometry.ComputePoints(_center, radius);
        for (var i = 0; i < CircleGeometry.SegmentCount; i++)
        {
            var start = points[i];
            var end = points[(i + 1) % CircleGeometry.SegmentCount];
            EdgeOperations.AddSegment(document, start, end, template, topologyTolerance);
        }

        PolygonBuilder.SyncFaces(document, topologyTolerance);

        ResetAfterCommit();
        Context.SetStatus(Strings.Input_Circle_SelectCenter);
        Context.RequestRedraw();
    }

    private double ResolveRadius(PointF pointOnCircle)
        => CircleGeometry.ComputeRadius(_center, pointOnCircle);

    private bool HasRadiusDirection()
    {
        if (!_hasCenter)
        {
            return false;
        }

        return CircleGeometry.ComputeRadius(_center, _radiusPoint) > Context!.SnapTolerance;
    }

    private void ResetAfterCommit()
    {
        _hasCenter = false;
        _visibleSnaps.Clear();
        Context?.SetLineInputModeEnabled(false, LineInputLabelMode.Length);
        Context?.SetLength(null);
        Context?.SetArea(null);
        Context?.ResetLengthInput(null);
    }

    private void Cancel(string status)
    {
        ResetState();
        Context?.SetStatus(status);
        Context?.RequestRedraw();
    }

    private void ResetState()
    {
        _hasCenter = false;
        _visibleSnaps.Clear();
        Context?.SetLineInputModeEnabled(false, LineInputLabelMode.Length);
        Context?.SetLength(null);
        Context?.SetArea(null);
        Context?.ResetLengthInput(null);
    }

    private PointF ResolveSnap(PointF world)
    {
        if (Context is null)
        {
            return world;
        }

        var tolerance = Context.SnapTolerance;
        var snap = Context.Session.SnapService.FindBestSnap(
            Context.Session.Document,
            world,
            tolerance,
            includeOnEdge: !_hasCenter);
        return snap.Resolve(world);
    }

    private Edge CreateTemplate()
    {
        var options = Context!.Session.LineToolOptions;
        var template = Edge.CreateStyleTemplate();
        template.Color = options.Color;
        template.Thickness = options.Thickness;
        template.LineType = options.LineType;
        return template;
    }

    private Pen CreatePreviewPen(double zoom)
    {
        if (Context is null)
        {
            return RenderStyles.EdgePen(zoom);
        }

        var options = Context.Session.LineToolOptions;
        var brush = new SolidColorBrush(options.Color);
        return RenderStyles.CreateScreenPen(brush, options.Thickness, zoom, GetDashArray(options.LineType));
    }

    private static DoubleCollection? GetDashArray(EdgeLineType lineType) => lineType switch
    {
        EdgeLineType.Dashed => [8, 4],
        EdgeLineType.Dotted => [2, 4],
        _ => null
    };

    private static Point ToPoint(PointF point)
        => new(point.X, point.Y);

    private static bool TryParseRadius(string text, out double radius)
    {
        radius = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out radius)
               && radius > 0;
    }
}
