using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;

namespace LiteCad.Services;

public sealed class MoveEdgeSnapshot
{
    public required Guid OriginalEdgeId { get; init; }

    public required PointF Start { get; init; }

    public required PointF End { get; init; }

    public required Edge Template { get; init; }
}

public sealed class MovePolygonSnapshot
{
    public required PolygonType Type { get; init; }

    public required List<DirectedEdgeReference> OuterLoop { get; init; }

    public required List<List<DirectedEdgeReference>> InnerLoops { get; init; }
}

public sealed class MoveAxisSnapshot
{
    public required Guid OriginalAxisId { get; init; }

    public required PointF Start { get; init; }

    public required PointF End { get; init; }
}

public sealed class MoveObjectSnapshot
{
    public List<MoveEdgeSnapshot> Edges { get; } = [];

    public List<MovePolygonSnapshot> UserPolygons { get; } = [];

    public List<MoveAxisSnapshot> Axes { get; } = [];
}

public static class MoveOperations
{
    public static MoveObjectSnapshot CreateSnapshot(CadDocument document, Selection selection)
    {
        var snapshot = new MoveObjectSnapshot();
        var edgeIds = CollectSelectedEdgeIds(document, selection);

        foreach (var edgeId in edgeIds.OrderBy(id => id))
        {
            var edge = document.Edges.First(item => item.Id == edgeId);
            snapshot.Edges.Add(new MoveEdgeSnapshot
            {
                OriginalEdgeId = edge.Id,
                Start = TopologyService.GetEdgeStartPoint(document, edge),
                End = TopologyService.GetEdgeEndPoint(document, edge),
                Template = edge.CloneGeometry()
            });
        }

        foreach (var polygon in document.Polygons)
        {
            if (!selection.SelectedPolygonIds.Contains(polygon.Id) || polygon.Type == PolygonType.Face)
            {
                continue;
            }

            snapshot.UserPolygons.Add(new MovePolygonSnapshot
            {
                Type = polygon.Type,
                OuterLoop = polygon.OuterLoop.Edges.ToList(),
                InnerLoops = polygon.InnerLoops
                    .Select(loop => loop.Edges.ToList())
                    .ToList()
            });
        }

        foreach (var axisId in selection.SelectedAxisIds.OrderBy(id => id))
        {
            var axis = document.Axes.First(item => item.Id == axisId);
            snapshot.Axes.Add(new MoveAxisSnapshot
            {
                OriginalAxisId = axis.Id,
                Start = axis.Start,
                End = axis.End
            });
        }

        return snapshot;
    }

    public static void MoveVertices(CadDocument document, IEnumerable<Guid> vertexIds, PointF delta)
    {
        foreach (var vertexId in vertexIds)
        {
            var vertex = TopologyService.GetVertex(document, vertexId);
            TopologyService.MoveVertex(
                document,
                vertexId,
                Translate(vertex.Position, delta));
        }

        PolygonBuilder.SyncFaces(document, TopologyTolerance.ForMutation);
    }

    public static HashSet<Guid> ExecuteObjectMove(
        CadDocument document,
        Selection selection,
        MoveObjectSnapshot snapshot,
        PointF delta)
    {
        var edgeIdsToRemove = snapshot.Edges
            .Select(entry => entry.OriginalEdgeId)
            .ToHashSet();

        var fillMigrationEntries = FaceFillMigration.CaptureAffectedFaceFills(document, edgeIdsToRemove);

        var userPolygonIds = document.Polygons
            .Where(polygon =>
                selection.SelectedPolygonIds.Contains(polygon.Id) &&
                polygon.Type != PolygonType.Face)
            .Select(polygon => polygon.Id)
            .ToHashSet();

        document.Edges.RemoveAll(edge => edgeIdsToRemove.Contains(edge.Id));
        document.Polygons.RemoveAll(polygon => userPolygonIds.Contains(polygon.Id));

        if (edgeIdsToRemove.Count > 0)
        {
            PolygonBuilder.InvalidateSuppressionOnEdgeTopologyChange(document);
            TopologyService.PruneUnusedVertices(document);
        }

        var tolerance = TopologyTolerance.ForMutation;
        var oldToNewEdgeIds = new Dictionary<Guid, Guid>();

        foreach (var entry in snapshot.Edges)
        {
            var recreated = TopologyService.CreateEdgeFromPoints(
                document,
                Translate(entry.Start, delta),
                Translate(entry.End, delta),
                entry.Template,
                tolerance);

            oldToNewEdgeIds[entry.OriginalEdgeId] = recreated.Id;
        }

        foreach (var polygonSnapshot in snapshot.UserPolygons)
        {
            var polygon = new Polygon { Type = polygonSnapshot.Type };
            RemapLoop(polygon.OuterLoop, polygonSnapshot.OuterLoop, oldToNewEdgeIds);

            foreach (var innerLoop in polygonSnapshot.InnerLoops)
            {
                var loop = new Loop();
                RemapLoop(loop, innerLoop, oldToNewEdgeIds);
                polygon.InnerLoops.Add(loop);
            }

            document.Polygons.Add(polygon);
        }

        PolygonBuilder.SyncFaces(document, tolerance);
        FaceFillMigration.ApplyMoveOrRotate(document, fillMigrationEntries, point => Translate(point, delta));

        foreach (var entry in snapshot.Axes)
        {
            var axis = document.Axes.FirstOrDefault(item => item.Id == entry.OriginalAxisId);
            if (axis is null)
            {
                continue;
            }

            axis.Start = Translate(entry.Start, delta);
            axis.End = Translate(entry.End, delta);
        }

        selection.SelectedEdgeIds.Clear();
        selection.SelectedPolygonIds.Clear();

        foreach (var newEdgeId in oldToNewEdgeIds.Values)
        {
            selection.SelectedEdgeIds.Add(newEdgeId);
        }

        return oldToNewEdgeIds.Values.ToHashSet();
    }

    public static bool CanMove(Selection selection)
        => selection.SelectedVertexIds.Count > 0
            || selection.SelectedEdgeIds.Count > 0
            || selection.SelectedPolygonIds.Count > 0
            || selection.SelectedAxisIds.Count > 0;

    public static bool UsesVertexMove(Selection selection)
        => selection.SelectedVertexIds.Count > 0
            && selection.SelectedEdgeIds.Count == 0
            && selection.SelectedPolygonIds.Count == 0
            && selection.SelectedAxisIds.Count == 0;

    private static HashSet<Guid> CollectSelectedEdgeIds(CadDocument document, Selection selection)
    {
        var edgeIds = new HashSet<Guid>(selection.SelectedEdgeIds);

        foreach (var polygon in document.Polygons)
        {
            if (!selection.SelectedPolygonIds.Contains(polygon.Id))
            {
                continue;
            }

            foreach (var edgeId in PolygonGeometry.GetAllEdgeIds(polygon))
            {
                edgeIds.Add(edgeId);
            }
        }

        return edgeIds;
    }

    private static void RemapLoop(
        Loop target,
        IReadOnlyList<DirectedEdgeReference> source,
        IReadOnlyDictionary<Guid, Guid> oldToNewEdgeIds)
    {
        foreach (var reference in source)
        {
            if (!oldToNewEdgeIds.TryGetValue(reference.EdgeId, out var newEdgeId))
            {
                continue;
            }

            target.Edges.Add(new DirectedEdgeReference(newEdgeId, reference.Forward));
        }
    }

    private static PointF Translate(PointF point, PointF delta)
        => new(point.X + delta.X, point.Y + delta.Y);
}
