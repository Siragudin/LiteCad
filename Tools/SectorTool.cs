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

public sealed class SectorTool : ToolBase
{
    private readonly List<SnapPoint> _visibleSnaps = [];
    private bool _hasCenter;
    private bool _hasStart;
    private PointF _center;
    private PointF _startPoint;
    private PointF _cursorPoint;
    private double _radius;
    private double _startAngleRadians;
    private double _sweepAngleDegrees;
    private SectorInputPhase _phase = SectorInputPhase.None;

    public override ToolId Id => ToolId.Sector;

    public override void OnActivated()
    {
        Context?.SetStatus(Strings.Input_Sector_SelectCenter);
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
            Cancel(Strings.Status_SectorCancelled);
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
            _cursorPoint = snapped;
            _phase = SectorInputPhase.Radius;
            Context.SetLineInputModeEnabled(true, LineInputLabelMode.Radius);
            Context.SetStatus(Strings.Input_Sector_SelectStart);
            Context.ResetLengthInput(null);
            Context.SetArea(null);
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        if (!_hasStart)
        {
            FixStartPoint(snapped);
            e.Handled = true;
            return;
        }

        CommitSector(_sweepAngleDegrees);
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

        _cursorPoint = ResolveSnap(world);

        if (!_hasCenter)
        {
            Context.SetLength(null);
            Context.SetArea(null);
        }
        else if (!_hasStart)
        {
            Context.SetLength(SectorGeometry.ComputeRadius(_center, _cursorPoint));
            Context.SetArea(null);
        }
        else
        {
            _sweepAngleDegrees = SectorGeometry.ComputeSignedSweepDegrees(_startAngleRadians, _cursorPoint, _center);
            Context.SetLength(Math.Abs(_sweepAngleDegrees));
            UpdatePreviewArea();
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
            Cancel(Strings.Status_SectorCancelled);
            e.Handled = true;
            return;
        }

        if (_phase == SectorInputPhase.Angle && e.Key == Key.Enter)
        {
            var wasTyping = !string.IsNullOrWhiteSpace(Context.GetLineInputText());
            if (Context.ProcessLengthKey(e))
            {
                if (!wasTyping)
                {
                    CommitSector(_sweepAngleDegrees);
                }

                e.Handled = true;
            }

            return;
        }

        if (_phase == SectorInputPhase.Radius && _hasCenter && Context.ProcessLengthKey(e))
        {
            e.Handled = true;
        }
    }

    public override bool TryApplyLength(double length)
    {
        if (Context is null || length <= 0)
        {
            return false;
        }

        if (_phase == SectorInputPhase.Radius && _hasCenter && !_hasStart)
        {
            return TryApplyRadius(length);
        }

        if (_phase == SectorInputPhase.Angle && _hasStart)
        {
            return TryCommitExactAngle(length);
        }

        return false;
    }

