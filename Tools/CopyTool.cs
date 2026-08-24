using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class CopyTool : ToolBase
{
    private readonly List<SnapPoint> _visibleSnaps = [];
    private bool _isActive;
    private PointF _basePoint;
    private PointF _previewPoint;
    private MoveObjectSnapshot? _objectSnapshot;
    private PointF? _exactDelta;

    public override ToolId Id => ToolId.Copy;

    public bool IsActive => _isActive;

    public PointF PreviewDelta => GetPreviewDelta();

    public bool BeginCopyFromSelection()
    {
        if (Context is null)
        {
            return false;
        }

        var selection = Context.Session.Selection;
        if (!CopyOperations.CanCopy(selection))
        {
            return false;
        }

        var document = Context.Session.Document;
        _objectSnapshot = MoveOperations.CreateSnapshot(document, selection);
        _basePoint = CopyOperations.GetBasePoint(_objectSnapshot);
        _previewPoint = _basePoint;
        _exactDelta = null;
        _isActive = true;
        Context.SetStatus(Strings.Input_Copy_SelectDestination);
        Context.RequestRedraw();
        return true;
    }

    public override void OnActivated()
    {
        ResetOperation();
        if (!BeginCopyFromSelection())
        {
            Context?.SetStatus(Strings.Input_Copy_SelectObjects);
        }
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
            if (_isActive)
            {
                CancelOperation(Strings.Status_CopyCancelled);
            }

            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left || !_isActive)
        {
            return;
        }

        if (_exactDelta is null)
        {
            _previewPoint = ResolveSnapWithOrtho(world);
        }

        CommitCopy();
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

        if (_isActive)
        {
            _previewPoint = ResolveSnapWithOrtho(world);
            _exactDelta = null;
        }

        RequestOverlayRedraw();
        e.Handled = true;
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (Context is null)
        {
            return;
        }

        if (e.Key == Key.Escape && _isActive)
        {
            CancelOperation(Strings.Status_CopyCancelled);
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

        if (!_isActive || _objectSnapshot is null)
        {
            return;
        }

        var delta = GetPreviewDelta();
        foreach (var entry in _objectSnapshot.Edges)
        {
            var pen = PreviewLineRenderer.CreatePen(
                zoom,
                entry.Template.Color,
                entry.Template.Thickness,
                GetDashArray(entry.Template.LineType));
            var start = Translate(entry.Start, delta);
            var end = Translate(entry.End, delta);
            context.DrawLine(
                pen,
                new Point(start.X, start.Y),
                new Point(end.X, end.Y));
        }
    }

    private bool CommitCopy()
    {
        if (Context is null || !_isActive || _objectSnapshot is null)
        {
            return false;
        }

        var delta = GetPreviewDelta();
        if (Math.Abs(delta.X) < 1e-9 && Math.Abs(delta.Y) < 1e-9)
        {
            return false;
        }

        Context.RecordUndo();
        CopyOperations.ExecuteObjectCopy(
            Context.Session.Document,
            Context.Session.Selection,
            _objectSnapshot,
            delta);

        ResetOperation();
        Context.SetStatus(Strings.Status_CopyCompleted);
        Context.RequestRedraw();
        return true;
    }

    private void CancelOperation(string status)
    {
        ResetOperation();
        Context?.SetStatus(status);
        Context?.RequestRedraw();
    }

    private void ResetOperation()
    {
        _isActive = false;
        _objectSnapshot = null;
        _visibleSnaps.Clear();
        _exactDelta = null;
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

    private PointF ResolveSnapWithOrtho(PointF world)
    {
        var resolved = ResolveSnap(world);
        if (IsOrthoEnabled() && _isActive)
        {
            resolved = Geometry2D.ApplyOrtho(_basePoint, resolved);
        }

        return resolved;
    }

    private PointF GetPreviewDelta()
    {
        if (_exactDelta is PointF exact)
        {
            return exact;
        }

        var point = _previewPoint;
        if (IsOrthoEnabled())
        {
            point = Geometry2D.ApplyOrtho(_basePoint, point);
        }

        return new PointF(point.X - _basePoint.X, point.Y - _basePoint.Y);
    }

    private bool IsOrthoEnabled()
        => Context?.Session.LineToolOptions.OrthoEnabled == true;

    private static PointF Translate(PointF point, PointF delta)
        => new(point.X + delta.X, point.Y + delta.Y);

    private static DoubleCollection? GetDashArray(EdgeLineType lineType) => lineType switch
    {
        EdgeLineType.Dashed => [8, 4],
        EdgeLineType.Dotted => [2, 4],
        _ => null
    };
}
