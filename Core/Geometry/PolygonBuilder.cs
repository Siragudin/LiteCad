using LiteCad.Core.Document;



namespace LiteCad.Core.Geometry;



public static class PolygonBuilder

{

    private const int MaxFaceVertices = 10_000;

    private const double MinFaceArea = 1e-10;



    public static void SyncFaces(CadDocument document, double tolerance)

    {

        document.Polygons.RemoveAll(polygon => polygon.Type == PolygonType.Face);



        if (document.Edges.Count == 0)

        {

            document.SuppressedFaceGeometryKeys.Clear();

            document.Vertices.Clear();

            return;

        }



        RebuildFaces(document, tolerance);

    }



    private static void RebuildFaces(CadDocument document, double tolerance)

    {

        tolerance = TopologyTolerance.ForMutation;

        var graph = BuildGraph(document);

        if (graph.Adjacency.Count == 0)

        {

            return;

        }



        graph.SortOutgoingByAngle(document);



        var boundedContours = EnumerateBoundedContours(document, graph, tolerance).ToList();

        _ = BuildContainmentRelations(document, boundedContours, tolerance);

        var createdIdentityKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var contour in boundedContours)
        {
            var outerLoop = CreateLoopFromDirectedEdges(contour.DirectedEdges);
            var faceIdentity = FaceIdentity.CreateLoop(document, outerLoop);

            if (document.SuppressedFaceGeometryKeys.Contains(faceIdentity))
            {
                continue;
            }

            if (!createdIdentityKeys.Add(faceIdentity))
            {
                continue;
            }

            var polygon = new Polygon { Type = PolygonType.Face };
            polygon.OuterLoop.Edges.AddRange(contour.DirectedEdges);
            document.Polygons.Add(polygon);
        }
    }

    public static IReadOnlyDictionary<string, string?> BuildContainmentParentMap(CadDocument document, double tolerance)
    {
        tolerance = TopologyTolerance.ForMutation;
        var graph = BuildGraph(document);
        if (graph.Adjacency.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        graph.SortOutgoingByAngle(document);
        var boundedContours = EnumerateBoundedContours(document, graph, tolerance).ToList();
        var relations = BuildContainmentRelations(document, boundedContours, tolerance);
        var parentMap = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var (childIdentity, parentIdentity) in relations)
        {
            parentMap[childIdentity] = parentIdentity;
        }

        return parentMap;
    }

    private static List<(string ChildIdentity, string? ParentIdentity)> BuildContainmentRelations(
        CadDocument document,
        IReadOnlyList<BoundedContour> boundedContours,
        double tolerance)
    {
        var parentByContour = new Dictionary<BoundedContour, BoundedContour?>();
        var relations = new List<(string ChildIdentity, string? ParentIdentity)>();

        foreach (var contour in boundedContours)
        {
            parentByContour[contour] = FindImmediateParent(contour, boundedContours, tolerance);
        }

        foreach (var contour in boundedContours)
        {
            var childIdentity = FaceIdentity.CreateLoop(
                document,
                CreateLoopFromDirectedEdges(contour.DirectedEdges));
            string? parentIdentity = null;

            if (parentByContour.TryGetValue(contour, out var parent) && parent is not null)
            {
                parentIdentity = FaceIdentity.CreateLoop(
                    document,
                    CreateLoopFromDirectedEdges(parent.DirectedEdges));
            }

            relations.Add((childIdentity, parentIdentity));
        }

        return relations;
    }



    private static BoundedContour? FindImmediateParent(

        BoundedContour contour,

        IReadOnlyList<BoundedContour> contours,

        double tolerance)

    {

        BoundedContour? parent = null;

        var parentArea = double.MaxValue;



        foreach (var candidate in contours)

        {

            if (ReferenceEquals(candidate, contour))

            {

                continue;

            }



            if (Math.Abs(candidate.SignedArea) <= Math.Abs(contour.SignedArea))

            {

                continue;

            }



            if (!IsContourInsideContour(contour, candidate, tolerance))

            {

                continue;

            }



            var candidateArea = Math.Abs(candidate.SignedArea);

            if (candidateArea < parentArea)

            {

                parent = candidate;

                parentArea = candidateArea;

            }

        }



        return parent;

    }



    private static IEnumerable<BoundedContour> EnumerateBoundedContours(

        CadDocument document,

        Graph graph,

        double tolerance)

    {

        var visited = new HashSet<(Guid From, Guid To, Guid EdgeId)>();



        foreach (var from in graph.Adjacency.Keys.ToList())

        {

            foreach (var neighbor in graph.GetNeighbors(from))

            {

                var start = (from, neighbor.VertexId, neighbor.EdgeId);

                if (visited.Contains(start))

                {

                    continue;

                }



                var contour = WalkBoundedContour(document, graph, from, neighbor, visited);

                if (contour is null || contour.EdgeIds.Count < 3)

                {

                    continue;

                }



                contour.SignedArea = MathUtils.SignedPolygonArea(contour.Vertices);

                if (contour.SignedArea <= MinFaceArea)

                {

                    continue;

                }



                yield return contour;

            }

        }

    }



    private static BoundedContour? WalkBoundedContour(

        CadDocument document,

        Graph graph,

        Guid startFrom,

        (Guid VertexId, Guid EdgeId) startEdge,

        HashSet<(Guid From, Guid To, Guid EdgeId)> visited)

    {

        var contour = new BoundedContour();

        var vertices = new List<PointF>();

        var walkKeys = new List<(Guid From, Guid To, Guid EdgeId)>();



        var from = startFrom;

        var to = startEdge.VertexId;

        var edgeId = startEdge.EdgeId;



        while (true)

        {

            var key = (from, to, edgeId);

            if (visited.Contains(key) || walkKeys.Contains(key))

            {

                return null;

            }



            walkKeys.Add(key);

            contour.EdgeIds.Add(edgeId);

            vertices.Add(TopologyService.GetVertexPosition(document, from));



            if (!TryGetDirectedReference(document, edgeId, from, to, out var directedEdge))

            {

                return null;

            }



            contour.DirectedEdges.Add(directedEdge);



            if (vertices.Count > MaxFaceVertices)

            {

                return null;

            }



            var next = graph.NextFaceEdge(to, from, edgeId);

            if (next is null)

            {

                return null;

            }



            from = to;

            to = next.Value.VertexId;

            edgeId = next.Value.EdgeId;



            if (from == startFrom && to == startEdge.VertexId && edgeId == startEdge.EdgeId)

            {

                foreach (var walkKey in walkKeys)

                {

                    visited.Add(walkKey);

                }



                contour.Vertices.AddRange(vertices);

                return contour;

            }

        }

    }



    private static bool TryGetDirectedReference(

        CadDocument document,

        Guid edgeId,

        Guid fromVertexId,

        Guid toVertexId,

        out DirectedEdgeReference reference)

    {

        var edge = document.Edges.FirstOrDefault(item => item.Id == edgeId);

        if (edge is null)

        {

            reference = default;

            return false;

        }



        if (edge.StartVertexId == fromVertexId && edge.EndVertexId == toVertexId)

        {

            reference = new DirectedEdgeReference(edgeId, forward: true);

            return true;

        }



        if (edge.EndVertexId == fromVertexId && edge.StartVertexId == toVertexId)

        {

            reference = new DirectedEdgeReference(edgeId, forward: false);

            return true;

        }



        reference = default;

        return false;

    }



    private static bool IsContourInsideContour(BoundedContour inner, BoundedContour outer, double tolerance)

    {

        if (inner.EdgeIds.Any(outer.EdgeIds.Contains))

        {

            return false;

        }



        foreach (var vertex in inner.Vertices)

        {

            if (!IsPointStrictlyInsideContour(vertex, outer.Vertices, tolerance))

            {

                return false;

            }

        }



        return true;

    }



    private static bool IsPointStrictlyInsideContour(

        PointF point,

        IReadOnlyList<PointF> contour,

        double tolerance)

    {

        if (!MathUtils.PointInPolygon(point, contour))

        {

            return false;

        }



        foreach (var vertex in contour)

        {

            if (Geometry2D.ArePointsSame(point, vertex, tolerance))

            {

                return false;

            }

        }



        for (var i = 0; i < contour.Count; i++)

        {

            var start = contour[i];

            var end = contour[(i + 1) % contour.Count];

            if (Geometry2D.TryProjectPointOnSegment(point, start, end, out _, out var distance, tolerance) &&

                distance <= tolerance)

            {

                return false;

            }

        }



        return true;

    }



    public static string CreateFaceKey(IReadOnlyList<Guid> edgeIds)

    {

        var ordered = edgeIds

            .Select(id => id.ToString())

            .OrderBy(id => id, StringComparer.Ordinal)

            .ToArray();

        return string.Join("|", ordered);

    }



    private static Loop CreateLoopFromDirectedEdges(IReadOnlyList<DirectedEdgeReference> directedEdges)
    {
        var loop = new Loop();
        loop.Edges.AddRange(directedEdges);
        return loop;
    }

    public static void SuppressFaceGeometry(CadDocument document, Polygon polygon, double tolerance)
    {
        _ = tolerance;

        document.SuppressedFaceGeometryKeys.Add(FaceIdentity.Create(document, polygon));
    }



    public static void InvalidateSuppressionOnEdgeTopologyChange(CadDocument document)

    {

        document.SuppressedFaceGeometryKeys.Clear();

    }



    private static Graph BuildGraph(CadDocument document)

    {

        var graph = new Graph();



        foreach (var edge in document.Edges)

        {

            if (edge.StartVertexId == edge.EndVertexId)

            {

                continue;

            }



            graph.AddEdge(edge.StartVertexId, edge.EndVertexId, edge.Id);

        }



        return graph;

    }



    private sealed class BoundedContour

    {

        public List<Guid> EdgeIds { get; } = [];



        public List<DirectedEdgeReference> DirectedEdges { get; } = [];



        public List<PointF> Vertices { get; } = [];



        public double SignedArea { get; set; }

    }



    private sealed class Graph

    {

        public Dictionary<Guid, List<(Guid VertexId, Guid EdgeId)>> Adjacency { get; } = new();



        public void AddEdge(Guid from, Guid to, Guid edgeId)

        {

            EnsureVertex(from);

            EnsureVertex(to);

            Adjacency[from].Add((to, edgeId));

            Adjacency[to].Add((from, edgeId));

        }



        private void EnsureVertex(Guid vertexId)

        {

            if (!Adjacency.ContainsKey(vertexId))

            {

                Adjacency[vertexId] = [];

            }

        }



        public IEnumerable<(Guid VertexId, Guid EdgeId)> GetNeighbors(Guid vertexId)

            => Adjacency.TryGetValue(vertexId, out var neighbors) ? neighbors : [];



        public void SortOutgoingByAngle(CadDocument document)

        {

            foreach (var (originId, neighbors) in Adjacency)

            {

                var origin = TopologyService.GetVertexPosition(document, originId);

                neighbors.Sort((a, b) =>

                {

                    var aPos = TopologyService.GetVertexPosition(document, a.VertexId);

                    var bPos = TopologyService.GetVertexPosition(document, b.VertexId);

                    var angleA = Math.Atan2(aPos.Y - origin.Y, aPos.X - origin.X);

                    var angleB = Math.Atan2(bPos.Y - origin.Y, bPos.X - origin.X);

                    var comparison = angleA.CompareTo(angleB);

                    return comparison != 0 ? comparison : a.EdgeId.CompareTo(b.EdgeId);

                });

            }

        }



        public (Guid VertexId, Guid EdgeId)? NextFaceEdge(Guid vertexId, Guid incomingFrom, Guid incomingEdgeId)

        {

            if (!Adjacency.TryGetValue(vertexId, out var neighbors) || neighbors.Count == 0)

            {

                return null;

            }



            var index = -1;

            for (var i = 0; i < neighbors.Count; i++)

            {

                if (neighbors[i].VertexId == incomingFrom && neighbors[i].EdgeId == incomingEdgeId)

                {

                    index = i;

                    break;

                }

            }



            if (index < 0)

            {

                return null;

            }



            var previous = (index - 1 + neighbors.Count) % neighbors.Count;

            return neighbors[previous];

        }

    }

}