    public override bool TryApplyLengthInput(string input)
    {
        if (Context is null || string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (_phase == SectorInputPhase.Radius && _hasCenter && !_hasStart)
        {
            return TryParsePositiveNumber(input, out var radius) && TryApplyRadius(radius);
        }

        if (_phase == SectorInputPhase.Angle && _hasStart)
        {
            if (!TryParseAngle(input, out var angle))
            {
                return false;
            }

            return TryCommitExactAngle(Math.Abs(angle));
        }

        return false;
    }

    private bool TryCommitExactAngle(double magnitude)
    {
        if (Context is null || !_hasStart || magnitude <= 0)
        {
            return false;
        }

        var sign = Math.Sign(_sweepAngleDegrees);
        if (sign == 0)
        {
            sign = Math.Sign(magnitude);
        }

        if (sign == 0)
        {
            sign = 1;
        }

        CommitSector(sign * NormalizeExactInputAngleDegrees(magnitude));
        return true;
    }

    public static double NormalizeExactInputAngleDegrees(double magnitude)
        => Math.Abs(magnitude) > 360.0 ? 360.0 : Math.Abs(magnitude);

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

        if (!_hasCenter)
        {
            return;
        }

        var options = Context.Session.LineToolOptions;
        var previewPen = PreviewLineRenderer.CreatePen(
            zoom,
            options.Color,
            options.Thickness,
            GetDashArray(options.LineType));

        if (!_hasStart)
        {
            context.DrawLine(previewPen, ToPoint(_center), ToPoint(_cursorPoint));
            return;
        }

        if (!TryGetPreviewSweep(out var sweep))
        {
            return;
        }

        if (SectorGeometry.IsFullCircle(sweep))
        {
            DrawFullCirclePreview(context, previewPen);
            return;
        }

        var arcPoints = SectorGeometry.ComputeArcPoints(_center, _radius, _startAngleRadians, sweep);
        if (arcPoints.Length == 0)
        {
            return;
        }

        context.DrawLine(previewPen, ToPoint(_center), ToPoint(arcPoints[0]));
        for (var i = 0; i < arcPoints.Length - 1; i++)
        {
            context.DrawLine(previewPen, ToPoint(arcPoints[i]), ToPoint(arcPoints[i + 1]));
        }

        context.DrawLine(previewPen, ToPoint(arcPoints[^1]), ToPoint(_center));
    }

    private void FixStartPoint(PointF pointOnCircle)
    {
        if (Context is null)
        {
            return;
        }

        var topologyTolerance = TopologyTolerance.ForMutation;
        _radius = SectorGeometry.ComputeRadius(_center, pointOnCircle);
        if (_radius <= topologyTolerance)
        {
            Context.SetStatus(Strings.Error_SectorTooSmall);
            return;
        }

        _hasStart = true;
        _startPoint = pointOnCircle;
        _startAngleRadians = SectorGeometry.ComputeStartAngleRadians(_center, _startPoint);
        _sweepAngleDegrees = SectorGeometry.ComputeSignedSweepDegrees(_startAngleRadians, _cursorPoint, _center);
        _phase = SectorInputPhase.Angle;
        Context.SetLineInputModeEnabled(true, LineInputLabelMode.Angle);
        Context.SetStatus(Strings.Input_Sector_SelectAngle);
        Context.ResetLengthInput(Math.Abs(_sweepAngleDegrees));
        UpdatePreviewArea();
        Context.RequestRedraw();
    }

    private bool TryApplyRadius(double radius)
    {
        if (Context is null || !_hasCenter || _hasStart)
        {
            return false;
        }

        var topologyTolerance = TopologyTolerance.ForMutation;
        if (radius <= topologyTolerance)
        {
            Context.SetStatus(Strings.Error_SectorTooSmall);
            return false;
        }

        if (!HasRadiusDirection())
        {
            Context.SetStatus(Strings.Input_Sector_SetDirectionThenTypeRadius);
            return false;
        }

        var angle = SectorGeometry.ComputeStartAngleRadians(_center, _cursorPoint);
        var start = SectorGeometry.PointOnArc(_center, radius, angle);
        FixStartPoint(start);
        return true;
    }

    private void CommitSector(double sweepAngleDegrees)
    {
        if (Context is null || !_hasStart)
        {
            return;
        }

        var topologyTolerance = TopologyTolerance.ForMutation;
        if (_radius <= topologyTolerance)
        {
            Context.SetStatus(Strings.Error_SectorTooSmall);
            return;
        }

        if (Math.Abs(sweepAngleDegrees) <= topologyTolerance)
        {
            Context.SetStatus(Strings.Error_SectorAngleTooSmall);
            return;
        }

        Context.RecordUndo();

        var template = CreateTemplate();
        var document = Context.Session.Document;

        if (SectorGeometry.IsFullCircle(sweepAngleDegrees))
        {
            CommitFullCircle(document, template, topologyTolerance);
        }
        else
        {
            CommitPartialSector(document, template, topologyTolerance, sweepAngleDegrees);
        }

        PolygonBuilder.SyncFaces(document, topologyTolerance);

        ResetAfterCommit();
        Context.SetStatus(Strings.Input_Sector_SelectCenter);
        Context.RequestRedraw();
    }

