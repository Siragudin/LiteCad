using LiteCad.Core.Document;

namespace LiteCad.Core.Geometry;

public static class FaceIdentity
{
    public static string Create(CadDocument document, Polygon polygon)
        => Create(document, polygon.OuterLoop, polygon.InnerLoops);

    public static string Create(
        CadDocument document,
        Loop outerLoop,
        IEnumerable<Loop> innerLoops)
    {
        var outer = CreateLoop(document, outerLoop);
        var holes = innerLoops
            .Select(hole => CreateLoop(document, hole))
            .Where(key => key.Length > 0)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        return holes.Length == 0
            ? outer
            : $"{outer}||{string.Join("|", holes)}";
    }

    public static string CreateLoop(CadDocument document, Loop loop)
    {
        var forward = GetStartVertexSequence(document, loop);
        if (forward.Count == 0)
        {
            return string.Empty;
        }

        var reverse = GetStartVertexSequence(document, ReverseLoop(loop));
        var forwardKey = CanonicalizeCyclicSequence(forward);
        var reverseKey = CanonicalizeCyclicSequence(reverse);

        return string.CompareOrdinal(forwardKey, reverseKey) <= 0
            ? forwardKey
            : reverseKey;
    }

    private static List<Guid> GetStartVertexSequence(CadDocument document, Loop loop)
    {
        var edgeMap = document.Edges.ToDictionary(edge => edge.Id);
        var vertices = new List<Guid>(loop.Edges.Count);

        foreach (var reference in loop.Edges)
        {
            if (!edgeMap.TryGetValue(reference.EdgeId, out var edge))
            {
                return [];
            }

            vertices.Add(reference.Forward ? edge.StartVertexId : edge.EndVertexId);
        }

        return vertices;
    }

    private static Loop ReverseLoop(Loop loop)
    {
        var reversed = new Loop();
        for (var i = loop.Edges.Count - 1; i >= 0; i--)
        {
            var reference = loop.Edges[i];
            reversed.Edges.Add(new DirectedEdgeReference(reference.EdgeId, !reference.Forward));
        }

        return reversed;
    }

    private static string CanonicalizeCyclicSequence(IReadOnlyList<Guid> sequence)
    {
        if (sequence.Count == 0)
        {
            return string.Empty;
        }

        string? best = null;
        for (var offset = 0; offset < sequence.Count; offset++)
        {
            var parts = new string[sequence.Count];
            for (var i = 0; i < sequence.Count; i++)
            {
                parts[i] = sequence[(offset + i) % sequence.Count].ToString("N");
            }

            var candidate = string.Join(",", parts);
            if (best is null || string.CompareOrdinal(candidate, best) < 0)
            {
                best = candidate;
            }
        }

        return best ?? string.Empty;
    }
}
