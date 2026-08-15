namespace LiteCad.Core.Selection;

public sealed class Selection
{
    public HashSet<Guid> SelectedEdgeIds { get; } = [];

    public HashSet<Guid> SelectedPolygonIds { get; } = [];

    public void Clear()
    {
        SelectedEdgeIds.Clear();
        SelectedPolygonIds.Clear();
    }

    public bool IsEmpty => SelectedEdgeIds.Count == 0 && SelectedPolygonIds.Count == 0;
}
