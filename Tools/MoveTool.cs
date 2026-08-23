using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using LiteCad.Texts;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class MoveTool : ToolBase
{
    private readonly List<SnapPoint> _visibleSnaps = [];
    private bool _hasBasePoint;
    private PointF _basePoint;
    private PointF _previewPoint;
    private MoveObjectSnapshot? _objectSnapshot;
    private Dictionary<Guid, PointF>? _vertexStartPositions;
    private MoveMode _mode;
    private Guid? _hoveredVertexId;
    private bool _lastOrthoEnabled;
    private bool _exactInputUiConfigured;
    private PointF? _exactDelta;
    private string _savedOffsetX = string.Empty;
    private string _savedOffsetY = string.Empty;
    private string _savedDistance = string.Empty;

    public override ToolId Id => ToolId.Move;

    public bool ShowsVertexHandles => Context is not null;

    public bool IsInVertexMoveMode => _mode == MoveMode.Vertex;

    public bool HasActiveMove => _hasBasePoint;

    public PointF PreviewDelta => GetPreviewDelta();

    public void NotifyOrthoChanged()
    {
        if (Context is null || !_hasBasePoint)
        {
            return;
        }

        _exactInputUiConfigured = false;
        SyncExactInputUi();
        _previewPoint = ResolveSnapWithOrtho(_previewPoint);
        UpdateExactInputPreviewDisplay();
        Context.RequestRedraw();
    }

    public override void OnActivated()
    {
        ResetOperation();
        Context?.SetStatus(GetIdleStatus());
    }

    public override void OnDeactivated()
    {
        DisableExactInput();
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

        var selection = Context.Session.Selection;
        var document = Context.Session.Document;
        var zoom = Context.Session.Camera.Zoom;

        if (!_hasBasePoint)
        {
            if (VertexHandleRenderer.TryPickVertex(document, world, zoom, Context.SnapTolerance, out var vertexId))
            {
                var reclickSelectedVertex =
                    selection.SelectedVertexIds.Count == 1
                    && selection.SelectedVertexIds.Contains(vertexId)
                    && selection.SelectedEdgeIds.Count == 0
                    && selection.SelectedPolygonIds.Count == 0;

                if (!reclickSelectedVertex)
                {
                    selection.Clear();
                    selection.SelectedVertexIds.Add(vertexId);
                    _mode = MoveMode.Vertex;
                    Context.SetStatus(Strings.Input_Move_VertexSelected);
                    Context.RequestRedraw();
                    e.Handled = true;
                    return;
                }
            }

            if (!MoveOperations.CanMove(selection))
            {
                Context.SetStatus(Strings.Input_Move_SelectVertices);
                e.Handled = true;
                return;
            }

            _mode = MoveOperations.UsesVertexMove(selection) ? MoveMode.Vertex : MoveMode.Objects;
            _basePoint = ResolveSnap(world);
            _previewPoint = _basePoint;

            if (_mode == MoveMode.Objects)
            {
                _objectSnapshot = MoveOperations.CreateSnapshot(document, selection);
            }
            else
            {
                _vertexStartPositions = selection.SelectedVertexIds
                    .ToDictionary(
                        id => id,
                        id => TopologyService.GetVertex(document, id).Position);
            }

            _hasBasePoint = true;
            _lastOrthoEnabled = IsOrthoEnabled();
            _exactInputUiConfigured = false;
            SyncExactInputUi();
            Context.SetStatus(Strings.Input_Move_SelectDestination);
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        if (_exactDelta is null)
        {
            _previewPoint = ResolveSnapWithOrtho(world);
        }

        CommitMove();
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
        var zoom = Context.Session.Camera.Zoom;

        _visibleSnaps.Clear();
        foreach (var snap in Context.Session.SnapService.GetVisibleSnaps(document, world, tolerance))
        {
            _visibleSnaps.Add(snap);
        }

        if (!_hasBasePoint)
        {
            _hoveredVertexId = VertexHandleRenderer.TryPickVertex(document, world, zoom, tolerance, out var hoveredId)
                ? hoveredId
                : null;
        }

        if (_hasBasePoint)
        {
            SyncExactInputUi();
            _previewPoint = ResolveSnapWithOrtho(world);
            _exactDelta = null;
            UpdateExactInputPreviewDisplay();
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

        if (e.Key == Key.Escape)
        {
            CancelOperation(Strings.Status_MoveCancelled);
            e.Handled = true;
            return;
        }

        if (!_hasBasePoint)
        {
            return;
        }

        if (IsOrthoEnabled())
        {
            if (Context.ProcessLengthKey(e))
            {
                e.Handled = true;
            }

            return;
        }

        if (Context.ProcessRectangleSizeKey(e))
        {
            e.Handled = true;
        }
    }

    public override bool TryApplyLength(double length)
    {
        if (!_hasBasePoint || Context is null || !IsOrthoEnabled())
        {
            return false;
        }

        if (!TryComputeOrthoDeltaFromDistance(length, out var delta))
        {
            Context.SetStatus(Strings.Input_SetDirectionThenTypeDistance);
            return false;
        }

        return TryCommitExactInput(delta);
    }

    public override bool TryApplyLengthInput(string input)
    {
        if (!_hasBasePoint || Context is null || !IsOrthoEnabled())
        {
            return false;
        }

        if (!MoveOffsetInputParser.TryParseDistance(input, out var distance))
        {
            return false;
        }

        if (!TryComputeOrthoDeltaFromDistance(distance, out var delta))
        {
            Context.SetStatus(Strings.Input_SetDirectionThenTypeDistance);
            return false;
        }

        _savedDistance = input.Trim();
        return TryCommitExactInput(delta);
    }

    public override bool TryApplyRectangleSize(string width, string height)
    {
        if (!_hasBasePoint || Context is null || IsOrthoEnabled())
        {
            return false;
        }

        if (!MoveOffsetInputParser.TryParseSigned(width, out var deltaX)
            || !MoveOffsetInputParser.TryParseSigned(height, out var deltaY))
        {
            return false;
        }

        _savedOffsetX = width.Trim();
        _savedOffsetY = height.Trim();
        return TryCommitExactInput(new PointF(deltaX, deltaY));
    }

    public override void RenderOverlay(DrawingContext context, Camera camera, Size viewport)
    {
        if (Context is null)
        {
            return;
        }

        var document = Context.Session.Document;
        var zoom = camera.Zoom;
        var selection = Context.Session.Selection;

        VertexHandleRenderer.DrawHandles(
            context,
            document,
            zoom,
            _hoveredVertexId,
            selection.SelectedVertexIds);

        foreach (var snap in _visibleSnaps)
        {
            SnapRenderer.DrawSnapPoint(context, snap, zoom);
        }

        if (!_hasBasePoint)
        {
            return;
        }

        var delta = GetPreviewDelta();
        var pen = PreviewLineRenderer.CreateGhostPen(zoom, 1.5, [4, 2]);

        if (_mode == MoveMode.Vertex && _vertexStartPositions is not null)
        {
            foreach (var (vertexId, start) in _vertexStartPositions)
            {
                var end = Translate(start, delta);
                foreach (var edge in document.Edges)
                {
                    if (edge.StartVertexId != vertexId && edge.EndVertexId != vertexId)
                    {
                        continue;
                    }

                    var edgeStart = edge.StartVertexId == vertexId
                        ? end
                        : TopologyService.GetVertexPosition(document, edge.StartVertexId);
                    var edgeEnd = edge.EndVertexId == vertexId
                        ? end
                        : TopologyService.GetVertexPosition(document, edge.EndVertexId);

                    context.DrawLine(
                        pen,
                        new Point(edgeStart.X, edgeStart.Y),
                        new Point(edgeEnd.X, edgeEnd.Y));
                }
            }

            return;
        }

        if (_objectSnapshot is null)
        {
            return;
        }

        foreach (var entry in _objectSnapshot.Edges)
        {
            var start = Translate(entry.Start, delta);
            var end = Translate(entry.End, delta);
            context.DrawLine(
                pen,
                new Point(start.X, start.Y),
                new Point(end.X, end.Y));
        }

        var previewColor = PreviewLineRenderer.GetAnnotationPreviewColor();
        foreach (var entry in _objectSnapshot.Texts)
        {
            var origin = Translate(entry.Origin, delta);
            PointF? arrowTip = entry.ArrowTip is PointF tip ? Translate(tip, delta) : null;
            TextAnnotationDrawing.DrawDraft(
                context,
                entry.Kind,
                origin,
                arrowTip,
                entry.Text,
                entry.TextSize,
                zoom,
                previewColor,
                camera,
                viewport,
                showCaret: false);
        }
    }

    private bool TryCommitExactInput(PointF delta)
    {
        if (!_hasBasePoint || Context is null)
        {
            return false;
        }

        if (Math.Abs(delta.X) < 1e-9 && Math.Abs(delta.Y) < 1e-9)
        {
            return false;
        }

        _exactDelta = delta;
        _previewPoint = Translate(_basePoint, delta);
        return CommitMove();
    }

    private bool CommitMove()
    {
        if (Context is null || !_hasBasePoint)
        {
            return false;
        }

        var delta = GetPreviewDelta();
        if (Math.Abs(delta.X) < 1e-9 && Math.Abs(delta.Y) < 1e-9)
        {
            ResetOperation();
            Context.SetStatus(Strings.Status_MoveCancelled);
            Context.RequestRedraw();
            return false;
        }

        Context.RecordUndo();
        var document = Context.Session.Document;
        var selection = Context.Session.Selection;

        if (_mode == MoveMode.Vertex && _vertexStartPositions is not null)
        {
            MoveOperations.MoveVertices(document, selection.SelectedVertexIds, delta);
        }
        else if (_objectSnapshot is not null)
        {
            MoveOperations.ExecuteObjectMove(document, selection, _objectSnapshot, delta);
        }

        ClearSavedInputState();
        DisableExactInput();
        ResetOperation();
        Context.SetStatus(Strings.Status_MoveCompleted);
        Context.RequestRedraw();
        return true;
    }

    private void HandleRightClick()
    {
        if (_hasBasePoint)
        {
            DisableExactInput();
            ResetOperation();
        }

        Context?.Session.Selection.Clear();
        Context?.SetStatus(Strings.Status_SelectionCleared);
        Context?.RequestRedraw();
    }

    private void CancelOperation(string status)
    {
        DisableExactInput();
        ResetOperation();
        Context?.SetStatus(status);
        Context?.RequestRedraw();
    }

    private void ResetOperation()
    {
        _hasBasePoint = false;
        _objectSnapshot = null;
        _vertexStartPositions = null;
        _visibleSnaps.Clear();
        _hoveredVertexId = null;
        _mode = MoveMode.None;
        _exactDelta = null;
        _lastOrthoEnabled = IsOrthoEnabled();
        _exactInputUiConfigured = false;
    }

    private void ClearSavedInputState()
    {
        _savedOffsetX = string.Empty;
        _savedOffsetY = string.Empty;
        _savedDistance = string.Empty;
    }

    private void DisableExactInput()
    {
        Context?.SetDualFieldInputEnabled(false, DualFieldLabelMode.MoveOffset);
        Context?.SetLineInputModeEnabled(false, LineInputLabelMode.Length);
    }

    private void SyncExactInputUi()
    {
        if (Context is null || !_hasBasePoint)
        {
            return;
        }

        var orthoEnabled = IsOrthoEnabled();
        if (orthoEnabled == _lastOrthoEnabled && _exactInputUiConfigured)
        {
            return;
        }

        CaptureCurrentInputTexts();

        if (orthoEnabled)
        {
            Context.SetDualFieldInputEnabled(false, DualFieldLabelMode.MoveOffset);
            Context.SetLineInputModeEnabled(true, LineInputLabelMode.Distance);
            Context.ResetLengthInput(null);
            if (!string.IsNullOrEmpty(_savedDistance))
            {
                Context.SetLineInputText(_savedDistance);
            }
        }
        else
        {
            Context.SetLineInputModeEnabled(false, LineInputLabelMode.Length);
            Context.SetDualFieldInputEnabled(true, DualFieldLabelMode.MoveOffset);
            Context.ResetRectangleSizeInput(null, null);
            if (!string.IsNullOrEmpty(_savedOffsetX) || !string.IsNullOrEmpty(_savedOffsetY))
            {
                Context.SetDualFieldInputText(_savedOffsetX, _savedOffsetY);
            }
        }

        _lastOrthoEnabled = orthoEnabled;
        _exactInputUiConfigured = true;
        _exactDelta = null;
    }

    private void CaptureCurrentInputTexts()
    {
        if (Context is null)
        {
            return;
        }

        if (_lastOrthoEnabled)
        {
            var distance = Context.GetLineInputText();
            if (!string.IsNullOrWhiteSpace(distance))
            {
                _savedDistance = distance;
            }

            return;
        }

        var (x, y) = Context.GetDualFieldInputText();
        if (!string.IsNullOrWhiteSpace(x))
        {
            _savedOffsetX = x;
        }

        if (!string.IsNullOrWhiteSpace(y))
        {
            _savedOffsetY = y;
        }
    }

    private void UpdateExactInputPreviewDisplay()
    {
        if (Context is null || !_hasBasePoint)
        {
            return;
        }

        var delta = GetPreviewDelta();
        if (IsOrthoEnabled())
        {
            Context.SetLength(Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y));
            return;
        }

        Context.SetRectangleSizePreview(delta.X, delta.Y);
    }

    private bool TryComputeOrthoDeltaFromDistance(double distance, out PointF delta)
    {
        delta = default;
        if (Context is null)
        {
            return false;
        }

        var magnitude = Math.Abs(distance);
        if (magnitude <= 0)
        {
            return false;
        }

        var direction = GetPreviewDelta();
        var directionLength = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        if (directionLength <= Context.SnapTolerance)
        {
            return false;
        }

        var scale = magnitude / directionLength;
        delta = new PointF(direction.X * scale, direction.Y * scale);
        return true;
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
        if (IsOrthoEnabled() && _hasBasePoint)
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

    private static string GetIdleStatus()
        => Strings.Input_Move_Idle;

    internal enum MoveMode
    {
        None,
        Vertex,
        Objects
    }
}