    private void CommitFullCircle(CadDocument document, Edge template, double topologyTolerance)
    {
        var sweeps = SectorGeometry.ComputeSegmentSweepAngles(360.0);
        var currentAngle = _startAngleRadians;
        var points = new PointF[sweeps.Length];
        for (var i = 0; i < sweeps.Length; i++)
        {
            points[i] = SectorGeometry.PointOnArc(_center, _radius, currentAngle);
            currentAngle += sweeps[i] * Math.PI / 180.0;
        }

        for (var i = 0; i < sweeps.Length; i++)
        {
            var start = points[i];
            var end = points[(i + 1) % sweeps.Length];
            EdgeOperations.AddSegment(document, start, end, template, topologyTolerance);
        }
    }

    private void CommitPartialSector(
        CadDocument document,
        Edge template,
        double topologyTolerance,
        double sweepAngleDegrees)
    {
        var arcPoints = SectorGeometry.ComputeArcPoints(_center, _radius, _startAngleRadians, sweepAngleDegrees);
        if (arcPoints.Length < 2)
        {
            return;
        }

        EdgeOperations.AddSegment(document, _center, arcPoints[0], template, topologyTolerance);
        for (var i = 0; i < arcPoints.Length - 1; i++)
        {
            EdgeOperations.AddSegment(document, arcPoints[i], arcPoints[i + 1], template, topologyTolerance);
        }

        EdgeOperations.AddSegment(document, arcPoints[^1], _center, template, topologyTolerance);
    }

    private void DrawFullCirclePreview(DrawingContext context, Pen previewPen)
    {
        var sweeps = SectorGeometry.ComputeSegmentSweepAngles(360.0);
        var currentAngle = _startAngleRadians;
        var points = new PointF[sweeps.Length];
        for (var i = 0; i < sweeps.Length; i++)
        {
            points[i] = SectorGeometry.PointOnArc(_center, _radius, currentAngle);
            currentAngle += sweeps[i] * Math.PI / 180.0;
        }

        for (var i = 0; i < sweeps.Length; i++)
        {
            var start = points[i];
            var end = points[(i + 1) % sweeps.Length];
            context.DrawLine(previewPen, ToPoint(start), ToPoint(end));
        }
    }

    private bool TryGetPreviewSweep(out double sweep)
    {
        sweep = _sweepAngleDegrees;
        var topologyTolerance = TopologyTolerance.ForMutation;
        return _hasStart && _radius > topologyTolerance && Math.Abs(sweep) > topologyTolerance;
    }

    private bool HasRadiusDirection()
    {
        if (!_hasCenter)
        {
            return false;
        }

        return SectorGeometry.ComputeRadius(_center, _cursorPoint) > Context!.SnapTolerance;
    }

    private void UpdatePreviewArea()
    {
        if (Context is null || !_hasStart)
        {
            return;
        }

        var angleRadians = Math.Abs(_sweepAngleDegrees) * Math.PI / 180.0;
        Context.SetArea(0.5 * _radius * _radius * angleRadians);
    }

    private void ResetAfterCommit()
    {
        _hasCenter = false;
        _hasStart = false;
        _phase = SectorInputPhase.None;
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
        _hasStart = false;
        _phase = SectorInputPhase.None;
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

    private static DoubleCollection? GetDashArray(EdgeLineType lineType) => lineType switch
    {
        EdgeLineType.Dashed => [8, 4],
        EdgeLineType.Dotted => [2, 4],
        _ => null
    };

    private static Point ToPoint(PointF point)
        => new(point.X, point.Y);

    private static bool TryParsePositiveNumber(string text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && value > 0;
    }

    private static bool TryParseAngle(string text, out double angle)
    {
        angle = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out angle)
               && Math.Abs(angle) > 0;
    }
}

public enum SectorInputPhase
{
    None,
    Radius,
    Angle
}
