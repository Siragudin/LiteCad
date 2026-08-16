using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
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
        Context.RequestRedraw();
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
            ? Color.FromArgb(0x30, 0x21, 0x96, 0xF3)
            : Color.FromArgb(0x30, 0x4C, 0xAF, 0x50));
        fill.Freeze();

        var stroke = RenderStyles.CreateScreenPen(
            new SolidColorBrush(isWindow ? Color.FromRgb(0x21, 0x96, 0xF3) : Color.FromRgb(0x4C, 0xAF, 0x50)),
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

        if (!additive)
        {
            selection.Clear();
        }

        if (TryToggleVertexAt(document, selection, world, tolerance, additive))
        {
            UpdateSelectionUi();
            return;
        }

        if (TryToggleEdgeAt(document, selection, world, tolerance, additive))
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

    private bool TryToggleVertexAt(
        CadDocument document,
        Core.Selection.Selection selection,
        PointF world,
        double tolerance,
        bool additive)
    {
        Vertex? closestVertex = null;
        var closestDistance = tolerance;

        foreach (var vertex in document.Vertices)
        {
            var distance = MathUtils.Distance(world, vertex.Position);
            if (distance > closestDistance)
            {
                continue;
            }

            closestVertex = vertex;
            closestDistance = distance;
        }

        if (closestVertex is null)
        {
            return false;
        }

        if (additive && selection.SelectedVertexIds.Contains(closestVertex.Id))
        {
            selection.SelectedVertexIds.Remove(closestVertex.Id);
        }
        else
        {
            selection.SelectedVertexIds.Add(closestVertex.Id);
        }

        Context!.SetStatus(additive ? Strings.Status_SelectionUpdated : Strings.Status_VertexSelected);
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
            if (!Geometry2D.TryProjectPointOnSegment(world, start, end, out _, out var distance, tolerance) ||
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

        if (edgeCount == 0 && polygonCount == 0 && vertexCount == 0)
        {
            Context.SetArea(null);
            Context.SetLength(null);
            Context.SetSelectionInfo(Strings.Selection_NothingSelected);
            Context.SetStatus(status ?? Strings.Status_SelectionCleared);
            return;
        }

        if (vertexCount == 1 && edgeCount == 0 && polygonCount == 0)
        {
            var vertex = document.Vertices.First(item => selection.SelectedVertexIds.Contains(item.Id));
            Context.SetLength(null);
            Context.SetArea(null);
            Context.SetSelectionInfo(Strings.Format(Strings.Selection_VertexAt, vertex.Position.X, vertex.Position.Y));
            Context.SetStatus(status ?? Strings.Status_VertexSelected);
            return;
        }

        if (edgeCount == 1 && polygonCount == 0 && vertexCount == 0)
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

        if (polygonCount == 1 && edgeCount == 0 && vertexCount == 0)
        {
            var polygon = document.Polygons.First(item => selection.SelectedPolygonIds.Contains(item.Id));
            var area = PolygonGeometry.GetArea(document, polygon, MathUtils.DefaultTolerance);
            Context.SetArea(area);
            Context.SetLength(null);
            Context.SetSelectionInfo(Strings.Format(Strings.Selection_PolygonWithArea, PolygonTypeDisplay.Get(polygon.Type), area));
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
