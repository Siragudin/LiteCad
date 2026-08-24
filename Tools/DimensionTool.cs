using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using LiteCad.UI;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class DimensionTool : ToolBase
{
    private readonly DimensionPreviewRenderer _previewRenderer = new();
    private Guid? _firstVertexId;
    private Guid? _secondVertexId;
    private PointF _firstAnchor;
    private PointF _secondAnchor;
    private double _offset;
    private double _lastOffset = 50;
    private bool _orthogonalIsHorizontal = true;
    private SnapPoint? _hoverVertexSnap;

    public override ToolId Id => ToolId.Dimension;

    public override void OnActivated()
    {
        ResetState();
        Context?.SetLineInputModeEnabled(false, LineInputLabelMode.Length);
        Context?.SetStatus(Strings.Input_Dimension_SelectFirstPoint);
    }

    public override void OnDeactivated()
    {
        ResetState();
        Context?.SetLineInputModeEnabled(false, LineInputLabelMode.Length);
        Context?.SetLength(null);
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

        if (_secondVertexId.HasValue)
        {
            CommitDimension();
            e.Handled = true;
            return;
        }

        if (!TryCommitMeasurementAnchor(world, out var vertexId, out var anchor))
        {
            e.Handled = true;
            return;
        }

        if (!_firstVertexId.HasValue)
        {
            _firstVertexId = vertexId;
            _firstAnchor = anchor;
            _hoverVertexSnap = null;
            Context.SetStatus(Strings.Input_Dimension_SelectSecondPoint);
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        if (_firstVertexId.Value == vertexId)
        {
            e.Handled = true;
            return;
        }

        _secondVertexId = vertexId;
        _secondAnchor = anchor;
        _hoverVertexSnap = null;
        _offset = _lastOffset;
        Context.SetLineInputModeEnabled(true, LineInputLabelMode.Offset);
        Context.ResetLengthInput(_offset);
        Context.SetLength(_offset);
        Context.SetStatus(Strings.Input_Dimension_SetOffset);
        UpdateOffsetFromCursor(world);
        Context.RequestRedraw();
        e.Handled = true;
    }

    public override void OnMouseMove(MouseEventArgs e, PointF world)
    {
        if (Context is null)
        {
            return;
        }

        if (!_secondVertexId.HasValue)
        {
            UpdateVertexHover(world);
            RequestOverlayRedraw();
            e.Handled = true;
            return;
        }

        UpdateOffsetFromCursor(world);
        Context.SetLength(_offset);
        RequestOverlayRedraw();
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
            CancelAndExit();
            e.Handled = true;
        }
    }

    public override bool TryApplyLengthInput(string input)
    {
        if (Context is null || !_secondVertexId.HasValue)
        {
            return false;
        }

        if (!TryParseOffset(input, out var offset))
        {
            return false;
        }

        _offset = offset;
        Context.SetLength(_offset);
        CommitDimension();
        return true;
    }

    public override bool TryApplyLength(double length)
    {
        if (Context is null || !_secondVertexId.HasValue)
        {
            return false;
        }

        _offset = length;
        CommitDimension();
        return true;
    }

    public override void RenderOverlay(DrawingContext context, Camera camera, Size viewport)
    {
        if (Context is null)
        {
            return;
        }

        if (_hoverVertexSnap is SnapPoint hoverSnap)
        {
            SnapRenderer.DrawSnapPoint(context, hoverSnap, camera.Zoom);
        }

        if (!_firstVertexId.HasValue)
        {
            return;
        }

        if (!_secondVertexId.HasValue)
        {
            DrawAnchorMarker(context, camera, _firstAnchor);
            return;
        }

        if (!DimensionGeometry.TryCreateLayout(
                _firstAnchor,
                _secondAnchor,
                _offset,
                Context.SnapTolerance,
                out var layout,
                IsOrthogonalEnabled(),
                _orthogonalIsHorizontal))
        {
            return;
        }

        var previewColor = PreviewLineRenderer.GetAnnotationPreviewColor();
        var distanceText = UnitDisplayFormatter.FormatLinear(
            layout.MeasuredDistance,
            Context.Session.DisplayUnitSettings.LinearUnit);
        _previewRenderer.Draw(
            context,
            layout,
            camera.Zoom,
            previewColor,
            camera,
            viewport,
            distanceText,
            Context.Session.DimensionToolOptions.ExtensionStyle,
            Context.Session.DimensionToolOptions.TextSize);
        DrawAnchorMarker(context, camera, layout.FirstAnchor);
        DrawAnchorMarker(context, camera, layout.SecondAnchor);
    }

    private void CommitDimension()
    {
        if (Context is null
            || !_firstVertexId.HasValue
            || !_secondVertexId.HasValue)
        {
            return;
        }

        var tolerance = Context.SnapTolerance;
        Context.RecordUndo();
        var dimension = DimensionService.Create(
            Context.Session.Document,
            _firstVertexId.Value,
            _secondVertexId.Value,
            _offset,
            tolerance,
            Context.Session.DimensionToolOptions.TextSize);
        dimension.ExtensionStyle = Context.Session.DimensionToolOptions.ExtensionStyle;
        dimension.IsOrthogonal = Context.Session.DimensionToolOptions.OrthoEnabled;
        dimension.OrthogonalIsHorizontal = _orthogonalIsHorizontal;

        _lastOffset = _offset;
        Context.SetLineInputModeEnabled(false, LineInputLabelMode.Length);
        Context.SetLength(null);
        Context.SetStatus(Strings.Status_DimensionCreated);
        ResetState();
        Context.RequestRedraw();
    }

    private void UpdateOffsetFromCursor(PointF world)
    {
        if (IsOrthogonalEnabled())
        {
            _orthogonalIsHorizontal = DimensionGeometry.ResolveOrthogonalIsHorizontal(
                _firstAnchor,
                _secondAnchor,
                world);
            _offset = DimensionGeometry.ComputeOrthogonalSignedOffset(
                _firstAnchor,
                _secondAnchor,
                world,
                _orthogonalIsHorizontal);
        }
        else
        {
            _offset = DimensionGeometry.ComputeSignedOffset(_firstAnchor, _secondAnchor, world);
        }

        if (Math.Abs(_offset) < Context?.SnapTolerance)
        {
            _offset = 0;
        }
    }

    private bool IsOrthogonalEnabled()
        => Context?.Session.DimensionToolOptions.OrthoEnabled == true;

    private void UpdateVertexHover(PointF world)
    {
        _hoverVertexSnap = TryFindMeasurementSnap(world, out var snap) ? snap : null;
    }

    internal SnapPoint? HoverMeasurementSnap => _hoverVertexSnap;

    internal DimensionPreviewRenderer PreviewRenderer => _previewRenderer;

    internal bool HasPendingOperation => _firstVertexId.HasValue;

    internal bool HasOffsetPhase => _secondVertexId.HasValue;

    private void HandleRightClick()
    {
        if (Context is null)
        {
            return;
        }

        var hadPending = _firstVertexId.HasValue;
        ResetState();
        Context.SetLineInputModeEnabled(false, LineInputLabelMode.Length);
        Context.SetLength(null);
        Context.Session.Selection.Clear();
        Context.SetStatus(hadPending ? Strings.Status_DimensionCancelled : Strings.Status_SelectionCleared);
        Context.RequestRedraw();
    }

    private void CancelAndExit()
    {
        ResetState();
        Context?.SetLineInputModeEnabled(false, LineInputLabelMode.Length);
        Context?.SetLength(null);
        Context?.SetStatus(Strings.Status_DimensionCancelled);
        Context?.ActivateSelectionTool();
        Context?.RequestRedraw();
    }

    private void ResetState()
    {
        _firstVertexId = null;
        _secondVertexId = null;
        _firstAnchor = PointF.Zero;
        _secondAnchor = PointF.Zero;
        _offset = _lastOffset;
        _orthogonalIsHorizontal = true;
        _hoverVertexSnap = null;
        _previewRenderer.Invalidate();
    }

    private bool TryFindMeasurementSnap(PointF world, out SnapPoint snap)
    {
        snap = default;
        if (Context is null)
        {
            return false;
        }

        var result = Context.Session.SnapService.FindBestSnap(
            Context.Session.Document,
            world,
            Context.SnapTolerance,
            includeOnEdge: true);

        if (!result.HasSnap)
        {
            return false;
        }

        snap = result.Snap!.Value;
        return IsMeasurementSnap(snap.Kind);
    }

    private bool TryCommitMeasurementAnchor(PointF world, out Guid vertexId, out PointF anchor)
    {
        vertexId = Guid.Empty;
        anchor = PointF.Zero;
        if (Context is null || !TryFindMeasurementSnap(world, out var snap))
        {
            return false;
        }

        return SnapService.TryResolveMeasurementAnchor(
            Context.Session.Document,
            snap,
            Context.SnapTolerance,
            out vertexId,
            out anchor);
    }

    private static bool IsMeasurementSnap(SnapKind kind)
        => kind is SnapKind.Endpoint
            or SnapKind.Intersection
            or SnapKind.AxisIntersection
            or SnapKind.Midpoint
            or SnapKind.OnEdge;

    private static bool TryParseOffset(string input, out double offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = input.Trim().Replace(',', '.');
        return double.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out offset);
    }

    private static void DrawAnchorMarker(DrawingContext context, Camera camera, PointF point)
    {
        var radius = 4.0 / camera.Zoom;
        context.DrawEllipse(
            Brushes.White,
            RenderStyles.CreateScreenPen(Brushes.ForestGreen, 1.5, camera.Zoom),
            new Point(point.X, point.Y),
            radius,
            radius);
    }
}
