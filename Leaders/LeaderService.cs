using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;

namespace LiteCad.Leaders;

public static class LeaderService
{
    public static Leader Create(
        CadDocument document,
        LeaderKind kind,
        PointF target,
        PointF textPosition,
        string text)
    {
        var leader = new Leader(Guid.NewGuid(), kind, target, textPosition, text);
        document.Leaders.Add(leader);
        document.NotifyChanged(DocumentChangeKind.Annotations);
        return leader;
    }

    public static Leader CreateElevation(CadDocument document, PointF measurePoint, PointF graphicOrigin)
    {
        var changeKind = DocumentChangeKind.Annotations;
        if (!document.ElevationBaseY.HasValue)
        {
            document.ElevationBaseY = measurePoint.Y;
            changeKind |= DocumentChangeKind.FaceFill;
        }

        var delta = ElevationFormatting.ComputeDelta(document.ElevationBaseY.Value, measurePoint.Y);
        graphicOrigin = LeaderGeometry.ConstrainHorizontal(measurePoint, graphicOrigin);
        var sideHint = LeaderGeometry.CreateSideHint(
            graphicOrigin,
            LeaderGeometry.ResolveSide(measurePoint, graphicOrigin));
        var leader = new Leader(
            Guid.NewGuid(),
            LeaderKind.Elevation,
            graphicOrigin,
            sideHint,
            ElevationFormatting.Format(delta));
        document.Leaders.Add(leader);
        document.NotifyChanged(changeKind);
        return leader;
    }

    public static int DeleteSelected(CadDocument document, Selection selection)
    {
        if (selection.SelectedLeaderIds.Count == 0)
        {
            return 0;
        }

        var removed = document.Leaders.RemoveAll(item => selection.SelectedLeaderIds.Contains(item.Id));
        selection.SelectedLeaderIds.Clear();
        if (removed > 0)
        {
            document.NotifyChanged(DocumentChangeKind.Annotations);
        }

        return removed;
    }
}
