using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class OffsetTool : ToolBase
{
    private readonly List<SnapPoint> _visibleSnaps = [];
    private Guid? _hoveredFaceId;
    private Guid? _sourceFaceId;
    private PointF _cursorPoint;
    private double _signedDistance;
    private bool _distanceInputEnabled;

    public override ToolId Id => ToolId.Offset;

    public bool HasActiveOffset => _sourceFaceId is not null;

    public double PreviewSignedDistance => _signedDistance;

    public OffsetValidationResult PreviewValidation => GetPreviewValidation();

    public override void OnActivated()
    {
        ResetPreview();
        _hoveredFaceId = null;
        Context?.SetStatus(GetIdleStatus());
    }

    public override void OnDeactivated()
    {
        DisableDistanceInput();
        ResetPreview();
        _hoveredFaceId = null;
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

        if (_sourceFaceId is null)
        {
            if (!TrySelectFaceAt(world))
            {
                e.Handled = true;
                return;
            }

            _cursorPoint = ResolveSnap(world);
            UpdateSignedDistance();
            SyncDistanceInput();
            Context.SetStatus(Strings.Input_Offset_SetDistance);
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        CommitOffset();
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

        if (_sourceFaceId is null)
        {
            UpdateHoveredFace(world, tolerance);
        }
        else
        {
            _cursorPoint = ResolveSnap(world);
            UpdateSignedDistance();
            UpdateDistancePreviewDisplay();
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

        if (e.Key == Key.Escape && _sourceFaceId is not null)
        {
            CancelOperation(Strings.Status_OffsetCancelled);
            e.Handled = true;
            return;
        }

        if (_sourceFaceId is not null && Context.ProcessLengthKey(e))
        {
            e.Handled = true;
        }
    }

    public override bool TryApplyLength(double length)
    {
        if (Context is null || _sourceFaceId is null || !TryGetSourceFace(out _))
        {
            return false;
        }

        if (length <= 0)
        {
            return false;
        }

        var sign = Math.Sign(_signedDistance);
        if (sign == 0)
        {
            sign = 1;
        }

        return CommitOffsetWithDistance(sign * length);
    }

    public override bool TryApplyLengthInput(string input)
    {
        if (Context is null || _sourceFaceId is null || !TryGetSourceFace(out _))
        {
            return false;
        }

        if (!MoveOffsetInputParser.TryParseDistance(input, out var distance))
        {
            return false;
        }

        var sign = Math.Sign(_signedDistance);
        if (sign == 0)
        {
            sign = 1;
        }

        return CommitOffsetWithDistance(sign * distance);
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
        var document = Context.Session.Document;

        foreach (var snap in _visibleSnaps)
        {
            SnapRenderer.DrawSnapPoint(context, snap, zoom);
        }

        if (_hoveredFaceId is Guid hoveredFaceId
            && (_sourceFaceId is null || _sourceFaceId != hoveredFaceId))
        {
            DrawFaceHighlight(context, document, hoveredFaceId, zoom);
        }

        if (_sourceFaceId is Guid sourceFaceId)
        {
            DrawFaceHighlight(context, document, sourceFaceId, zoom);
        }

        if (_sourceFaceId is null || !TryGetSourceFace(out var face))
        {
            return;
        }

        var preview = OffsetOperations.ComputeFaceOffset(
            document,
            face,
            _signedDistance,
            Context.SnapTolerance);

        if (preview.Validation != OffsetValidationResult.Valid || preview.Points.Count < 3)
        {
            return;
        }

        var pen = RenderStyles.CreateScreenPen(
            new SolidColorBrush(Color.FromArgb(0xB0, 0x21, 0x96, 0xF3)),
            1.5,
            zoom,
            [4, 2]);

        for (var index = 0; index < preview.Points.Count; index++)
        {
            var start = preview.Points[index];
            var end = preview.Points[(index + 1) % preview.Points.Count];
            context.DrawLine(
                pen,
                new Point(start.X, start.Y),
                new Point(end.X, end.Y));
        }
    }

    private bool TrySelectFaceAt(PointF world)
    {
        if (Context is null)
        {
            return false;
        }

        if (!FacePickOperations.TryPickFaceAt(
                Context.Session.Document,
                world,
                Context.SnapTolerance,
                out var face))
        {
            Context.SetStatus(Strings.Input_Offset_SelectFace);
            return false;
        }

        var selection = Context.Session.Selection;
        selection.Clear();
        selection.SelectedPolygonIds.Add(face.Id);
        foreach (var reference in face.OuterLoop.Edges)
        {
            selection.SelectedEdgeIds.Add(reference.EdgeId);
        }

        _sourceFaceId = face.Id;
        _hoveredFaceId = null;
        return true;
    }

    private bool CommitOffset()
        => CommitOffsetWithDistance(_signedDistance);

    private bool CommitOffsetWithDistance(double signedDistance)
    {
        if (Context is null || _sourceFaceId is null || !TryGetSourceFace(out var face))
        {
            return false;
        }

        var validation = OffsetOperations.ComputeFaceOffset(
            Context.Session.Document,
            face,
            signedDistance,
            TopologyTolerance.ForMutation);

        if (validation.Validation != OffsetValidationResult.Valid)
        {
            Context.SetStatus(GetValidationStatus(validation.Validation));
            return false;
        }

        Context.RecordUndo();
        var result = OffsetOperations.ExecuteObjectOffset(
            Context.Session.Document,
            Context.Session.Selection,
            face,
            signedDistance,
            TopologyTolerance.ForMutation);

        if (result is null)
        {
            Context.SetStatus(Strings.Status_OffsetCancelled);
            return false;
        }

        ResetPreview();
        Context.SetStatus(Strings.Status_OffsetCompleted);
        Context.RequestRedraw();
        return true;
    }

    private void HandleRightClick()
    {
        if (_sourceFaceId is not null)
        {
            ResetPreview();
        }

        Context?.Session.Selection.Clear();
        Context?.SetStatus(Strings.Status_SelectionCleared);
        Context?.RequestRedraw();
    }

    private void CancelOperation(string status)
    {
        ResetPreview();
        Context?.SetStatus(status);
        Context?.RequestRedraw();
    }

    private void ResetPreview()
    {
        _sourceFaceId = null;
        _signedDistance = 0;
        DisableDistanceInput();
    }

    private void UpdateHoveredFace(PointF world, double tolerance)
    {
        if (Context is null)
        {
            _hoveredFaceId = null;
            return;
        }

        _hoveredFaceId = FacePickOperations.TryPickFaceAt(
            Context.Session.Document,
            world,
            tolerance,
            out var face)
            ? face.Id
            : null;
    }

    private void UpdateSignedDistance()
    {
        if (Context is null || !TryGetSourceFace(out var face))
        {
            _signedDistance = 0;
            return;
        }

        _signedDistance = OffsetOperations.ComputeSignedDistance(
            Context.Session.Document,
            face,
            _cursorPoint,
            Context.SnapTolerance);
    }

    private OffsetValidationResult GetPreviewValidation()
    {
        if (Context is null || !TryGetSourceFace(out var face))
        {
            return OffsetValidationResult.UnsupportedFace;
        }

        return OffsetOperations.ComputeFaceOffset(
            Context.Session.Document,
            face,
            _signedDistance,
            Context.SnapTolerance).Validation;
    }

    private bool TryGetSourceFace(out Polygon face)
    {
        face = null!;
        if (Context is null || _sourceFaceId is not Guid sourceFaceId)
        {
            return false;
        }

        face = Context.Session.Document.Polygons.FirstOrDefault(polygon => polygon.Id == sourceFaceId)
            ?? null!;

        return face is not null && OffsetOperations.CanOffsetFace(face);
    }

    private void SyncDistanceInput()
    {
        if (Context is null || _sourceFaceId is null)
        {
            DisableDistanceInput();
            return;
        }

        if (!_distanceInputEnabled)
        {
            Context.SetLineInputModeEnabled(true, LineInputLabelMode.Distance);
            _distanceInputEnabled = true;
        }

        UpdateDistancePreviewDisplay();
    }

    private void UpdateDistancePreviewDisplay()
    {
        if (Context is null || _sourceFaceId is null)
        {
            return;
        }

        Context.SetLength(Math.Abs(_signedDistance));
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

    private static void DrawFaceHighlight(
        DrawingContext context,
        CadDocument document,
        Guid faceId,
        double zoom)
    {
        var polygon = document.Polygons.FirstOrDefault(item => item.Id == faceId);
        if (polygon is null)
        {
            return;
        }

        var points = PolygonGeometry.GetOuterBoundaryPoints(document, polygon);
        if (points.Count < 3)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(points[0].X, points[0].Y), isFilled: true, isClosed: true);
            for (var index = 1; index < points.Count; index++)
            {
                ctx.LineTo(new Point(points[index].X, points[index].Y), isStroked: true, isSmoothJoin: false);
            }
        }

        geometry.Freeze();

        var fill = new SolidColorBrush(Color.FromArgb(0x40, 0x21, 0x96, 0xF3));
        fill.Freeze();
        var stroke = RenderStyles.CreateScreenPen(
            new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
            1.5,
            zoom,
            [4, 2]);

        context.DrawGeometry(fill, stroke, geometry);
    }

    private static string GetIdleStatus()
        => Strings.Input_Offset_Idle;

    private static string GetValidationStatus(OffsetValidationResult validation)
        => validation switch
        {
            OffsetValidationResult.ZeroDistance => Strings.Error_OffsetZeroDistance,
            OffsetValidationResult.Degenerate => Strings.Error_OffsetDegenerate,
            OffsetValidationResult.SelfIntersection => Strings.Error_OffsetSelfIntersection,
            OffsetValidationResult.UnsupportedFace => Strings.Error_OffsetUnsupportedFace,
            _ => Strings.Status_OffsetCancelled
        };
}
