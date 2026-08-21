using LiteCad.Core.Document;

namespace LiteCad.Core.Geometry;

public readonly struct SnapIdentity : IEquatable<SnapIdentity>
{
    private SnapIdentity(SnapKind kind, string key)
    {
        Kind = kind;
        Key = key;
    }

    public SnapKind Kind { get; }

    public string Key { get; }

    public static SnapIdentity ForVertex(Guid vertexId)
        => new(SnapKind.Endpoint, vertexId.ToString("N"));

    public static SnapIdentity ForMidpoint(Guid edgeId)
        => new(SnapKind.Midpoint, edgeId.ToString("N"));

    public static SnapIdentity ForAxisMidpoint(Guid axisId)
        => new(SnapKind.Midpoint, $"axis:{axisId:N}");

    public static SnapIdentity ForAxisEndpoint(Guid axisId, bool isStart)
        => new(SnapKind.Endpoint, $"axis-end:{axisId:N}:{(isStart ? 's' : 'e')}");

    public static SnapIdentity ForOnAxis(Guid axisId, long parameterKey)
        => new(SnapKind.OnEdge, $"axis-on:{axisId:N}:{parameterKey}");

    public static SnapIdentity ForAxisIntersection(IEnumerable<Guid> axisIds)
    {
        var normalized = axisIds
            .Select(id => id.ToString("N"))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        return new SnapIdentity(SnapKind.AxisIntersection, string.Join("|", normalized));
    }

    public static SnapIdentity ForIntersection(IEnumerable<Guid> edgeIds)
    {
        var normalized = edgeIds
            .Select(id => id.ToString("N"))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        return new SnapIdentity(SnapKind.Intersection, string.Join("|", normalized));
    }

    public static SnapIdentity ForOnEdge(Guid edgeId, long parameterKey)
        => new(SnapKind.OnEdge, $"{edgeId:N}:{parameterKey}");

    public static SnapIdentity ForAlignment(string alignmentKey)
        => new(SnapKind.Alignment, alignmentKey);

    public static SnapIdentity FromSnap(SnapPoint snap)
        => snap.Kind switch
        {
            SnapKind.Endpoint when snap.VertexId.HasValue => ForVertex(snap.VertexId.Value),
            SnapKind.Midpoint when snap.EdgeId.HasValue => ForMidpoint(snap.EdgeId.Value),
            SnapKind.OnEdge when snap.EdgeId.HasValue => ForOnEdge(snap.EdgeId.Value, 0),
            SnapKind.Alignment => ForAlignment("alignment"),
            SnapKind.AxisIntersection => ForAxisIntersection([]),
            _ => new SnapIdentity(snap.Kind, snap.Position.ToString() ?? string.Empty)
        };

    public bool Equals(SnapIdentity other)
        => Kind == other.Kind && string.Equals(Key, other.Key, StringComparison.Ordinal);

    public override bool Equals(object? obj)
        => obj is SnapIdentity other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Kind, Key);

    public static bool operator ==(SnapIdentity left, SnapIdentity right)
        => left.Equals(right);

    public static bool operator !=(SnapIdentity left, SnapIdentity right)
        => !left.Equals(right);
}
