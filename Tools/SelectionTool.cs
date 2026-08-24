using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Leaders;
using LiteCad.Texts;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using LiteCad.UI;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public sealed class SelectionTool : ToolBase
{
    private const double MarqueeThresholdPixels = 4.0;

    private bool _isSelecting;
    private PointF _selectStartWorld;
    private PointF _selectCurrentWorld;

    public override ToolId Id => ToolId.Selection;

    public override void OnDeactivated()
    {
        _isSelecting = false;
    }

    public override void OnMouseDown(MouseButtonEventArgs e, PointF world)
    {
        if (Context is null || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _isSelecting = true;
        _selectStartWorld = world;
        _selectCurrentWorld = world;
        e.Handled = true;
    }

    public override void OnMouseMove(MouseEventArgs e, PointF world)
    {
        if (Context is null || !_isSelecting || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _selectCurrentWorld = world;
        RequestOverlayRedraw();
        e.Handled = true;
    }

    public override void OnMouseUp(MouseButtonEventArgs e, PointF world)
    {
        if (Context is null || e.ChangedButton != MouseButton.Left || !_isSelecting)
        {
            return;
        }

        _isSelecting = false;
        _selectCurrentWorld = world;

        var additive = IsShiftPressed();
        if (IsMarqueeDrag())
        {
            ApplyMarqueeSelection(additive);
        }
        else
        {
            ApplyClickSelection(world, additive);
        }

        Context.RequestRedraw();
        e.Handled = true;
    }

    public override void RenderOverlay(DrawingContext context, Camera camera, Size viewport)
    {
        if (Context is null || !_isSelecting || !IsMarqueeDrag())
        {
            return;
        }

        var rect = SelectionBounds.FromPoints(_selectStartWorld, _selectCurrentWorld);
        var isWindow = IsWindowSelection(camera, viewport);
        var fill = new SolidColorBrush(isWindow
            ? CanvasTheme.MarqueeWindowFill
            : CanvasTheme.MarqueeCrossFill);
        fill.Freeze();

        var stroke = RenderStyles.CreateScreenPen(
            CanvasTheme.CreateFrozenBrush(isWindow ? CanvasTheme.MarqueeWindowStroke : CanvasTheme.MarqueeCrossStroke),
            1.0,
            camera.Zoom,
            [4, 2]);

        context.DrawRectangle(
            fill,
            stroke,
            new Rect(rect.MinX, rect.MinY, rect.Width, rect.Height));
    }

    private void ApplyClickSelection(PointF world, bool additive)
    {
        if (Context is null)
        {
            return;
        }

        var document = Context.Session.Document;
        var selection = Context.Session.Selection;
        var tolerance = Context.SnapTolerance;
        var segmentPickTolerance = Context.SelectionPickTolerance;

        if (!additive)
        {
            selection.Clear();
        }

        if (TryToggleDimensionAt(document, selection, world, tolerance, additive))
        {
            UpdateSelectionUi();
            return;
        }

        if (TryToggleLeaderAt(document, selection, world, segmentPickTolerance, additive))
        {
            UpdateSelectionUi();
            return;
        }

        if (TryToggleTextAt(document, selection, world, segmentPickTolerance, additive))
        {
            UpdateSelectionUi();
            return;
        }

        if (TryToggleAxisAt(document, selection, world, segmentPickTolerance, additive))
        {
            UpdateSelectionUi();
            return;
        }

        if (TryToggleEdgeAt(document, selection, world, segmentPickTolerance, additive))
        {
            UpdateSelectionUi();
            return;
        }

        if (TryTogglePolygonAt(document, selection, world, tolerance, additive))
        {
            UpdateSelectionUi();
            return;
        }

        if (!additive)
        {
            selection.Clear();
            Context.SetArea(null);
            Context.SetLength(null);
            Context.SetSelectionInfo(Strings.Selection_NothingSelected);
            Context.SetStatus(Strings.Status_SelectionCleared);
            return;
        }

        UpdateSelectionUi();
    }

    private void ApplyMarqueeSelection(bool additive)
    {
        if (Context is null)
        {
            return;
        }

        var document = Context.Session.Document;
        var selection = Context.Session.Selection;
        var viewport = Context.GetViewportSize();
        var camera = Context.Session.Camera;
        var bounds = SelectionBounds.FromPoints(_selectStartWorld, _selectCurrentWorld);
        var windowSelection = IsWindowSelection(camera, viewport);

        if (!additive)
        {
            selection.Clear();
        }

        foreach (var edge in document.Edges)
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            if (windowSelection
                    ? bounds.ContainsSegmentFully(start, end)
                    : bounds.IntersectsSegment(start, end))
            {
                selection.SelectedEdgeIds.Add(edge.Id);
            }
        }

        foreach (var axis in document.Axes)
        {
            if (windowSelection
                    ? bounds.ContainsSegmentFully(axis.Start, axis.End)
                    : bounds.IntersectsSegment(axis.Start, axis.End))
            {
                selection.SelectedAxisIds.Add(axis.Id);
            }
        }

        foreach (var leader in document.Leaders)
        {
            var layout = LeaderGeometry.CreateLayout(
                leader.Target,
                leader.TextPosition,
                Context?.Session.Camera.Zoom ?? 1.0);
            var hit = false;
            foreach (var (start, end) in LeaderGeometry.GetSegments(layout))
            {
                hit = windowSelection
                    ? bounds.ContainsSegmentFully(start, end)
                    : bounds.IntersectsSegment(start, end);
                if (hit)
                {
                    break;
                }
            }

            if (!hit)
            {
                hit = bounds.Contains(layout.TextPosition);
            }

            if (hit)
            {
                selection.SelectedLeaderIds.Add(leader.Id);
            }
        }

        foreach (var note in document.Texts)
        {
            var layout = TextGeometry.CreateLayout(note, Context?.Session.Camera.Zoom ?? 1.0);
            var hit = bounds.Contains(layout.Origin) || bounds.Contains(layout.UnderlineEnd);
            if (!hit && layout.ArrowTip is PointF tip)
            {
                hit = windowSelection
                    ? bounds.ContainsSegmentFully(tip, layout.Origin)
                    : bounds.IntersectsSegment(tip, layout.Origin);
            }

            if (hit)
            {
                selection.SelectedTextIds.Add(note.Id);
            }
        }

        foreach (var polygon in document.Polygons)
        {
            if (polygon.OuterLoop.Edges.Count < 3)
            {
                continue;
            }

            if (windowSelection
                    ? bounds.ContainsPolygonFully(document, polygon)
                    : bounds.IntersectsPolygon(document, polygon))
            {
                selection.SelectedPolygonIds.Add(polygon.Id);
            }
        }

        UpdateSelectionUi(windowSelection ? Strings.Status_WindowSelection : Strings.Status_CrossingSelection);
    }

    private bool TryToggleAxisAt(
        CadDocument document,
        Core.Selection.Selection selection,
        PointF world,
        double tolerance,
        bool additive)
    {
        Axis? closestAxis = null;
        var closestDistance = tolerance;

        foreach (var axis in document.Axes)
        {
            if (!Geometry2D.TryHitTestSegment(world, axis.Start, axis.End, tolerance, out var distance) ||
                distance > closestDistance)
            {
                continue;
            }

            closestAxis = axis;
            closestDistance = distance;
        }

        if (closestAxis is null)
        {
            return false;
        }

        if (additive && selection.SelectedAxisIds.Contains(closestAxis.Id))
        {
            selection.SelectedAxisIds.Remove(closestAxis.Id);
        }
        else
        {
            selection.SelectedAxisIds.Add(closestAxis.Id);
        }

        Context!.SetStatus(additive ? Strings.Status_SelectionUpdated : Strings.Status_AxisSelected);
        return true;
    }

    private bool TryToggleEdgeAt(
        CadDocument document,
        Core.Selection.Selection selection,
        PointF world,
        double tolerance,
        bool additive)
    {
        Edge? closestEdge = null;
        var closestDistance = tolerance;

        foreach (var edge in document.Edges)
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            if (!Geometry2D.TryHitTestSegment(world, start, end, tolerance, out var distance) ||
                distance > closestDistance)
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

        if (additive && selection.SelectedEdgeIds.Contains(closestEdge.Id))
        {
            selection.SelectedEdgeIds.Remove(closestEdge.Id);
        }
        else
        {
            selection.SelectedEdgeIds.Add(closestEdge.Id);
        }

        Context!.SetStatus(additive ? Strings.Status_SelectionUpdated : Strings.Status_EdgeSelected);
        return true;
    }

    private bool TryTogglePolygonAt(
        CadDocument document,
        Core.Selection.Selection selection,
        PointF world,
        double tolerance,
        bool additive)
    {
        Polygon? closestPolygon = null;
        var closestArea = double.MaxValue;

        foreach (var polygon in document.Polygons)
        {
            if (polygon.OuterLoop.Edges.Count < 3 ||
                !PolygonGeometry.ContainsPoint(document, polygon, world, tolerance))
            {
                continue;
            }

            var area = PolygonGeometry.GetArea(document, polygon, tolerance);
            if (area >= closestArea)
            {
                continue;
            }

            closestPolygon = polygon;
            closestArea = area;
        }

        if (closestPolygon is null)
        {
            return false;
        }

        if (additive && selection.SelectedPolygonIds.Contains(closestPolygon.Id))
        {
            selection.SelectedPolygonIds.Remove(closestPolygon.Id);
        }
        else
        {
            selection.SelectedPolygonIds.Add(closestPolygon.Id);
        }

        Context!.SetStatus(additive ? Strings.Status_SelectionUpdated : Strings.Status_PolygonSelected);
        return true;
    }

    private bool TryToggleDimensionAt(
        CadDocument document,
        Core.Selection.Selection selection,
        PointF world,
        double tolerance,
        bool additive)
    {
        if (!DimensionPickOperations.TryPickAt(document, world, tolerance, out var dimensionId))
        {
            return false;
        }

        if (additive && selection.SelectedDimensionIds.Contains(dimensionId))
        {
            selection.SelectedDimensionIds.Remove(dimensionId);
        }
        else
        {
            selection.SelectedDimensionIds.Add(dimensionId);
        }

        Context!.SetStatus(additive ? Strings.Status_SelectionUpdated : Strings.Status_DimensionSelected);
        return true;
    }

    private bool TryToggleLeaderAt(
        CadDocument document,
        Core.Selection.Selection selection,
        PointF world,
        double tolerance,
        bool additive)
    {
        if (!LeaderPickOperations.TryPickAt(
            document,
            world,
            tolerance,
            out var leaderId,
            Context?.Session.Camera.Zoom ?? 1.0))
        {
            return false;
        }

        if (additive && selection.SelectedLeaderIds.Contains(leaderId))
        {
            selection.SelectedLeaderIds.Remove(leaderId);
        }
        else
        {
            selection.SelectedLeaderIds.Add(leaderId);
        }

        Context!.SetStatus(additive ? Strings.Status_SelectionUpdated : Strings.Status_LeaderSelected);
        return true;
    }

    private bool TryToggleTextAt(
        CadDocument document,
        Core.Selection.Selection selection,
        PointF world,
        double tolerance,
        bool additive)
    {
        if (!TextPickOperations.TryPickAt(
            document,
            world,
            tolerance,
            out var textId,
            Context?.Session.Camera.Zoom ?? 1.0))
        {
            return false;
        }

        if (additive && selection.SelectedTextIds.Contains(textId))
        {
            selection.SelectedTextIds.Remove(textId);
        }
        else
        {
            selection.SelectedTextIds.Add(textId);
        }

        Context!.SetStatus(additive ? Strings.Status_SelectionUpdated : Strings.Status_TextSelected);
        return true;
    }

    private void UpdateSelectionUi(string? status = null)
    {
        if (Context is null)
        {
            return;
        }

        var document = Context.Session.Document;
        var selection = Context.Session.Selection;
        var edgeCount = selection.SelectedEdgeIds.Count;
        var polygonCount = selection.SelectedPolygonIds.Count;
        var vertexCount = selection.SelectedVertexIds.Count;
        var dimensionCount = selection.SelectedDimensionIds.Count;
        var axisCount = selection.SelectedAxisIds.Count;
        var leaderCount = selection.SelectedLeaderIds.Count;
        var textCount = selection.SelectedTextIds.Count;

        if (edgeCount == 0 && polygonCount == 0 && vertexCount == 0 && dimensionCount == 0 && axisCount == 0 && leaderCount == 0 && textCount == 0)
        {
            Context.SetArea(null);
            Context.SetLength(null);
            Context.SetSelectionInfo(Strings.Selection_NothingSelected);
            Context.SetStatus(status ?? Strings.Status_SelectionCleared);
            return;
        }

        if (leaderCount == 1 && edgeCount == 0 && polygonCount == 0 && vertexCount == 0 && dimensionCount == 0 && axisCount == 0 && textCount == 0)
        {
            var leader = document.Leaders.First(item => selection.SelectedLeaderIds.Contains(item.Id));
            Context.SetLength(null);
            Context.SetArea(null);
            Context.SetSelectionInfo(Strings.Format(Strings.Selection_LeaderWithText, leader.Text));
            Context.SetStatus(status ?? Strings.Status_LeaderSelected);
            return;
        }

        if (textCount == 1 && edgeCount == 0 && polygonCount == 0 && vertexCount == 0 && dimensionCount == 0 && axisCount == 0 && leaderCount == 0)
        {
            var note = document.Texts.First(item => selection.SelectedTextIds.Contains(item.Id));
            Context.SetLength(null);
            Context.SetArea(null);
            Context.SetSelectionInfo(Strings.Format(Strings.Selection_TextWithContent, note.Text.Replace('\n', ' ')));
            Context.SetStatus(status ?? Strings.Status_TextSelected);
            return;
        }

        if (dimensionCount == 1 && edgeCount == 0 && polygonCount == 0 && vertexCount == 0 && axisCount == 0)
        {
            var dimension = document.Dimensions.First(item => selection.SelectedDimensionIds.Contains(item.Id));
            var measured = DimensionService.GetMeasuredDistance(document, dimension);
            var unit = Context.Session.DisplayUnitSettings.LinearUnit;
            Context.SetLength(null);
            Context.SetArea(null);
            Context.SetSelectionInfo(Strings.Format(
                Strings.Selection_DimensionWithDistance,
                UnitDisplayFormatter.FormatLinear(measured, unit),
                UnitDisplayFormatter.FormatLinear(dimension.Offset, unit)));
            Context.SetStatus(status ?? Strings.Status_DimensionSelected);
            return;
        }

        if (vertexCount == 1 && edgeCount == 0 && polygonCount == 0 && dimensionCount == 0 && axisCount == 0)
        {
            var vertex = document.Vertices.First(item => selection.SelectedVertexIds.Contains(item.Id));
            var unit = Context.Session.DisplayUnitSettings.LinearUnit;
            Context.SetLength(null);
            Context.SetArea(null);
            Context.SetSelectionInfo(Strings.Format(
                Strings.Selection_VertexAt,
                UnitDisplayFormatter.FormatCoordinate(vertex.Position.X, unit),
                UnitDisplayFormatter.FormatCoordinate(vertex.Position.Y, unit)));
            Context.SetStatus(status ?? Strings.Status_VertexSelected);
            return;
        }

        if (axisCount == 1 && edgeCount == 0 && polygonCount == 0 && vertexCount == 0 && dimensionCount == 0)
        {
            var axis = document.Axes.First(item => selection.SelectedAxisIds.Contains(item.Id));
            var length = MathUtils.Distance(axis.Start, axis.End);
            var unit = Context.Session.DisplayUnitSettings.LinearUnit;
            Context.SetLength(length);
            Context.SetArea(null);
            Context.SetSelectionInfo(Strings.Format(
                Strings.Selection_AxisWithLength,
                UnitDisplayFormatter.FormatLinear(length, unit)));
            Context.SetStatus(status ?? Strings.Status_AxisSelected);
            return;
        }

        if (edgeCount == 1 && polygonCount == 0 && vertexCount == 0 && dimensionCount == 0 && axisCount == 0)
        {
            var edge = document.Edges.First(item => selection.SelectedEdgeIds.Contains(item.Id));
            var length = MathUtils.Distance(
                TopologyService.GetEdgeStartPoint(document, edge),
                TopologyService.GetEdgeEndPoint(document, edge));
            Context.SetLength(length);
            Context.SetArea(null);
            Context.SetSelectionInfo(Strings.Selection_Edge);
            Context.SetStatus(status ?? Strings.Status_EdgeSelected);
            return;
        }

        if (polygonCount == 1 && edgeCount == 0 && vertexCount == 0 && dimensionCount == 0 && axisCount == 0)
        {
            var polygon = document.Polygons.First(item => selection.SelectedPolygonIds.Contains(item.Id));
            var area = PolygonGeometry.GetArea(document, polygon, MathUtils.DefaultTolerance);
            Context.SetArea(area);
            Context.SetLength(null);
            Context.SetSelectionInfo(Strings.Format(
                Strings.Selection_PolygonWithArea,
                PolygonTypeDisplay.Get(polygon.Type),
                UnitDisplayFormatter.FormatArea(area)));
            Context.SetStatus(status ?? Strings.Status_PolygonSelected);
            return;
        }

        Context.SetLength(null);
        Context.SetArea(null);
        Context.SetSelectionInfo(Strings.Format(Strings.Selection_MultipleCount, vertexCount, edgeCount, polygonCount));
        Context.SetStatus(status ?? Strings.Status_MultipleObjectsSelected);
    }

    private bool IsMarqueeDrag()
    {
        if (Context is null)
        {
            return false;
        }

        var viewport = Context.GetViewportSize();
        var camera = Context.Session.Camera;
        var start = camera.WorldToScreen(_selectStartWorld, viewport);
        var end = camera.WorldToScreen(_selectCurrentWorld, viewport);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        return Math.Sqrt(dx * dx + dy * dy) >= MarqueeThresholdPixels;
    }

    private bool IsWindowSelection(Camera camera, Size viewport)
    {
        var start = camera.WorldToScreen(_selectStartWorld, viewport);
        var end = camera.WorldToScreen(_selectCurrentWorld, viewport);
        return end.X >= start.X;
    }

    private static bool IsShiftPressed()
        => Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

    private readonly struct SelectionBounds(double minX, double minY, double maxX, double maxY)
    {
        public double MinX { get; } = minX;
        public double MinY { get; } = minY;
        public double MaxX { get; } = maxX;
        public double MaxY { get; } = maxY;

        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;

        public static SelectionBounds FromPoints(PointF a, PointF b)
            => new(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y),
                Math.Max(a.X, b.X),
                Math.Max(a.Y, b.Y));

        public bool Contains(PointF point)
            => point.X >= MinX && point.X <= MaxX && point.Y >= MinY && point.Y <= MaxY;

        public bool ContainsSegmentFully(PointF start, PointF end)
            => Contains(start) && Contains(end);

        public bool IntersectsSegment(PointF start, PointF end)
        {
            if (Contains(start) || Contains(end))
            {
                return true;
            }

            var topLeft = new PointF((float)MinX, (float)MaxY);
            var topRight = new PointF((float)MaxX, (float)MaxY);
            var bottomLeft = new PointF((float)MinX, (float)MinY);
            var bottomRight = new PointF((float)MaxX, (float)MinY);

            return SegmentIntersects(start, end, topLeft, topRight) ||
                   SegmentIntersects(start, end, topRight, bottomRight) ||
                   SegmentIntersects(start, end, bottomRight, bottomLeft) ||
                   SegmentIntersects(start, end, bottomLeft, topLeft);
        }

        public bool ContainsPolygonFully(CadDocument document, Polygon polygon)
        {
            var points = PolygonGeometry.GetOuterBoundaryPoints(document, polygon);
            if (points.Count < 3)
            {
                return false;
            }

            foreach (var point in points)
            {
                if (!Contains(point))
                {
                    return false;
                }
            }

            return true;
        }

        public bool IntersectsPolygon(CadDocument document, Polygon polygon)
        {
            var points = PolygonGeometry.GetOuterBoundaryPoints(document, polygon);
            if (points.Count < 3)
            {
                return false;
            }

            foreach (var point in points)
            {
                if (Contains(point))
                {
                    return true;
                }
            }

            for (var i = 0; i < points.Count; i++)
            {
                var start = points[i];
                var end = points[(i + 1) % points.Count];
                if (IntersectsSegment(start, end))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SegmentIntersects(PointF aStart, PointF aEnd, PointF bStart, PointF bEnd)
            => Geometry2D.TryGetSegmentIntersection(aStart, aEnd, bStart, bEnd, out _, MathUtils.DefaultTolerance);
    }
}
