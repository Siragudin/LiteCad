using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class StretchTool : ToolBase
{
    private readonly List<SnapPoint> _visibleSnaps = [];
    private bool _hasBasePoint;
    private PointF _basePoint;
    private PointF _previewPoint;
    private StretchPlan? _plan;
    private Guid? _hoveredEdgeId;
    private bool _distanceInputEnabled;

    public override ToolId Id => ToolId.Stretch;

    public bool HasValidPlan => _plan is not null;

    public bool HasActiveOperation => _hasBasePoint;

    internal PointF PreviewDelta => GetPreviewDelta();

    public PointF ProjectedPreviewDelta
        => _plan is null ? PointF.Zero : StretchOperations.ProjectDeltaOntoNormal(GetPreviewDelta(), _plan.Normal);

    public override void OnActivated()
    {
        ResetOperation();
        TryLoadPlanFromSelection();
        Context?.SetStatus(GetIdleStatus());
    }

    public override void OnDeactivated()
    {
        DisableDistanceInput();
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

        if (!_hasBasePoint)
        {
            if (TryPickEdgeAt(world, out var edgeId))
            {
                if (_plan is null || _plan.EdgeId != edgeId)
                {
                    TrySelectEdge(edgeId);
                    e.Handled = true;
                    return;
                }
            }

            if (_plan is null)
            {
                e.Handled = true;
                return;
            }

            _basePoint = ResolveSnap(world);
            _previewPoint = _basePoint;
            _hasBasePoint = true;
            SyncDistanceInput();
            Context.SetStatus(Strings.Input_Stretch_SelectDestination);
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        CommitStretch(ResolveSnap(world));
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

        if (!_hasBasePoint)
        {
            UpdateHoveredEdge(world, tolerance);
        }
        else if (_plan is not null)
        {
            _previewPoint = ResolveSnap(world);
            UpdateDistancePreviewDisplay();
            Context.RequestRedraw();
        }
        else
        {
            Context.RequestRedraw();
        }

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
            CancelActiveOperation(Strings.Status_StretchCancelled);
            e.Handled = true;
            return;
        }

        if (_hasBasePoint && Context.ProcessLengthKey(e))
        {
            e.Handled = true;
        }
    }

    public override bool TryApplyLength(double length)
    {
        if (!_hasBasePoint || Context is null || _plan is null)
        {
            return false;
        }

        if (!TryComputeStretchDeltaFromDistance(length, out var delta))
        {
            Context.SetStatus(Strings.Input_SetDirectionThenTypeDistance);
            return false;
        }

        return CommitStretchWithDelta(delta);
    }

    public override bool TryApplyLengthInput(string input)
    {
        if (!_hasBasePoint || Context is null || _plan is null)
        {
            return false;
        }

        if (!MoveOffsetInputParser.TryParseDistance(input, out var distance))
        {
            return false;
        }

        if (!TryComputeStretchDeltaFromDistance(distance, out var delta))
        {
            Context.SetStatus(Strings.Input_SetDirectionThenTypeDistance);
            return false;
        }

        return CommitStretchWithDelta(delta);
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

        if (_hoveredEdgeId is Guid hoveredEdgeId
            && (_plan is null || _plan.EdgeId != hoveredEdgeId))
        {
            DrawEdgeHighlight(
                context,
                Context.Session.Document,
                hoveredEdgeId,
                zoom,
                Color.FromArgb(0xB0, 0xFF, 0x98, 0x00),
                2.5);
        }

        if (_plan is null || !_hasBasePoint)
        {
            return;
        }

        var delta = GetPreviewDelta();
        var move = StretchOperations.ProjectDeltaOntoNormal(delta, _plan.Normal);
        var pen = RenderStyles.CreateScreenPen(
            new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0x98, 0x00)),
            2.0,
            zoom,
            [4, 2]);

        var newStart = new Point(
            _plan.StartPosition.X + move.X,
            _plan.StartPosition.Y + move.Y);
        var newEnd = new Point(
            _plan.EndPosition.X + move.X,
            _plan.EndPosition.Y + move.Y);

        context.DrawLine(pen, newStart, newEnd);

        var document = Context.Session.Document;
        DrawConnectedSideEdges(context, pen, document, _plan.StartVertexId, newStart, _plan.EdgeId);
        DrawConnectedSideEdges(context, pen, document, _plan.EndVertexId, newEnd, _plan.EdgeId);
    }

    private bool TrySelectEdge(Guid edgeId)
    {
        if (Context is null)
        {
            return false;
        }

        if (!StretchOperations.TryCreatePlan(
                Context.Session.Document,
                edgeId,
                TopologyTolerance.ForMutation,
                out var plan))
        {
            Context.SetStatus(Strings.Error_StretchUnavailable);
            return false;
        }

        var selection = Context.Session.Selection;
        selection.Clear();
        selection.SelectedEdgeIds.Add(edgeId);
        _plan = plan;
        _hasBasePoint = false;
        _hoveredEdgeId = null;
        SyncDistanceInput();
        Context.SetStatus(Strings.Input_Stretch_SelectBasePoint);
        Context.RequestRedraw();
        return true;
    }

    private bool TryPickEdgeAt(PointF world, out Guid edgeId)
    {
        edgeId = Guid.Empty;
        if (Context is null)
        {
            return false;
        }

        var document = Context.Session.Document;
        var tolerance = Context.SnapTolerance;
        Edge? closestEdge = null;
        var closestDistance = tolerance;

        foreach (var edge in document.Edges)
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            if (!Geometry2D.TryProjectPointOnSegment(world, start, end, out _, out var distance, tolerance)
                || distance > closestDistance)
            {
                continue;
            }

            closestEdge = edge;
            closestDistance = distance;
        }

        if (closestEdge is null)
        {
            return false;
        }

        edgeId = closestEdge.Id;
        return true;
    }

    private void UpdateHoveredEdge(PointF world, double tolerance)
    {
        _hoveredEdgeId = null;
        if (Context is null || !TryPickEdgeAt(world, out var edgeId))
        {
            return;
        }

        if (StretchOperations.TryCreatePlan(
                Context.Session.Document,
                edgeId,
                TopologyTolerance.ForMutation,
                out _))
        {
            _hoveredEdgeId = edgeId;
        }
    }

    private void CommitStretch(PointF destination)
    {
        if (Context is null || !_hasBasePoint || _plan is null)
        {
            return;
        }

        var delta = new PointF(destination.X - _basePoint.X, destination.Y - _basePoint.Y);
        CommitStretchWithDelta(delta);
    }

    private bool CommitStretchWithDelta(PointF delta)
    {
        if (Context is null || !_hasBasePoint || _plan is null)
        {
            return false;
        }

        var move = StretchOperations.ProjectDeltaOntoNormal(delta, _plan.Normal);
        if (Math.Abs(move.X) < 1e-9 && Math.Abs(move.Y) < 1e-9)
        {
            CancelActiveOperation(Strings.Status_StretchCancelled);
            return false;
        }

        Context.RecordUndo();
        StretchOperations.ApplyStretch(Context.Session.Document, _plan, delta);
        FinishCommittedOperation();
        return true;
    }

    private void FinishCommittedOperation()
    {
        DisableDistanceInput();
        _hasBasePoint = false;
        _previewPoint = PointF.Zero;
        _visibleSnaps.Clear();
        TryLoadPlanFromSelection();
        Context?.SetStatus(Strings.Status_StretchCompleted);
        Context?.RequestRedraw();
    }

    private bool TryLoadPlanFromSelection()
    {
        if (Context is null)
        {
            return false;
        }

        var selection = Context.Session.Selection;
        if (selection.SelectedEdgeIds.Count != 1
            || selection.SelectedPolygonIds.Count > 0
            || selection.SelectedVertexIds.Count > 0)
        {
            _plan = null;
            if (!_hasBasePoint)
            {
                Context.SetStatus(GetIdleStatus());
            }

            return false;
        }

        var edgeId = selection.SelectedEdgeIds.First();
        if (!StretchOperations.TryCreatePlan(
                Context.Session.Document,
                edgeId,
                TopologyTolerance.ForMutation,
                out var plan))
        {
            _plan = null;
            Context.SetStatus(Strings.Error_StretchUnavailable);
            return false;
        }

        _plan = plan;
        if (!_hasBasePoint)
        {
            Context.SetStatus(Strings.Input_Stretch_SelectBasePoint);
        }

        return true;
    }

    private bool TryComputeStretchDeltaFromDistance(double distance, out PointF delta)
    {
        delta = default;
        if (Context is null || _plan is null)
        {
            return false;
        }

        var magnitude = Math.Abs(distance);
        if (magnitude <= 0)
        {
            return false;
        }

        var direction = StretchOperations.ProjectDeltaOntoNormal(GetPreviewDelta(), _plan.Normal);
        var directionLength = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        if (directionLength <= Context.SnapTolerance)
        {
            return false;
        }

        var scale = magnitude / directionLength;
        delta = new PointF(direction.X * scale, direction.Y * scale);
        return true;
    }

    private void SyncDistanceInput()
    {
        if (Context is null)
        {
            return;
        }

        if (_hasBasePoint && _plan is not null)
        {
            if (!_distanceInputEnabled)
            {
                Context.SetLineInputModeEnabled(true, LineInputLabelMode.Distance);
                _distanceInputEnabled = true;
            }

            UpdateDistancePreviewDisplay();
            return;
        }

        DisableDistanceInput();
    }

    private void UpdateDistancePreviewDisplay()
    {
        if (Context is null || !_hasBasePoint || _plan is null)
        {
            return;
        }

        var projected = StretchOperations.ProjectDeltaOntoNormal(GetPreviewDelta(), _plan.Normal);
        Context.SetLength(Math.Sqrt(projected.X * projected.X + projected.Y * projected.Y));
    }

    private void DisableDistanceInput()
    {
        if (!_distanceInputEnabled)
        {
            return;
        }

        Context?.SetLineInputModeEnabled(false, LineInputLabelMode.Length);
        _distanceInputEnabled = false;
    }

    private static void DrawConnectedSideEdges(
        DrawingContext context,
        Pen pen,
        CadDocument document,
        Guid endpointVertexId,
        Point movedEndpoint,
        Guid stretchEdgeId)
    {
        foreach (var edge in document.Edges)
        {
            if (edge.Id == stretchEdgeId
                || (edge.StartVertexId != endpointVertexId && edge.EndVertexId != endpointVertexId))
            {
                continue;
            }

            var fixedVertexId = edge.StartVertexId == endpointVertexId
                ? edge.EndVertexId
                : edge.StartVertexId;
            var fixedPoint = TopologyService.GetVertexPosition(document, fixedVertexId);

            context.DrawLine(
                pen,
                movedEndpoint,
                new Point(fixedPoint.X, fixedPoint.Y));
        }
    }

    private static void DrawEdgeHighlight(
        DrawingContext context,
        CadDocument document,
        Guid edgeId,
        double zoom,
        Color color,
        double thickness)
    {
        var edge = document.Edges.FirstOrDefault(item => item.Id == edgeId);
        if (edge is null)
        {
            return;
        }

        var start = TopologyService.GetEdgeStartPoint(document, edge);
        var end = TopologyService.GetEdgeEndPoint(document, edge);
        var pen = RenderStyles.CreateScreenPen(new SolidColorBrush(color), thickness, zoom, [3, 2]);
        context.DrawLine(
            pen,
            new Point(start.X, start.Y),
            new Point(end.X, end.Y));
    }

    private void HandleRightClick()
    {
        if (_hasBasePoint)
        {
            CancelActiveOperation(null);
        }

        Context?.Session.Selection.Clear();
        _plan = null;
        _hoveredEdgeId = null;
        DisableDistanceInput();
        Context?.SetStatus(Strings.Status_SelectionCleared);
        Context?.RequestRedraw();
    }

    private void CancelActiveOperation(string? status)
    {
        _hasBasePoint = false;
        _previewPoint = PointF.Zero;
        _visibleSnaps.Clear();
        DisableDistanceInput();
        if (!string.IsNullOrEmpty(status))
        {
            Context?.SetStatus(status);
        }

        Context?.RequestRedraw();
    }

    private void ResetOperation()
    {
        _hasBasePoint = false;
        _plan = null;
        _hoveredEdgeId = null;
        _visibleSnaps.Clear();
        _distanceInputEnabled = false;
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

    private PointF GetPreviewDelta()
        => new(_previewPoint.X - _basePoint.X, _previewPoint.Y - _basePoint.Y);

    private static string GetIdleStatus()
        => Strings.Input_Stretch_Idle;
}
