using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class MirrorTool : ToolBase
{
    private readonly List<SnapPoint> _visibleSnaps = [];
    private bool _hasAxisStart;
    private PointF _axisStart;
    private PointF _axisEnd;
    private MoveObjectSnapshot? _objectSnapshot;

    public override ToolId Id => ToolId.Mirror;

    public bool HasAxisStart => _hasAxisStart;

    public PointF PreviewAxisEnd => GetPreviewAxisEnd();

    public void NotifyOrthoChanged()
    {
        if (Context is null || !_hasAxisStart)
        {
            return;
        }

        Context.RequestRedraw();
    }

    public override void OnActivated()
    {
        ResetOperation();
        Context?.SetStatus(GetIdleStatus());
    }

    public override void OnDeactivated()
    {
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

        if (!_hasAxisStart)
        {
            if (!MirrorOperations.CanMirror(Context.Session.Selection))
            {
                Context.SetStatus(Strings.Input_Mirror_SelectObjects);
                e.Handled = true;
                return;
            }

            _axisStart = ResolveSnap(world);
            _axisEnd = _axisStart;
            _objectSnapshot = MoveOperations.CreateSnapshot(
                Context.Session.Document,
                Context.Session.Selection);
            _hasAxisStart = true;
            Context.SetStatus(Strings.Input_Mirror_SelectAxisEnd);
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        _axisEnd = ResolveSnapWithOrtho(world);
        CommitMirror(_axisStart, GetPreviewAxisEnd());
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

        if (_hasAxisStart)
        {
            _axisEnd = ResolveSnapWithOrtho(world);
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

        if (e.Key == Key.Escape && _hasAxisStart)
        {
            CancelOperation(Strings.Status_MirrorCancelled);
            e.Handled = true;
        }
    }

    public override bool TryApplyLength(double length)
    {
        _ = length;
        return false;
    }

    public override bool TryApplyLengthInput(string input)
    {
        _ = input;
        return false;
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

        if (!_hasAxisStart || _objectSnapshot is null)
        {
            return;
        }

        var axisEnd = GetPreviewAxisEnd();
        var axisPen = RenderStyles.CreateScreenPen(
            new SolidColorBrush(Color.FromArgb(0xA0, 0xFF, 0x98, 0x00)),
            1.0,
            zoom,
            [6, 4]);
        context.DrawLine(
            axisPen,
            new Point(_axisStart.X, _axisStart.Y),
            new Point(axisEnd.X, axisEnd.Y));

        var previewPen = RenderStyles.CreateScreenPen(
            new SolidColorBrush(Color.FromArgb(0xB0, 0x21, 0x96, 0xF3)),
            1.5,
            zoom,
            [4, 2]);

        foreach (var entry in _objectSnapshot.Edges)
        {
            var start = MirrorOperations.MirrorPoint(entry.Start, _axisStart, axisEnd);
            var end = MirrorOperations.MirrorPoint(entry.End, _axisStart, axisEnd);
            context.DrawLine(
                previewPen,
                new Point(start.X, start.Y),
                new Point(end.X, end.Y));
        }
    }

    private bool CommitMirror(PointF axisStart, PointF axisEnd)
    {
        if (Context is null || !_hasAxisStart || _objectSnapshot is null)
        {
            return false;
        }

        if (!MirrorOperations.IsValidAxis(axisStart, axisEnd))
        {
            CancelOperation(Strings.Status_MirrorCancelled);
            return false;
        }

        Context.RecordUndo();
        MirrorOperations.ExecuteObjectMirror(
            Context.Session.Document,
            Context.Session.Selection,
            _objectSnapshot,
            axisStart,
            axisEnd);

        ResetOperation();
        Context.SetStatus(Strings.Status_MirrorCompleted);
        Context.RequestRedraw();
        return true;
    }

    private void HandleRightClick()
    {
        if (_hasAxisStart)
        {
            ResetOperation();
        }

        Context?.Session.Selection.Clear();
        Context?.SetStatus(Strings.Status_SelectionCleared);
        Context?.RequestRedraw();
    }

    private void CancelOperation(string status)
    {
        ResetOperation();
        Context?.SetStatus(status);
        Context?.RequestRedraw();
    }

    private void ResetOperation()
    {
        _hasAxisStart = false;
        _objectSnapshot = null;
        _visibleSnaps.Clear();
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

    private PointF GetPreviewAxisEnd()
    {
        if (!_hasAxisStart)
        {
            return _axisEnd;
        }

        if (IsOrthoEnabled())
        {
            return Geometry2D.ApplyOrtho(_axisStart, _axisEnd);
        }

        return _axisEnd;
    }

    private PointF ResolveSnapWithOrtho(PointF world)
    {
        var resolved = ResolveSnap(world);
        if (IsOrthoEnabled() && _hasAxisStart)
        {
            resolved = Geometry2D.ApplyOrtho(_axisStart, resolved);
        }

        return resolved;
    }

    private bool IsOrthoEnabled()
        => Context?.Session.MirrorToolOptions.OrthoEnabled == true;

    private static string GetIdleStatus()
        => Strings.Input_Mirror_Idle;
}
