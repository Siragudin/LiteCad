namespace LiteCad.Core.Selection;

public sealed class Selection
{
    public HashSet<Guid> SelectedEdgeIds { get; } = [];

    public HashSet<Guid> SelectedPolygonIds { get; } = [];

    public HashSet<Guid> SelectedVertexIds { get; } = [];

    public HashSet<Guid> SelectedDimensionIds { get; } = [];

    public HashSet<Guid> SelectedAxisIds { get; } = [];

    public void Clear()
    {
        SelectedEdgeIds.Clear();
        SelectedPolygonIds.Clear();
        SelectedVertexIds.Clear();
        SelectedDimensionIds.Clear();
        SelectedAxisIds.Clear();
    }

    public bool IsEmpty =>
        SelectedEdgeIds.Count == 0 &&
        SelectedPolygonIds.Count == 0 &&
        SelectedVertexIds.Count == 0 &&
        SelectedDimensionIds.Count == 0 &&
        SelectedAxisIds.Count == 0;
}
