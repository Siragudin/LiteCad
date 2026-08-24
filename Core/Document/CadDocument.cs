using LiteCad.Dimensions;
using LiteCad.Leaders;
using LiteCad.Texts;

namespace LiteCad.Core.Document;

public sealed class CadDocument
{
    internal CadDocumentIndex Index { get; } = new();

    private int _mutationBatchDepth;
    private DocumentChangeKind _pendingChangeKinds;

    /// <summary>
    /// Monotonically increases on every committed document mutation.
    /// </summary>
    public long Revision { get; private set; }

    public event EventHandler<DocumentChangedEventArgs>? Changed;

    public List<Vertex> Vertices { get; } = [];

    public List<Edge> Edges { get; } = [];

    public List<Polygon> Polygons { get; } = [];

    public List<Dimension> Dimensions { get; } = [];

    public List<Axis> Axes { get; } = [];

    public List<Leader> Leaders { get; } = [];

    public List<TextNote> Texts { get; } = [];

    /// <summary>
    /// User-defined elevation zero in model Y. Null until the first elevation leader is placed.
    /// </summary>
    public double? ElevationBaseY { get; set; }

    /// <summary>
    /// Geometry-based keys for faces the user explicitly removed.
    /// Not tied to Edge.Id — derived faces can restore when topology is rebuilt.
    /// </summary>
    public HashSet<string> SuppressedFaceGeometryKeys { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Visual fill styles for derived faces, keyed by stable face geometry identity.
    /// Not part of topology — metadata only.
    /// </summary>
    public Dictionary<string, FaceFillStyle> FaceFillStyles { get; } = new(StringComparer.Ordinal);

    public CadDocumentMutationBatch BeginMutationBatch()
        => new(this);

    public void NotifyChanged(DocumentChangeKind kind)
    {
        if (kind == DocumentChangeKind.None)
        {
            return;
        }

        if (_mutationBatchDepth > 0)
        {
            _pendingChangeKinds |= kind;
            return;
        }

        BumpRevision(kind);
    }

    internal void BeginMutationBatchCore()
        => _mutationBatchDepth++;

    internal void EndMutationBatchCore()
    {
        _mutationBatchDepth--;
        if (_mutationBatchDepth > 0 || _pendingChangeKinds == DocumentChangeKind.None)
        {
            return;
        }

        var kinds = _pendingChangeKinds;
        _pendingChangeKinds = DocumentChangeKind.None;
        BumpRevision(kinds);
    }

    private void BumpRevision(DocumentChangeKind kind)
    {
        Revision++;
        Changed?.Invoke(this, new DocumentChangedEventArgs(Revision, kind));
    }

    internal void AddVertex(Vertex vertex)
    {
        Vertices.Add(vertex);
        Index.AddVertex(vertex);
    }

    internal void AddVertices(IEnumerable<Vertex> vertices)
    {
        foreach (var vertex in vertices)
        {
            AddVertex(vertex);
        }
    }

    internal void AddEdge(Edge edge)
    {
        Edges.Add(edge);
        Index.AddEdge(edge);
    }

    internal void ClearTopology()
    {
        Vertices.Clear();
        Edges.Clear();
        Index.Clear();
    }

    internal void ClearVertices()
    {
        Vertices.Clear();
        Index.ClearVertices();
    }

    internal void ClearEdges()
    {
        Edges.Clear();
        Index.ClearEdges();
    }

    internal void RemoveEdge(Edge edge)
    {
        Edges.Remove(edge);
        Index.RemoveEdge(edge);
    }

    internal int RemoveEdgesWhere(Predicate<Edge> match)
    {
        var removed = 0;
        for (var i = Edges.Count - 1; i >= 0; i--)
        {
            var edge = Edges[i];
            if (!match(edge))
            {
                continue;
            }

            Index.RemoveEdge(edge);
            Edges.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    internal void RemoveUnusedVertices(HashSet<Guid> usedVertexIds)
    {
        foreach (var dimension in Dimensions)
        {
            usedVertexIds.Add(dimension.FirstVertexId);
            usedVertexIds.Add(dimension.SecondVertexId);
        }

        for (var i = Vertices.Count - 1; i >= 0; i--)
        {
            var vertex = Vertices[i];
            if (usedVertexIds.Contains(vertex.Id))
            {
                continue;
            }

            Index.RemoveVertex(vertex);
            Vertices.RemoveAt(i);
        }
    }
}
