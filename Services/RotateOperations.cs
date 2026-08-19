using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;

namespace LiteCad.Services;

public static class RotateOperations
{
    public static bool CanRotate(Selection selection)
        => CopyOperations.CanCopy(selection);

    public static PointF RotatePoint(PointF point, PointF pivot, double angleRadians)
    {
        var dx = point.X - pivot.X;
        var dy = point.Y - pivot.Y;
        var cos = Math.Cos(angleRadians);
        var sin = Math.Sin(angleRadians);
        return new PointF(
            pivot.X + (float)(dx * cos - dy * sin),
            pivot.Y + (float)(dx * sin + dy * cos));
    }

    public static HashSet<Guid> ExecuteObjectRotate(
        CadDocument document,
        Selection selection,
        MoveObjectSnapshot snapshot,
        PointF pivot,
        double angleRadians)
    {
        var edgeIdsToRemove = snapshot.Edges
            .Select(entry => entry.OriginalEdgeId)
            .ToHashSet();

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
                RotatePoint(entry.Start, pivot, angleRadians),
                RotatePoint(entry.End, pivot, angleRadians),
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

        selection.SelectedEdgeIds.Clear();
        selection.SelectedPolygonIds.Clear();

        foreach (var newEdgeId in oldToNewEdgeIds.Values)
        {
            selection.SelectedEdgeIds.Add(newEdgeId);
        }

        return oldToNewEdgeIds.Values.ToHashSet();
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
}
