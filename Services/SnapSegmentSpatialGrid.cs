using LiteCad.Core.Geometry;

namespace LiteCad.Services;

/// <summary>
/// Uniform grid broad-phase index for segment contact queries in <see cref="SnapService"/>.
/// </summary>
internal sealed class SnapSegmentSpatialGrid
{
    private const double CellSize = 50.0;
    private const int MaxCellsPerAxis = 128;

    private readonly List<Entry> _entries;
    private readonly Dictionary<(long X, long Y), List<int>> _cells = new();
    private readonly List<int> _longSegmentIndices = [];

    private readonly struct Entry
    {
        public required Guid Id { get; init; }

        public required PointF Start { get; init; }

        public required PointF End { get; init; }

        public required double MinX { get; init; }

        public required double MinY { get; init; }

        public required double MaxX { get; init; }

        public required double MaxY { get; init; }
    }

    private SnapSegmentSpatialGrid(List<Entry> entries)
    {
        _entries = entries;
    }

    public int Count => _entries.Count;

    public static SnapSegmentSpatialGrid FromSegments(
        IEnumerable<(Guid Id, PointF Start, PointF End)> segments,
        double tolerance)
    {
        tolerance = TopologyTolerance.Resolve(tolerance);
        var entries = new List<Entry>();
        foreach (var (id, start, end) in segments)
        {
            var bounds = ExpandBounds(start, end, tolerance);
            entries.Add(new Entry
            {
                Id = id,
                Start = start,
                End = end,
                MinX = bounds.MinX,
                MinY = bounds.MinY,
                MaxX = bounds.MaxX,
                MaxY = bounds.MaxY
            });
        }

        var grid = new SnapSegmentSpatialGrid(entries);
        grid.BuildCells();
        return grid;
    }

    public PointF GetStart(int index) => _entries[index].Start;

    public PointF GetEnd(int index) => _entries[index].End;

    public HashSet<int> GetCandidateIndicesForSegment(PointF start, PointF end, double tolerance)
    {
        tolerance = TopologyTolerance.Resolve(tolerance);
        var bounds = ExpandBounds(start, end, tolerance);
        return CollectCandidates(bounds.MinX, bounds.MinY, bounds.MaxX, bounds.MaxY, excludeIndex: -1);
    }

    public IReadOnlyList<(int OuterIndex, int InnerIndex)> GetOrderedPairIndices()
    {
        var seen = new HashSet<(int OuterIndex, int InnerIndex)>();
        var pairs = new List<(int OuterIndex, int InnerIndex)>();

        foreach (var bucket in _cells.Values)
        {
            for (var i = 0; i < bucket.Count; i++)
            {
                for (var j = i + 1; j < bucket.Count; j++)
                {
                    TryAddPair(bucket[i], bucket[j], seen, pairs);
                }
            }
        }

        for (var i = 0; i < _longSegmentIndices.Count; i++)
        {
            var longIndex = _longSegmentIndices[i];
            for (var otherIndex = 0; otherIndex < _entries.Count; otherIndex++)
            {
                TryAddPair(longIndex, otherIndex, seen, pairs);
            }
        }

        pairs.Sort(static (left, right) =>
        {
            var outerCompare = left.OuterIndex.CompareTo(right.OuterIndex);
            return outerCompare != 0
                ? outerCompare
                : left.InnerIndex.CompareTo(right.InnerIndex);
        });

        return pairs;
    }

    private void TryAddPair(
        int indexA,
        int indexB,
        HashSet<(int OuterIndex, int InnerIndex)> seen,
        List<(int OuterIndex, int InnerIndex)> pairs)
    {
        if (indexA == indexB)
        {
            return;
        }

        var entryA = _entries[indexA];
        var entryB = _entries[indexB];
        if (!BoundsOverlap(entryA, entryB))
        {
            return;
        }

        int outerIndex;
        int innerIndex;
        if (entryA.Id.CompareTo(entryB.Id) < 0)
        {
            outerIndex = indexA;
            innerIndex = indexB;
        }
        else if (entryB.Id.CompareTo(entryA.Id) < 0)
        {
            outerIndex = indexB;
            innerIndex = indexA;
        }
        else
        {
            return;
        }

        if (!seen.Add((outerIndex, innerIndex)))
        {
            return;
        }

        pairs.Add((outerIndex, innerIndex));
    }

    private HashSet<int> CollectCandidates(
        double minX,
        double minY,
        double maxX,
        double maxY,
        int excludeIndex)
    {
        var result = new HashSet<int>();
        var minCellX = (long)Math.Floor(minX / CellSize);
        var maxCellX = (long)Math.Floor(maxX / CellSize);
        var minCellY = (long)Math.Floor(minY / CellSize);
        var maxCellY = (long)Math.Floor(maxY / CellSize);

        for (var cellX = minCellX; cellX <= maxCellX; cellX++)
        {
            for (var cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                if (!_cells.TryGetValue((cellX, cellY), out var bucket))
                {
                    continue;
                }

                foreach (var index in bucket)
                {
                    if (index == excludeIndex)
                    {
                        continue;
                    }

                    if (BoundsOverlap(minX, minY, maxX, maxY, _entries[index]))
                    {
                        result.Add(index);
                    }
                }
            }
        }

        foreach (var index in _longSegmentIndices)
        {
            if (index == excludeIndex)
            {
                continue;
            }

            if (BoundsOverlap(minX, minY, maxX, maxY, _entries[index]))
            {
                result.Add(index);
            }
        }

        return result;
    }

    private void BuildCells()
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            var entry = _entries[index];
            var minCellX = (long)Math.Floor(entry.MinX / CellSize);
            var maxCellX = (long)Math.Floor(entry.MaxX / CellSize);
            var minCellY = (long)Math.Floor(entry.MinY / CellSize);
            var maxCellY = (long)Math.Floor(entry.MaxY / CellSize);

            var spanX = maxCellX - minCellX + 1;
            var spanY = maxCellY - minCellY + 1;
            if (spanX > MaxCellsPerAxis || spanY > MaxCellsPerAxis)
            {
                _longSegmentIndices.Add(index);
                continue;
            }

            for (var cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                for (var cellY = minCellY; cellY <= maxCellY; cellY++)
                {
                    var key = (cellX, cellY);
                    if (!_cells.TryGetValue(key, out var bucket))
                    {
                        bucket = [];
                        _cells[key] = bucket;
                    }

                    bucket.Add(index);
                }
            }
        }
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) ExpandBounds(
        PointF start,
        PointF end,
        double tolerance)
    {
        var minX = Math.Min(start.X, end.X) - tolerance;
        var minY = Math.Min(start.Y, end.Y) - tolerance;
        var maxX = Math.Max(start.X, end.X) + tolerance;
        var maxY = Math.Max(start.Y, end.Y) + tolerance;
        return (minX, minY, maxX, maxY);
    }

    private static bool BoundsOverlap(Entry a, Entry b)
        => a.MinX <= b.MaxX &&
           a.MaxX >= b.MinX &&
           a.MinY <= b.MaxY &&
           a.MaxY >= b.MinY;

    private static bool BoundsOverlap(
        double minX,
        double minY,
        double maxX,
        double maxY,
        Entry entry)
        => minX <= entry.MaxX &&
           maxX >= entry.MinX &&
           minY <= entry.MaxY &&
           maxY >= entry.MinY;
}
