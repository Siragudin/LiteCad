using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class RectangleTool : ToolBase
{
    private readonly List<SnapPoint> _visibleSnaps = [];
    private bool _hasFirstCorner;
    private PointF _firstCorner;
    private PointF _oppositeCorner;

    public override ToolId Id => ToolId.Rectangle;

    public override void OnDeactivated()
    {
        Context?.SetRectangleSizeInputEnabled(false);
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
            Cancel(Strings.Status_RectangleCancelled);
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var snapped = ResolveSnap(world);

        if (!_hasFirstCorner)
        {
            _hasFirstCorner = true;
            _firstCorner = snapped;
            _oppositeCorner = snapped;
            Context.SetRectangleSizeInputEnabled(true);
            Context.SetStatus(Strings.Input_Rectangle_SelectOppositeCorner);
            Context.ResetRectangleSizeInput(null, null);
            Context.SetArea(null);
            Context.RequestRedraw();
            e.Handled = true;
            return;
        }

        CommitRectangle(ResolveSnap(world));
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

        if (_hasFirstCorner)
        {
            _oppositeCorner = ResolveSnap(world);
            UpdateSizeDisplay();
        }
        else
        {
            Context.SetRectangleSizePreview(null, null);
            Context.SetArea(null);
        }

        Context.RequestRedraw();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        if (Context is null)
        {
            return;
        }

        if (e.Key == Key.Escape && _hasFirstCorner)
        {
            Cancel(Strings.Status_RectangleCancelled);
            e.Handled = true;
            return;
        }

        if (_hasFirstCorner && Context.ProcessRectangleSizeKey(e))
        {
            e.Handled = true;
        }
    }

    public override bool TryApplyRectangleSize(string width, string height)
    {
        if (!_hasFirstCorner || Context is null)
        {
            return false;
        }

        if (!RectangleSizeInputParser.TryParseSingle(width, out var parsedWidth)
            || !RectangleSizeInputParser.TryParseSingle(height, out var parsedHeight))
        {
            return false;
        }

        var opposite = ComputeOppositeFromSize(_firstCorner, _oppositeCorner, parsedWidth, parsedHeight);
        CommitRectangle(opposite);
        return true;
    }

    public override bool TryApplyLengthInput(string input)
    {
        if (!_hasFirstCorner || Context is null)
        {
            return false;
        }

        if (!RectangleSizeInputParser.TryParse(input, out var width, out var height))
        {
            return false;
        }

        var opposite = ComputeOppositeFromSize(_firstCorner, _oppositeCorner, width, height);
        CommitRectangle(opposite);
        return true;
    }

    public override bool TryApplyLength(double length) => false;

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

        if (!_hasFirstCorner)
        {
            return;
        }

        var corners = GetRectangleCorners(_firstCorner, _oppositeCorner);
        var options = Context.Session.LineToolOptions;
        var previewPen = PreviewLineRenderer.CreatePen(
            zoom,
            options.Color,
            options.Thickness,
            GetDashArray(options.LineType));
        context.DrawLine(previewPen, ToPoint(corners.V1), ToPoint(corners.V2));
        context.DrawLine(previewPen, ToPoint(corners.V2), ToPoint(corners.V3));
        context.DrawLine(previewPen, ToPoint(corners.V3), ToPoint(corners.V4));
        context.DrawLine(previewPen, ToPoint(corners.V4), ToPoint(corners.V1));
    }

    private void CommitRectangle(PointF oppositeCorner)
    {
        if (Context is null || !_hasFirstCorner)
        {
            return;
        }

        var corners = GetRectangleCorners(_firstCorner, oppositeCorner);
        var width = Math.Abs(oppositeCorner.X - _firstCorner.X);
        var height = Math.Abs(oppositeCorner.Y - _firstCorner.Y);
        var topologyTolerance = TopologyTolerance.ForMutation;

        if (width <= topologyTolerance || height <= topologyTolerance)
        {
            Context.SetStatus(Strings.Error_RectangleTooSmall);
            return;
        }

        Context.RecordUndo();

        var template = CreateTemplate();
        var document = Context.Session.Document;
        EdgeOperations.AddSegment(document, corners.V1, corners.V2, template, topologyTolerance);
        EdgeOperations.AddSegment(document, corners.V2, corners.V3, template, topologyTolerance);
        EdgeOperations.AddSegment(document, corners.V3, corners.V4, template, topologyTolerance);
        EdgeOperations.AddSegment(document, corners.V4, corners.V1, template, topologyTolerance);
        PolygonBuilder.SyncFaces(document, topologyTolerance);

        ResetAfterCommit();
        Context.SetStatus(Strings.Input_Rectangle_SelectFirstCorner);
        Context.RequestRedraw();
    }

    private void ResetAfterCommit()
    {
        _hasFirstCorner = false;
        _visibleSnaps.Clear();
        Context?.SetRectangleSizeInputEnabled(false);
        Context?.SetArea(null);
        Context?.ResetRectangleSizeInput(null, null);
    }

    private void Cancel(string status)
    {
        ResetState();
        Context?.SetStatus(status);
        Context?.RequestRedraw();
    }

    private void ResetState()
    {
        _hasFirstCorner = false;
        _visibleSnaps.Clear();
        Context?.SetRectangleSizeInputEnabled(false);
        Context?.SetArea(null);
        Context?.ResetRectangleSizeInput(null, null);
    }

    private void UpdateSizeDisplay()
    {
        if (Context is null)
        {
            return;
        }

        var width = Math.Abs(_oppositeCorner.X - _firstCorner.X);
        var height = Math.Abs(_oppositeCorner.Y - _firstCorner.Y);
        Context.SetRectangleSizePreview(width, height);
        Context.SetArea(width * height);
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
            includeOnEdge: !_hasFirstCorner);
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

    internal static RectangleCorners GetRectangleCorners(PointF firstCorner, PointF oppositeCorner)
        => new(
            firstCorner,
            new PointF(oppositeCorner.X, firstCorner.Y),
            oppositeCorner,
            new PointF(firstCorner.X, oppositeCorner.Y));

    internal static PointF ComputeOppositeFromSize(PointF firstCorner, PointF directionHint, double width, double height)
    {
        var signX = directionHint.X >= firstCorner.X ? 1.0 : -1.0;
        var signY = directionHint.Y >= firstCorner.Y ? 1.0 : -1.0;

        if (Math.Abs(directionHint.X - firstCorner.X) <= TopologyTolerance.ForMutation)
        {
            signX = 1.0;
        }

        if (Math.Abs(directionHint.Y - firstCorner.Y) <= TopologyTolerance.ForMutation)
        {
            signY = 1.0;
        }

        return new PointF(firstCorner.X + signX * width, firstCorner.Y + signY * height);
    }

    private static Point ToPoint(PointF point) => new(point.X, point.Y);

    internal readonly record struct RectangleCorners(PointF V1, PointF V2, PointF V3, PointF V4);
}
