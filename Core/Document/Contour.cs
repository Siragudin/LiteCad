namespace LiteCad.Core.Document;

public sealed class Contour
{
    public List<Guid> EdgeIds { get; } = [];

    public bool IsClosed => EdgeIds.Count >= 3;
}
