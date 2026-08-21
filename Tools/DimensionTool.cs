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
            Cancel();
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

        if (!TryResolveVertexSnap(world, out var vertexId, out var anchor))
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
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        UpdateOffsetFromCursor(world);
        Context.SetLength(_offset);
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
            Cancel();
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

        var previewColor = Color.FromRgb(0x43, 0xA0, 0x47);
        var distanceText = UnitDisplayFormatter.FormatLinear(
            layout.MeasuredDistance,
            Context.Session.DisplayUnitSettings.LinearUnit);
        DimensionAnnotationDrawing.Draw(
            context,
            layout,
            _offset,
            camera.Zoom,
            previewColor,
            isSelected: false,
            camera,
            viewport,
            distanceText: distanceText,
            extensionStyle: Context.Session.DimensionToolOptions.ExtensionStyle,
            textWorldHeight: Context.Session.DimensionToolOptions.TextSize);
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
        _hoverVertexSnap = null;
        if (TryResolveVertexSnap(world, out var vertexId, out var anchor))
        {
            _hoverVertexSnap = new SnapPoint(anchor, SnapKind.Endpoint, vertexId: vertexId);
        }
    }

    private void Cancel()
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
    }

    private bool TryResolveVertexSnap(PointF world, out Guid vertexId, out PointF anchor)
    {
        vertexId = Guid.Empty;
        anchor = PointF.Zero;
        if (Context is null)
        {
            return false;
        }

        var result = Context.Session.SnapService.FindBestSnap(
            Context.Session.Document,
            world,
            Context.SnapTolerance,
            includeOnEdge: false);

        if (!result.HasSnap)
        {
            return false;
        }

        return SnapService.TryResolveMeasurementAnchor(
            Context.Session.Document,
            result.Snap!.Value,
            Context.SnapTolerance,
            out vertexId,
            out anchor);
    }

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
