using LiteCad.Core.Document;



namespace LiteCad.Core.Geometry;



public static class EdgeOperations

{

    private const double ParameterTolerance = 1e-6;



    public static void SplitEdgeAt(CadDocument document, Edge edge, PointF point, double tolerance)

        => TopologyService.SplitEdge(document, edge, point, tolerance);



    public static void AddSegment(CadDocument document, PointF start, PointF end, Edge template, double tolerance)

    {

        _ = tolerance;

        tolerance = TopologyTolerance.ForMutation;

        if (MathUtils.Distance(start, end) <= tolerance)

        {

            return;

        }



        if (TryReplaceCoincidentEdge(document, start, end, template, tolerance))

        {

            return;

        }



        RemoveCollinearOverlaps(document, start, end, tolerance);



        var breakpoints = new List<PointF> { start, end };



        foreach (var edge in document.Edges.ToList())

        {

            var edgeStart = TopologyService.GetEdgeStartPoint(document, edge);

            var edgeEnd = TopologyService.GetEdgeEndPoint(document, edge);



            if (IntersectionService.TryGetSegmentIntersection(start, end, edgeStart, edgeEnd, out var intersection, tolerance))

            {

                if (Geometry2D.IsPointOnSegmentInterior(intersection, start, end, tolerance))

                {

                    AddUniquePoint(breakpoints, intersection, tolerance);

                }



                if (Geometry2D.IsPointOnSegmentInterior(intersection, edgeStart, edgeEnd, tolerance))

                {

                    TrySplitEdge(document, edge, intersection, tolerance);

                }

            }



            if (Geometry2D.IsPointOnSegmentInterior(start, edgeStart, edgeEnd, tolerance))

            {

                TrySplitEdge(document, edge, start, tolerance);

            }



            if (Geometry2D.IsPointOnSegmentInterior(end, edgeStart, edgeEnd, tolerance))

            {

                TrySplitEdge(document, edge, end, tolerance);

            }

        }



        SortPointsAlongSegment(breakpoints, start, end, tolerance);



        for (var i = 0; i < breakpoints.Count - 1; i++)

        {

            var segmentStart = breakpoints[i];

            var segmentEnd = breakpoints[i + 1];

            if (MathUtils.Distance(segmentStart, segmentEnd) <= tolerance)

            {

                continue;

            }



            if (HasCoincidentEdge(document, segmentStart, segmentEnd, tolerance))

            {

                continue;

            }



            TopologyService.CreateEdgeFromPoints(document, segmentStart, segmentEnd, template, tolerance);

        }

    }



    private static void TrySplitEdge(CadDocument document, Edge edge, PointF point, double tolerance)
    {
        if (!document.Edges.Contains(edge))
        {
            return;
        }

        TopologyService.SplitEdge(document, edge, point, tolerance);
    }



    private static bool TryReplaceCoincidentEdge(

        CadDocument document,

        PointF start,

        PointF end,

        Edge template,

        double tolerance)

    {

        foreach (var edge in document.Edges.ToList())

        {

            var edgeStart = TopologyService.GetEdgeStartPoint(document, edge);

            var edgeEnd = TopologyService.GetEdgeEndPoint(document, edge);

            if (!IsSameSegment(start, end, edgeStart, edgeEnd, tolerance))

            {

                continue;

            }



            ApplyTemplate(edge, template);

            var startVertexId = TopologyService.FindOrCreateVertex(document, start, tolerance);

            var endVertexId = TopologyService.FindOrCreateVertex(document, end, tolerance);

            edge.StartVertexId = startVertexId;

            edge.EndVertexId = endVertexId;

            edge.OrthoAlignment = template.OrthoAlignment;

            return true;

        }



        return false;

    }



    private static void RemoveCollinearOverlaps(

        CadDocument document,

        PointF start,

        PointF end,

        double tolerance)

    {

        foreach (var edge in document.Edges.ToList())

        {

            var edgeStart = TopologyService.GetEdgeStartPoint(document, edge);

            var edgeEnd = TopologyService.GetEdgeEndPoint(document, edge);



            if (!Geometry2D.TryGetCollinearSegmentOverlap(

                    start,

                    end,

                    edgeStart,

                    edgeEnd,

                    tolerance,

                    out var overlapStart,

                    out var overlapEnd))

            {

                continue;

            }



            if (IsSameSegment(start, end, edgeStart, edgeEnd, tolerance))

            {

                continue;

            }



            TrimEdgeOverlap(document, edge, overlapStart, overlapEnd, tolerance);

        }

    }



