using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
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
    private double _lastSignedSagitta;
    private double? _signedSagittaOverride;

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
            _lastSignedSagitta = 0;
            _signedSagittaOverride = null;
            Context.SetLineInputModeEnabled(true, LineInputLabelMode.ArcHeight);
            Context.SetStatus(Strings.Input_Arc_SelectHeight);
            Context.ResetLengthInput(null);
            UpdatePreviewSagittaDisplay();
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        if (TryResolvePreview(out var radius, out var bendPoint) && CommitArc(radius, bendPoint))
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

        if (string.IsNullOrWhiteSpace(Context.GetLineInputText()))
        {
            _signedSagittaOverride = null;
        }

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
        else if (_signedSagittaOverride is null
                 && ArcGeometry.TryGetSignedSagitta(_startPoint, _endPoint, _cursorPoint, out var signedSagitta))
        {
            _lastSignedSagitta = signedSagitta;
            UpdatePreviewSagittaDisplay();
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
                if (!wasTyping && TryResolvePreview(out var radius, out var bendPoint))
                {
                    CommitArc(radius, bendPoint);
                }

                e.Handled = true;
            }

            return;
        }

        if (Context.ProcessLengthKey(e))
        {
            e.Handled = true;
            return;
        }

        TryUpdatePreviewFromInputText();
        UpdatePreviewSagittaDisplay();
        Context.RequestRedraw();
    }

    public override bool TryApplyLength(double arcHeightMillimeters)
    {
        if (!_hasEnd || Context is null || arcHeightMillimeters <= 0)
        {
            return false;
        }

        if (!TrySetPreviewSagitta(arcHeightMillimeters))
        {
            Context.SetStatus(Strings.Error_ArcHeightTooSmall);
            return false;
        }

        if (!TryResolvePreview(out var radius, out var bendPoint))
        {
            Context.SetStatus(Strings.Error_ArcHeightTooSmall);
            return false;
        }

        return CommitArc(radius, bendPoint);
    }

    public override bool TryApplyLengthInput(string input)
        => TryApplyLengthFromInput(input, commit: true);

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

        if (!_hasEnd)
        {
            PreviewLineRenderer.Draw(context, _startPoint, _cursorPoint, previewPen);
            return;
        }

        if (!TryResolvePreview(out var radius, out var bendPoint)
            || !ArcGeometry.TryBuildArc(_startPoint, _endPoint, radius, bendPoint, out var center, out var startAngle, out var sweep))
        {
            return;
        }

        var points = ArcGeometry.ComputeOpenArcPoints(center, radius, startAngle, sweep);
        for (var i = 0; i < points.Length - 1; i++)
        {
            context.DrawLine(previewPen, ToPoint(points[i]), ToPoint(points[i + 1]));
        }
    }

    private bool TryResolvePreview(out double radius, out PointF bendPoint)
    {
        radius = 0;
        bendPoint = default;

        if (!_hasEnd || Context is null)
        {
            return false;
        }

        var signedSagitta = ResolveSignedSagitta();
        if (!signedSagitta.HasValue)
        {
            return false;
        }

        var chord = MathUtils.Distance(_startPoint, _endPoint);
        if (!ArcGeometry.TryComputeRadiusFromSignedSagitta(chord, signedSagitta.Value, out radius))
        {
            return false;
        }

        return ArcGeometry.TryCreateBendPoint(_startPoint, _endPoint, signedSagitta.Value, out bendPoint);
    }

    private double? ResolveSignedSagitta()
    {
        if (_signedSagittaOverride.HasValue)
        {
            return _signedSagittaOverride.Value;
        }

        if (ArcGeometry.TryGetSignedSagitta(_startPoint, _endPoint, _cursorPoint, out var signedSagitta))
        {
            _lastSignedSagitta = signedSagitta;
            return signedSagitta;
        }

        if (Math.Abs(_lastSignedSagitta) > TopologyTolerance.ForMutation)
        {
            return _lastSignedSagitta;
        }

        return null;
    }

    private bool TrySetPreviewSagitta(double arcHeightMillimeters)
    {
        if (arcHeightMillimeters <= TopologyTolerance.ForMutation)
        {
            return false;
        }

        var sign = Math.Abs(_lastSignedSagitta) > TopologyTolerance.ForMutation
            ? Math.Sign(_lastSignedSagitta)
            : 1;
        _signedSagittaOverride = sign * arcHeightMillimeters;
        return true;
    }

    internal bool TryApplyLengthFromInput(string input, bool commit)
    {
        if (!_hasEnd || Context is null)
        {
            return false;
        }

        if (!LinearInputParser.TryParsePositiveDistance(
                input,
                Context.Session.DisplayUnitSettings.LinearUnit,
                out var arcHeightMillimeters))
        {
            return false;
        }

        if (!TrySetPreviewSagitta(arcHeightMillimeters))
        {
            return false;
        }

        UpdatePreviewSagittaDisplay();
        Context.RequestRedraw();

        if (!commit)
        {
            return true;
        }

        if (!TryResolvePreview(out var radius, out var bendPoint))
        {
            return false;
        }

        return CommitArc(radius, bendPoint);
    }

    private void TryUpdatePreviewFromInputText()
    {
        if (Context is null || !_hasEnd)
        {
            return;
        }

        var text = Context.GetLineInputText();
        if (string.IsNullOrWhiteSpace(text))
        {
            _signedSagittaOverride = null;
            return;
        }

        if (LinearInputParser.TryParsePositiveDistance(
                text,
                Context.Session.DisplayUnitSettings.LinearUnit,
                out var arcHeightMillimeters))
        {
            TrySetPreviewSagitta(arcHeightMillimeters);
        }
    }

    private bool CommitArc(double radius, PointF bendPoint)
    {
        if (Context is null || !_hasEnd)
        {
            return false;
        }

        if (radius < GetMinimumRadius())
        {
            Context.SetStatus(Strings.Error_ArcHeightTooSmall);
            return false;
        }

        if (!ArcGeometry.TryBuildArc(_startPoint, _endPoint, radius, bendPoint, out var center, out var startAngle, out var sweep))
        {
            Context.SetStatus(Strings.Error_ArcHeightTooSmall);
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
        Context.Session.SnapService.InvalidateCache();

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

    private void UpdatePreviewSagittaDisplay()
    {
        if (Context is null || !_hasEnd)
        {
            return;
        }

        var signedSagitta = ResolveSignedSagitta();
        Context.SetLength(signedSagitta.HasValue ? Math.Abs(signedSagitta.Value) : null);
        Context.SetArea(null);
    }

    private void ResetAfterCommit()
    {
        _hasStart = false;
        _hasEnd = false;
        _lastSignedSagitta = 0;
        _signedSagittaOverride = null;
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
        _lastSignedSagitta = 0;
        _signedSagittaOverride = null;
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

        return Context.Session.SnapService.ResolveDrawingSnap(
            Context.Session.Document,
            world,
            null,
            Context.SnapTolerance,
            orthoEnabled: false,
            includeOnEdge: true);
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
}
