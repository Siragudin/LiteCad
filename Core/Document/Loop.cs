namespace LiteCad.Core.Document;

public sealed class Loop
{
    public List<DirectedEdgeReference> Edges { get; } = [];

    public Loop Clone()
    {
        var clone = new Loop();
        clone.Edges.AddRange(Edges);
        return clone;
    }
}