    private static void TrimEdgeOverlap(

        CadDocument document,

        Edge edge,

        PointF overlapStart,

        PointF overlapEnd,

        double tolerance)

    {

        var edgeStart = TopologyService.GetEdgeStartPoint(document, edge);

        var edgeEnd = TopologyService.GetEdgeEndPoint(document, edge);

        var template = edge.CloneGeometry();

        var startVertexId = edge.StartVertexId;

        var endVertexId = edge.EndVertexId;



        TopologyService.DeleteEdge(document, edge);



        var tStart = Geometry2D.GetSegmentParameter(overlapStart, edgeStart, edgeEnd, tolerance);

        var tEnd = Geometry2D.GetSegmentParameter(overlapEnd, edgeStart, edgeEnd, tolerance);

        if (tStart > tEnd)

        {

            (tStart, tEnd) = (tEnd, tStart);

        }



        if (tStart > ParameterTolerance)

        {

            var beforeEndVertexId = TopologyService.FindOrCreateVertex(document, overlapStart, tolerance);

            if (MathUtils.Distance(edgeStart, overlapStart) > tolerance)

            {

                TopologyService.CreateEdge(document, startVertexId, beforeEndVertexId, template);

            }

        }



        if (tEnd < 1 - ParameterTolerance)

        {

            var afterStartVertexId = TopologyService.FindOrCreateVertex(document, overlapEnd, tolerance);

            if (MathUtils.Distance(overlapEnd, edgeEnd) > tolerance)

            {

                TopologyService.CreateEdge(document, afterStartVertexId, endVertexId, template);

            }

        }

    }



    private static bool HasCoincidentEdge(

        CadDocument document,

        PointF start,

        PointF end,

        double tolerance)

    {

        foreach (var edge in document.Edges)

        {

            var edgeStart = TopologyService.GetEdgeStartPoint(document, edge);

            var edgeEnd = TopologyService.GetEdgeEndPoint(document, edge);

            if (IsSameSegment(start, end, edgeStart, edgeEnd, tolerance))

            {

                return true;

            }

        }



        return false;

    }



    private static bool IsSameSegment(

        PointF aStart,

        PointF aEnd,

        PointF bStart,

        PointF bEnd,

        double tolerance)

    {

        return (Geometry2D.ArePointsSame(aStart, bStart, tolerance) &&

                Geometry2D.ArePointsSame(aEnd, bEnd, tolerance)) ||

               (Geometry2D.ArePointsSame(aStart, bEnd, tolerance) &&

                Geometry2D.ArePointsSame(aEnd, bStart, tolerance));

    }



    private static void ApplyTemplate(Edge edge, Edge template)

    {

        edge.Color = template.Color;

        edge.Thickness = template.Thickness;

        edge.LineType = template.LineType;

        edge.IsAxis = template.IsAxis;

        edge.OrthoAlignment = template.OrthoAlignment;

    }



    private static void AddUniquePoint(List<PointF> points, PointF point, double tolerance)

    {

        foreach (var existing in points)

        {

            if (Geometry2D.ArePointsSame(existing, point, tolerance))

            {

                return;

            }

        }



        points.Add(point);

    }



    private static void SortPointsAlongSegment(List<PointF> points, PointF start, PointF end, double tolerance)

    {

        var dx = end.X - start.X;

        var dy = end.Y - start.Y;

        points.Sort((a, b) =>

        {

            var ta = Math.Abs(dx) >= Math.Abs(dy)

                ? (a.X - start.X) / (dx == 0 ? tolerance : dx)

                : (a.Y - start.Y) / (dy == 0 ? tolerance : dy);

            var tb = Math.Abs(dx) >= Math.Abs(dy)

                ? (b.X - start.X) / (dx == 0 ? tolerance : dx)

                : (b.Y - start.Y) / (dy == 0 ? tolerance : dy);

            return ta.CompareTo(tb);

        });

    }

}

