namespace LiteCad.Core.Document;

public readonly struct DirectedEdgeReference(Guid edgeId, bool forward)
{
    public Guid EdgeId { get; } = edgeId;

    /// <summary>
    /// True when traversing StartVertex → EndVertex; false for reverse.
    /// </summary>
    public bool Forward { get; } = forward;
}
