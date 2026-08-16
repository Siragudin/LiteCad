namespace LiteCad.Core.Selection;

public sealed class Selection
{
    public HashSet<Guid> SelectedEdgeIds { get; } = [];

    public HashSet<Guid> SelectedPolygonIds { get; } = [];

    public HashSet<Guid> SelectedVertexIds { get; } = [];

    public void Clear()
    {
        SelectedEdgeIds.Clear();
        SelectedPolygonIds.Clear();
        SelectedVertexIds.Clear();
    }

    public bool IsEmpty =>
        SelectedEdgeIds.Count == 0 &&
        SelectedPolygonIds.Count == 0 &&
        SelectedVertexIds.Count == 0;
}
