using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class RotateTool : ToolBase
{
    private readonly List<SnapPoint> _visibleSnaps = [];
    private bool _hasPivot;
    private PointF _pivot;
    private PointF _cursorPoint;
    private MoveObjectSnapshot? _objectSnapshot;
    private double _sweepAngleDegrees;
    private double? _exactAngleDegrees;

    public override ToolId Id => ToolId.Rotate;

    public bool HasPivot => _hasPivot;

    public double PreviewAngleDegrees => _exactAngleDegrees ?? _sweepAngleDegrees;

    public override void OnActivated()
    {
        ResetOperation();
        Context?.SetStatus(GetIdleStatus());
    }

    public override void OnDeactivated()
    {
        DisableAngleInput();
        ResetOperation();
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

        if (!_hasPivot)
        {
            if (!RotateOperations.CanRotate(Context.Session.Selection))
            {
                Context.SetStatus(Strings.Input_Rotate_SelectObjects);
                e.Handled = true;
                return;
            }

            _pivot = ResolveSnap(world);
            _cursorPoint = _pivot;
            _objectSnapshot = MoveOperations.CreateSnapshot(
                Context.Session.Document,
                Context.Session.Selection);
            _hasPivot = true;
            _sweepAngleDegrees = 0;
            _exactAngleDegrees = null;
            EnableAngleInput();
            Context.SetStatus(Strings.Input_Rotate_SelectAngle);
            Context.ResetLengthInput(0);
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        _exactAngleDegrees = null;
        _cursorPoint = ResolveSnap(world);
        UpdateSweepFromCursor();
        CommitRotate(_sweepAngleDegrees);
        e.Handled = true;
    }

    public override void OnMouseMove(MouseEventArgs e, PointF world)
    {
        if (Context is null)
        {
            return;
        }

        var document = Context.Session.Document;
        var tolerance = Context.SnapTolerance;

        _visibleSnaps.Clear();
        foreach (var snap in Context.Session.SnapService.GetVisibleSnaps(document, world, tolerance))
        {
            _visibleSnaps.Add(snap);
        }

        if (_hasPivot)
        {
            _cursorPoint = ResolveSnap(world);
            _exactAngleDegrees = null;
            UpdateSweepFromCursor();
            Context.SetLength(Math.Abs(_sweepAngleDegrees));
        }

        Context.RequestRedraw();
        e.Handled = true;
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (Context is null)
        {
            return;
        }

        if (e.Key == Key.Escape && _hasPivot)
        {
            CancelOperation(Strings.Status_RotateCancelled);
            e.Handled = true;
            return;
        }

        if (!_hasPivot)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            var wasTyping = !string.IsNullOrWhiteSpace(Context.GetLineInputText());
            if (Context.ProcessLengthKey(e))
            {
                if (!wasTyping)
                {
                    CommitRotate(_sweepAngleDegrees);
                }

                e.Handled = true;
            }

            return;
        }

        if (Context.ProcessLengthKey(e))
        {
            e.Handled = true;
        }
    }

    public override bool TryApplyLength(double length)
    {
        if (!_hasPivot || Context is null || length <= 0)
        {
            return false;
        }

        return TryCommitExactAngle(length);
    }

    public override bool TryApplyLengthInput(string input)
    {
        if (!_hasPivot || Context is null || string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (!TryParseAngle(input, out var angle))
        {
            return false;
        }

        return TryCommitExactAngle(Math.Abs(angle));
    }

    public override bool TryApplyRectangleSize(string width, string height)
    {
        _ = width;
        _ = height;
        return false;
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

        if (!_hasPivot || _objectSnapshot is null)
        {
            return;
        }

        var angleRadians = PreviewAngleDegrees * Math.PI / 180.0;
        var pen = PreviewLineRenderer.CreateGhostPen(zoom, 1.5, [4, 2]);

        foreach (var entry in _objectSnapshot.Edges)
        {
            var start = RotateOperations.RotatePoint(entry.Start, _pivot, angleRadians);
            var end = RotateOperations.RotatePoint(entry.End, _pivot, angleRadians);
            context.DrawLine(
                pen,
                new Point(start.X, start.Y),
                new Point(end.X, end.Y));
        }
    }

    private bool TryCommitExactAngle(double magnitude)
    {
        if (Context is null || !_hasPivot || magnitude <= 0)
        {
            return false;
        }

        if (!HasRotationDirection())
        {
            Context.SetStatus(Strings.Input_Rotate_SetDirectionThenTypeAngle);
            return false;
        }

        var sign = Math.Sign(_sweepAngleDegrees);
        if (sign == 0)
        {
            sign = 1;
        }

        return CommitRotate(sign * SectorTool.NormalizeExactInputAngleDegrees(magnitude));
    }

    private bool CommitRotate(double sweepAngleDegrees)
    {
        if (Context is null || !_hasPivot || _objectSnapshot is null)
        {
            return false;
        }

        var topologyTolerance = TopologyTolerance.ForMutation;
        if (Math.Abs(sweepAngleDegrees) <= topologyTolerance)
        {
            CancelOperation(Strings.Status_RotateCancelled);
            return false;
        }

        Context.RecordUndo();
        var angleRadians = sweepAngleDegrees * Math.PI / 180.0;
        RotateOperations.ExecuteObjectRotate(
            Context.Session.Document,
            Context.Session.Selection,
            _objectSnapshot,
            _pivot,
            angleRadians);

        DisableAngleInput();
        ResetOperation();
        Context.SetStatus(Strings.Status_RotateCompleted);
        Context.RequestRedraw();
        return true;
    }

    private void UpdateSweepFromCursor()
    {
        _sweepAngleDegrees = SectorGeometry.ComputeSignedSweepDegrees(0, _cursorPoint, _pivot);
    }

    private bool HasRotationDirection()
    {
        if (!_hasPivot)
        {
            return false;
        }

        return MathUtils.Distance(_pivot, _cursorPoint) > Context!.SnapTolerance;
    }

    private void HandleRightClick()
    {
        if (_hasPivot)
        {
            DisableAngleInput();
            ResetOperation();
        }

        Context?.Session.Selection.Clear();
        Context?.SetStatus(Strings.Status_SelectionCleared);
        Context?.RequestRedraw();
    }

    private void CancelOperation(string status)
    {
        DisableAngleInput();
        ResetOperation();
        Context?.SetStatus(status);
        Context?.RequestRedraw();
    }

    private void ResetOperation()
    {
        _hasPivot = false;
        _objectSnapshot = null;
        _visibleSnaps.Clear();
        _sweepAngleDegrees = 0;
        _exactAngleDegrees = null;
    }

    private void EnableAngleInput()
    {
        Context?.SetLineInputModeEnabled(true, LineInputLabelMode.Angle);
    }

    private void DisableAngleInput()
    {
        Context?.SetLineInputModeEnabled(false, LineInputLabelMode.Length);
        Context?.SetLength(null);
        Context?.ResetLengthInput(null);
    }

    private PointF ResolveSnap(PointF world)
    {
        if (Context is null)
        {
            return world;
        }

        var snap = Context.Session.SnapService.FindBestSnap(
            Context.Session.Document,
            world,
            Context.SnapTolerance,
            includeOnEdge: true);

        return snap.Resolve(world);
    }

    private static string GetIdleStatus()
        => Strings.Input_Rotate_Idle;

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
