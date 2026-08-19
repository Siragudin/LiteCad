using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;

namespace LiteCad.Services;

public static class MirrorOperations
{
    public static bool CanMirror(Selection selection)
        => CopyOperations.CanCopy(selection);

    public static bool IsValidAxis(PointF axisStart, PointF axisEnd)
        => MathUtils.Distance(axisStart, axisEnd) > TopologyTolerance.ForMutation;

    public static PointF MirrorPoint(PointF point, PointF axisStart, PointF axisEnd)
    {
        var dx = axisEnd.X - axisStart.X;
        var dy = axisEnd.Y - axisStart.Y;
        var lengthSq = dx * dx + dy * dy;
        var tolerance = TopologyTolerance.ForMutation;
        if (lengthSq <= tolerance * tolerance)
        {
            return point;
        }

        var t = ((point.X - axisStart.X) * dx + (point.Y - axisStart.Y) * dy) / lengthSq;
        var footX = axisStart.X + t * dx;
        var footY = axisStart.Y + t * dy;
        return new PointF(
            2 * footX - point.X,
            2 * footY - point.Y);
    }

    public static HashSet<Guid> ExecuteObjectMirror(
        CadDocument document,
        Selection selection,
        MoveObjectSnapshot snapshot,
        PointF axisStart,
        PointF axisEnd)
    {
        if (!IsValidAxis(axisStart, axisEnd))
        {
            return [];
        }

        var tolerance = TopologyTolerance.ForMutation;
        var oldToNewEdgeIds = new Dictionary<Guid, Guid>();
        var newUserPolygonIds = new List<Guid>();

        foreach (var entry in snapshot.Edges)
        {
            var recreated = TopologyService.CreateEdgeFromPoints(
                document,
                MirrorPoint(entry.Start, axisStart, axisEnd),
                MirrorPoint(entry.End, axisStart, axisEnd),
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
            newUserPolygonIds.Add(polygon.Id);
        }

        PolygonBuilder.SyncFaces(document, tolerance);

        selection.SelectedEdgeIds.Clear();
        selection.SelectedPolygonIds.Clear();

        foreach (var newEdgeId in oldToNewEdgeIds.Values)
        {
            selection.SelectedEdgeIds.Add(newEdgeId);
        }

        foreach (var polygonId in newUserPolygonIds)
        {
            selection.SelectedPolygonIds.Add(polygonId);
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

            target.Edges.Add(new DirectedEdgeReference(newEdgeId, !reference.Forward));
        }
    }
}
