using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;

namespace LiteCad.Services;

public static class CopyOperations
{
    public static bool CanCopy(Selection selection)
        => selection.SelectedEdgeIds.Count > 0
            || selection.SelectedPolygonIds.Count > 0
            || selection.SelectedAxisIds.Count > 0;

    public static PointF GetBasePoint(MoveObjectSnapshot snapshot)
    {
        if (snapshot.Edges.Count > 0)
        {
            return snapshot.Edges[0].Start;
        }

        if (snapshot.Axes.Count > 0)
        {
            return snapshot.Axes[0].Start;
        }

        return PointF.Zero;
    }

    public static HashSet<Guid> ExecuteObjectCopy(
        CadDocument document,
        Selection selection,
        MoveObjectSnapshot snapshot,
        PointF delta)
    {
        var tolerance = TopologyTolerance.ForMutation;
        var oldToNewEdgeIds = new Dictionary<Guid, Guid>();
        var affectedEdgeIds = snapshot.Edges
            .Select(entry => entry.OriginalEdgeId)
            .ToHashSet();
        var identitiesBefore = FaceFillMigration.CaptureFaceIdentities(document);
        var fillMigrationEntries = FaceFillMigration.CaptureAffectedFaceFills(document, affectedEdgeIds);

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

        var newUserPolygonIds = new List<Guid>();
        var newAxisIds = new List<Guid>();

        foreach (var entry in snapshot.Axes)
        {
            var copy = new Axis(
                Translate(entry.Start, delta),
                Translate(entry.End, delta));
            document.Axes.Add(copy);
            newAxisIds.Add(copy.Id);
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
        FaceFillMigration.ApplyCopyOrMirror(
            document,
            fillMigrationEntries,
            point => Translate(point, delta),
            identitiesBefore);

        selection.SelectedEdgeIds.Clear();
        selection.SelectedPolygonIds.Clear();
        selection.SelectedAxisIds.Clear();

        foreach (var newEdgeId in oldToNewEdgeIds.Values)
        {
            selection.SelectedEdgeIds.Add(newEdgeId);
        }

        foreach (var polygonId in newUserPolygonIds)
        {
            selection.SelectedPolygonIds.Add(polygonId);
        }

        foreach (var axisId in newAxisIds)
        {
            selection.SelectedAxisIds.Add(axisId);
        }

        document.NotifyChanged(
            DocumentChangeKind.Topology | DocumentChangeKind.FaceFill | DocumentChangeKind.Axes);
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

    private static PointF Translate(PointF point, PointF delta)
        => new(point.X + delta.X, point.Y + delta.Y);
}
