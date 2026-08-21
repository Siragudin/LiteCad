using LiteCad.Core.Document;
using LiteCad.Core.Geometry;

namespace LiteCad.Services;

public enum SelectionPickKind
{
    Vertex,
    Axis,
    Edge,
    Polygon
}

public readonly record struct SelectionPick(SelectionPickKind Kind, Guid Id);

public static class SelectionPickOperations
{
    public static SelectionPick? PickAt(CadDocument document, PointF world, double tolerance)
    {
        if (TryPickVertex(document, world, tolerance, out var vertexId))
        {
            return new SelectionPick(SelectionPickKind.Vertex, vertexId);
        }

        if (TryPickAxis(document, world, tolerance, out var axisId))
        {
            return new SelectionPick(SelectionPickKind.Axis, axisId);
        }

        if (TryPickEdge(document, world, tolerance, out var edgeId))
        {
            return new SelectionPick(SelectionPickKind.Edge, edgeId);
        }

        if (TryPickPolygon(document, world, tolerance, out var polygonId))
        {
            return new SelectionPick(SelectionPickKind.Polygon, polygonId);
        }

        return null;
    }

    public static void ApplyToSelection(SelectionPick pick, Core.Selection.Selection selection)
    {
        selection.Clear();

        switch (pick.Kind)
        {
            case SelectionPickKind.Vertex:
                selection.SelectedVertexIds.Add(pick.Id);
                break;
            case SelectionPickKind.Axis:
                selection.SelectedAxisIds.Add(pick.Id);
                break;
            case SelectionPickKind.Edge:
                selection.SelectedEdgeIds.Add(pick.Id);
                break;
            case SelectionPickKind.Polygon:
                selection.SelectedPolygonIds.Add(pick.Id);
                break;
        }
    }

    private static bool TryPickVertex(CadDocument document, PointF world, double tolerance, out Guid vertexId)
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
            vertexId = Guid.Empty;
            return false;
        }

        vertexId = closestVertex.Id;
        return true;
    }

    private static bool TryPickAxis(CadDocument document, PointF world, double tolerance, out Guid axisId)
    {
        Axis? closestAxis = null;
        var closestDistance = tolerance;

        foreach (var axis in document.Axes)
        {
            if (!Geometry2D.TryProjectPointOnSegment(world, axis.Start, axis.End, out _, out var distance, tolerance) ||
                distance > closestDistance)
            {
                continue;
            }

            closestAxis = axis;
            closestDistance = distance;
        }

        if (closestAxis is null)
        {
            axisId = Guid.Empty;
            return false;
        }

        axisId = closestAxis.Id;
        return true;
    }

    private static bool TryPickEdge(CadDocument document, PointF world, double tolerance, out Guid edgeId)
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
            edgeId = Guid.Empty;
            return false;
        }

        edgeId = closestEdge.Id;
        return true;
    }

    private static bool TryPickPolygon(CadDocument document, PointF world, double tolerance, out Guid polygonId)
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
            polygonId = Guid.Empty;
            return false;
        }

        polygonId = closestPolygon.Id;
        return true;
    }
}
