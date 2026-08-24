using LiteCad.Core.Geometry;

namespace LiteCad.Core.Document;

/// <summary>
/// O(1) vertex/edge lookup and spatial indexing for coordinate-based vertex search.
/// Must stay in sync with <see cref="CadDocument.Vertices"/> and <see cref="CadDocument.Edges"/>.
/// </summary>
internal sealed class CadDocumentIndex
{
    /// <summary>
    /// World-space grid cell size. Queries expand by tolerance in cell units.
    /// </summary>
    private const double SpatialCellSize = 1.0;

    private readonly Dictionary<Guid, Vertex> _verticesById = new();
    private readonly Dictionary<Guid, Edge> _edgesById = new();
    private readonly Dictionary<(long X, long Y), List<Vertex>> _spatialCells = new();

    public void Clear()
    {
        _verticesById.Clear();
        _edgesById.Clear();
        _spatialCells.Clear();
    }

    public void ClearVertices()
    {
        _verticesById.Clear();
        _spatialCells.Clear();
    }

    public void ClearEdges()
        => _edgesById.Clear();

    public void AddVertex(Vertex vertex)
    {
        _verticesById[vertex.Id] = vertex;
        AddToSpatialCell(vertex);
    }

    public void AddEdge(Edge edge)
        => _edgesById[edge.Id] = edge;

    public void RemoveVertex(Vertex vertex)
    {
        _verticesById.Remove(vertex.Id);
        RemoveFromSpatialCell(vertex);
    }

    public void RemoveEdge(Edge edge)
        => _edgesById.Remove(edge.Id);

    public void UpdateVertexPosition(Vertex vertex, PointF oldPosition, PointF newPosition)
    {
        if (MathUtils.ArePointsEqual(oldPosition, newPosition, TopologyTolerance.ForMutation))
        {
            return;
        }

        RemoveFromSpatialCellAt(vertex, oldPosition);
        AddToSpatialCellAt(vertex, newPosition);
    }

    public bool TryGetVertex(Guid vertexId, out Vertex vertex)
        => _verticesById.TryGetValue(vertexId, out vertex!);

    public bool TryGetEdge(Guid edgeId, out Edge edge)
        => _edgesById.TryGetValue(edgeId, out edge!);

    public Vertex? FindVertex(CadDocument document, PointF position, double tolerance)
    {
        tolerance = TopologyTolerance.Resolve(tolerance);
        var candidateIds = CollectCandidateIds(position, tolerance);
        if (candidateIds.Count == 0)
        {
            return null;
        }

        foreach (var vertex in document.Vertices)
        {
            if (candidateIds.Contains(vertex.Id) &&
                MathUtils.ArePointsEqual(vertex.Position, position, tolerance))
            {
                return vertex;
            }
        }

        return null;
    }

    public void Rebuild(IReadOnlyList<Vertex> vertices, IReadOnlyList<Edge> edges)
    {
        Clear();
        foreach (var vertex in vertices)
        {
            AddVertex(vertex);
        }

        foreach (var edge in edges)
        {
            AddEdge(edge);
        }
    }

    private HashSet<Guid> CollectCandidateIds(PointF position, double tolerance)
    {
        var result = new HashSet<Guid>();
        var minCellX = (long)Math.Floor((position.X - tolerance) / SpatialCellSize);
        var maxCellX = (long)Math.Floor((position.X + tolerance) / SpatialCellSize);
        var minCellY = (long)Math.Floor((position.Y - tolerance) / SpatialCellSize);
        var maxCellY = (long)Math.Floor((position.Y + tolerance) / SpatialCellSize);

        for (var cellX = minCellX; cellX <= maxCellX; cellX++)
        {
            for (var cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                if (!_spatialCells.TryGetValue((cellX, cellY), out var bucket))
                {
                    continue;
                }

                foreach (var vertex in bucket)
                {
                    result.Add(vertex.Id);
                }
            }
        }

        return result;
    }

    private void AddToSpatialCell(Vertex vertex)
        => AddToSpatialCellAt(vertex, vertex.Position);

    private void AddToSpatialCellAt(Vertex vertex, PointF position)
    {
        var key = ToCellKey(position);
        if (!_spatialCells.TryGetValue(key, out var bucket))
        {
            bucket = [];
            _spatialCells[key] = bucket;
        }

        bucket.Add(vertex);
    }

    private void RemoveFromSpatialCell(Vertex vertex)
        => RemoveFromSpatialCellAt(vertex, vertex.Position);

    private void RemoveFromSpatialCellAt(Vertex vertex, PointF position)
    {
        var key = ToCellKey(position);
        if (!_spatialCells.TryGetValue(key, out var bucket))
        {
            return;
        }

        bucket.Remove(vertex);
        if (bucket.Count == 0)
        {
            _spatialCells.Remove(key);
        }
    }

    private static (long X, long Y) ToCellKey(PointF position)
        => ((long)Math.Floor(position.X / SpatialCellSize), (long)Math.Floor(position.Y / SpatialCellSize));
}
