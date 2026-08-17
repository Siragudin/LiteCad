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

public sealed class ArcTool : ToolBase
{
    private readonly List<SnapPoint> _visibleSnaps = [];
    private bool _hasStart;
    private bool _hasEnd;
    private PointF _startPoint;
    private PointF _endPoint;
    private PointF _cursorPoint;

    public override ToolId Id => ToolId.Arc;

    public override void OnActivated()
    {
        Context?.SetStatus(Strings.Input_Arc_SelectStart);
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
            Cancel(Strings.Status_ArcCancelled);
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
            _cursorPoint = snapped;
            Context.SetLineInputModeEnabled(false, LineInputLabelMode.Length);
            Context.SetStatus(Strings.Input_Arc_SelectEnd);
            Context.ResetLengthInput(null);
            Context.SetArea(null);
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        if (!_hasEnd)
        {
            if (MathUtils.ArePointsEqual(_startPoint, snapped, Context.SnapTolerance))
            {
                Context.SetStatus(Strings.Error_ArcTooSmall);
                e.Handled = true;
                return;
            }

            _hasEnd = true;
            _endPoint = snapped;
            _cursorPoint = snapped;
            Context.SetLineInputModeEnabled(true, LineInputLabelMode.Radius);
            Context.SetStatus(Strings.Input_Arc_SelectRadius);
            Context.ResetLengthInput(null);
            UpdatePreviewRadiusDisplay();
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        if (TryResolvePreviewRadius(out var radius) && CommitArc(radius))
        {
            e.Handled = true;
        }
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

        _cursorPoint = ResolveSnap(world);

        if (!_hasStart)
        {
            Context.SetLength(null);
            Context.SetArea(null);
        }
        else if (!_hasEnd)
        {
            Context.SetLength(MathUtils.Distance(_startPoint, _cursorPoint));
            Context.SetArea(null);
        }
        else
        {
            UpdatePreviewRadiusDisplay();
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
            Cancel(Strings.Status_ArcCancelled);
            e.Handled = true;
            return;
        }

        if (!_hasEnd)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            var wasTyping = !string.IsNullOrWhiteSpace(Context.GetLineInputText());
            if (Context.ProcessLengthKey(e))
            {
                if (!wasTyping && TryResolvePreviewRadius(out var radius))
                {
                    CommitArc(radius);
                }

                e.Handled = true;
            }

            return;
        }

        if (Context.ProcessLengthKey(e))
        {
            UpdatePreviewRadiusDisplay();
            Context.RequestRedraw();
            e.Handled = true;
        }
    }

    public override bool TryApplyLength(double length)
    {
        if (!_hasEnd || Context is null || length <= 0)
        {
            return false;
        }

        return CommitArc(length);
    }

    public override bool TryApplyLengthInput(string input)
    {
        if (!_hasEnd || Context is null)
        {
            return false;
        }

        if (!TryParseRadius(input, out var radius))
        {
            return false;
        }

        return CommitArc(radius);
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

        var previewPen = CreatePreviewPen(zoom);

        if (!_hasEnd)
        {
            context.DrawLine(previewPen, ToPoint(_startPoint), ToPoint(_cursorPoint));
            return;
        }

        if (!TryResolvePreviewRadius(out var radius)
            || !ArcGeometry.TryBuildArc(_startPoint, _endPoint, radius, _cursorPoint, out var center, out var startAngle, out var sweep))
        {
            return;
        }

        var points = ArcGeometry.ComputeOpenArcPoints(center, radius, startAngle, sweep);
        for (var i = 0; i < points.Length - 1; i++)
        {
            context.DrawLine(previewPen, ToPoint(points[i]), ToPoint(points[i + 1]));
        }
    }

    private bool TryResolvePreviewRadius(out double radius)
    {
        radius = 0;
        if (!_hasEnd || Context is null)
        {
            return false;
        }

        var text = Context.GetLineInputText();
        if (TryParseRadius(text, out var typedRadius) && typedRadius >= GetMinimumRadius())
        {
            radius = typedRadius;
            return true;
        }

        return ArcGeometry.TryComputeRadiusFromSagitta(_startPoint, _endPoint, _cursorPoint, out radius);
    }

    private bool CommitArc(double radius)
    {
        if (Context is null || !_hasEnd)
        {
            return false;
        }

        if (radius < GetMinimumRadius())
        {
            Context.SetStatus(Strings.Error_ArcRadiusTooSmall);
            return false;
        }

        if (!ArcGeometry.TryBuildArc(_startPoint, _endPoint, radius, _cursorPoint, out var center, out var startAngle, out var sweep))
        {
            Context.SetStatus(Strings.Error_ArcRadiusTooSmall);
            return false;
        }

        var points = ArcGeometry.ComputeOpenArcPoints(center, radius, startAngle, sweep);
        if (points.Length < 2)
        {
            Context.SetStatus(Strings.Error_ArcTooSmall);
            return false;
        }

        Context.RecordUndo();

        var template = CreateTemplate();
        var document = Context.Session.Document;
        var topologyTolerance = TopologyTolerance.ForMutation;
        for (var i = 0; i < points.Length - 1; i++)
        {
            EdgeOperations.AddSegment(document, points[i], points[i + 1], template, topologyTolerance);
        }

        PolygonBuilder.SyncFaces(document, topologyTolerance);

        ResetAfterCommit();
        Context.SetStatus(Strings.Input_Arc_SelectStart);
        Context.RequestRedraw();
        return true;
    }

    private double GetMinimumRadius()
    {
        if (!_hasEnd)
        {
            return 0;
        }

        return MathUtils.Distance(_startPoint, _endPoint) / 2;
    }

    private void UpdatePreviewRadiusDisplay()
    {
        if (Context is null || !_hasEnd)
        {
            return;
        }

        Context.SetLength(TryResolvePreviewRadius(out var radius) ? radius : null);
        Context.SetArea(null);
    }

    private void ResetAfterCommit()
    {
        _hasStart = false;
        _hasEnd = false;
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
        _hasStart = false;
        _hasEnd = false;
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
            includeOnEdge: !_hasStart);
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
