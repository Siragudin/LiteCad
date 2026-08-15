using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace LiteCad.Tests;

/// <summary>
/// Audit-only trace for parallel-line AddSegment repro. Does not assert production fixes.
/// </summary>
public class ParallelLinesAddSegmentAuditTests
{
    private readonly ITestOutputHelper _output;

    public ParallelLinesAddSegmentAuditTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(1e-4)]
    [InlineData(12.0)]
    [InlineData(20.0)]
    [InlineData(24.0)]
    [InlineData(30.0)]
    public void Audit_ParallelLinesAddSegment_PipelineTrace(double tolerance)
    {
        TraceScenario(
            existingStart: new PointF(0, 0),
            existingEnd: new PointF(100, 0),
            newStart: new PointF(0, 20),
            newEnd: new PointF(120, 20),
            tolerance,
            label: "exact repro y=20");
    }

    [Fact]
    public void Audit_ParallelLinesAtSnapToleranceDistance_y12_tol12()
    {
        TraceScenario(
            existingStart: new PointF(0, 0),
            existingEnd: new PointF(100, 0),
            newStart: new PointF(0, 12),
            newEnd: new PointF(120, 12),
            tolerance: 12.0,
            label: "parallel offset=SnapToleranceWorld@zoom=1");
    }

    [Fact]
    public void Audit_ExistingEndProjectsOntoNewSegment_InteriorCheck()
    {
        var existingStart = new PointF(0, 0);
        var existingEnd = new PointF(100, 0);
        var newStart = new PointF(0, 20);
        var newEnd = new PointF(120, 20);

        var report = new StringBuilder();
        report.AppendLine("=== existingEnd projection onto new segment ===");
        for (var tolerance = 12.0; tolerance <= 30.0; tolerance += 2.0)
        {
            var onNew = Geometry2D.IsPointOnSegmentInterior(existingEnd, newStart, newEnd, tolerance);
            Geometry2D.TryProjectPointOnSegment(existingEnd, newStart, newEnd, out var proj, out var dist, tolerance);
            report.AppendLine(
                $"tol={tolerance:G}: existingEnd(100,0) on new interior={onNew}, proj=({proj.X},{proj.Y}), dist={dist:G}");
        }

        _output.WriteLine(report.ToString());
    }

    private void TraceScenario(
        PointF existingStart,
        PointF existingEnd,
        PointF newStart,
        PointF newEnd,
        double tolerance,
        string label)
    {
        var report = new StringBuilder();
        report.AppendLine($"=== AddSegment audit [{label}] (tolerance={tolerance:G}) ===");
        report.AppendLine($"Existing: ({existingStart.X},{existingStart.Y}) -> ({existingEnd.X},{existingEnd.Y})");
        report.AppendLine($"New:      ({newStart.X},{newStart.Y}) -> ({newEnd.X},{newEnd.Y})");
        report.AppendLine();

        TraceGeometryChecks(report, existingStart, existingEnd, newStart, newEnd, tolerance);
        TraceSimulatedAddSegmentPipeline(report, existingStart, existingEnd, newStart, newEnd, tolerance);

        var document = new CadDocument();
        var template = Edge.CreateStyleTemplate();
        EdgeOperations.AddSegment(document, existingStart, existingEnd, template, tolerance);

        report.AppendLine("--- After AddSegment(existing) ---");
        DumpDocument(report, document);

        EdgeOperations.AddSegment(document, newStart, newEnd, template, tolerance);

        report.AppendLine("--- After AddSegment(new) ---");
        DumpDocument(report, document);

        _output.WriteLine(report.ToString());
    }

    [Fact]
    public void Audit_SquareBottomPlusRightEdge_ParallelLineAtY20()
    {
        var document = new CadDocument();
        var template = Edge.CreateStyleTemplate();
        const double tolerance = 12.0;

        EdgeOperations.AddSegment(document, new PointF(0, 0), new PointF(100, 0), template, tolerance);
        EdgeOperations.AddSegment(document, new PointF(100, 0), new PointF(100, 100), template, tolerance);

        var report = new StringBuilder();
        report.AppendLine("=== Square L-shape + parallel line at y=20 (tol=12) ===");
        DumpDocument(report, document);

        var newStart = new PointF(0, 20);
        var newEnd = new PointF(120, 20);
        TraceGeometryChecks(report, new PointF(0, 0), new PointF(100, 0), newStart, newEnd, tolerance);
        TraceSimulatedAddSegmentPipeline(report, new PointF(0, 0), new PointF(100, 0), newStart, newEnd, tolerance);

        foreach (var edge in document.Edges.ToList())
        {
            var edgeStart = TopologyService.GetEdgeStartPoint(document, edge);
            var edgeEnd = TopologyService.GetEdgeEndPoint(document, edge);
            if (IntersectionService.TryGetSegmentIntersection(newStart, newEnd, edgeStart, edgeEnd, out var hit, tolerance))
            {
                report.AppendLine(
                    $"Intersection new x existing edge ({edgeStart.X},{edgeStart.Y})->({edgeEnd.X},{edgeEnd.Y}) at ({hit.X},{hit.Y})");
            }
        }

        EdgeOperations.AddSegment(document, newStart, newEnd, template, tolerance);
        report.AppendLine("--- After AddSegment(new) ---");
        DumpDocument(report, document);

        _output.WriteLine(report.ToString());
    }

    private static void TraceGeometryChecks(
        StringBuilder report,
        PointF existingStart,
        PointF existingEnd,
        PointF newStart,
        PointF newEnd,
        double tolerance)
    {
        report.AppendLine("--- Geometry2D pre-checks ---");

        var parallel = Geometry2D.AreDirectionsParallel(
            newEnd.X - newStart.X,
            newEnd.Y - newStart.Y,
            MathUtils.Distance(newStart, newEnd),
            existingEnd.X - existingStart.X,
            existingEnd.Y - existingStart.Y,
            MathUtils.Distance(existingStart, existingEnd),
            tolerance);
        report.AppendLine($"AreDirectionsParallel: {parallel}");

        var distExistingStart = Geometry2D.DistanceToLine(existingStart, newStart, newEnd, tolerance);
        var distExistingEnd = Geometry2D.DistanceToLine(existingEnd, newStart, newEnd, tolerance);
        report.AppendLine(
            $"DistanceToLine existingStart->new segment: {distExistingStart:G17} (<=tol ? {distExistingStart <= tolerance})");
        report.AppendLine(
            $"DistanceToLine existingEnd->new segment:   {distExistingEnd:G17} (<=tol ? {distExistingEnd <= tolerance})");

        var collinear = Geometry2D.AreSegmentsCollinear(newStart, newEnd, existingStart, existingEnd, tolerance);
        report.AppendLine($"AreSegmentsCollinear: {collinear}");

        var overlap = Geometry2D.TryGetCollinearSegmentOverlap(
            newStart,
            newEnd,
            existingStart,
            existingEnd,
            tolerance,
            out var overlapStart,
            out var overlapEnd);
        report.AppendLine(
            overlap
                ? $"TryGetCollinearSegmentOverlap: TRUE overlap=({overlapStart.X},{overlapStart.Y})->({overlapEnd.X},{overlapEnd.Y})"
                : "TryGetCollinearSegmentOverlap: FALSE");

        var intersection = IntersectionService.TryGetSegmentIntersection(
            newStart,
            newEnd,
            existingStart,
            existingEnd,
            out var intersectionPoint,
            tolerance);
        report.AppendLine(
            intersection
                ? $"TryGetSegmentIntersection: TRUE at ({intersectionPoint.X},{intersectionPoint.Y})"
                : "TryGetSegmentIntersection: FALSE");

        report.AppendLine($"IsPointOnSegmentInterior newStart on existing: {Geometry2D.IsPointOnSegmentInterior(newStart, existingStart, existingEnd, tolerance)}");
        report.AppendLine($"IsPointOnSegmentInterior newEnd on existing:   {Geometry2D.IsPointOnSegmentInterior(newEnd, existingStart, existingEnd, tolerance)}");
        report.AppendLine($"IsPointOnSegmentInterior existingEnd on new:    {Geometry2D.IsPointOnSegmentInterior(existingEnd, newStart, newEnd, tolerance)}");
        report.AppendLine($"IsPointOnSegmentInterior existingStart on new:  {Geometry2D.IsPointOnSegmentInterior(existingStart, newStart, newEnd, tolerance)}");
        report.AppendLine();
    }

    private static void TraceSimulatedAddSegmentPipeline(
        StringBuilder report,
        PointF existingStart,
        PointF existingEnd,
        PointF newStart,
        PointF newEnd,
        double tolerance)
    {
        report.AppendLine("--- Simulated AddSegment(new) pipeline ---");

        var breakpoints = new List<PointF> { newStart, newEnd };
        report.AppendLine($"Initial breakpoints: {FormatPoints(breakpoints)}");

        if (Geometry2D.TryGetCollinearSegmentOverlap(
                newStart,
                newEnd,
                existingStart,
                existingEnd,
                tolerance,
                out var overlapStart,
                out var overlapEnd))
        {
            report.AppendLine(
                $"RemoveCollinearOverlaps WOULD TrimEdgeOverlap on existing with overlap=({overlapStart.X},{overlapStart.Y})->({overlapEnd.X},{overlapEnd.Y})");

            var tStart = Geometry2D.GetSegmentParameter(overlapStart, existingStart, existingEnd, tolerance);
            var tEnd = Geometry2D.GetSegmentParameter(overlapEnd, existingStart, existingEnd, tolerance);
            report.AppendLine($"  TrimEdgeOverlap params on existing: tStart={tStart:G17}, tEnd={tEnd:G17}");
        }
        else
        {
            report.AppendLine("RemoveCollinearOverlaps: no collinear overlap (existing edge skipped)");
        }

        if (IntersectionService.TryGetSegmentIntersection(
                newStart,
                newEnd,
                existingStart,
                existingEnd,
                out var intersection,
                tolerance))
        {
            if (Geometry2D.IsPointOnSegmentInterior(intersection, newStart, newEnd, tolerance))
            {
                AddUniquePoint(breakpoints, intersection, tolerance);
                report.AppendLine($"Intersection added to breakpoints: ({intersection.X},{intersection.Y})");
            }

            if (Geometry2D.IsPointOnSegmentInterior(intersection, existingStart, existingEnd, tolerance))
            {
                report.AppendLine($"SplitEdge WOULD fire on existing at ({intersection.X},{intersection.Y})");
            }
        }

        if (Geometry2D.IsPointOnSegmentInterior(newStart, existingStart, existingEnd, tolerance))
        {
            report.AppendLine($"SplitEdge WOULD fire on existing at newStart ({newStart.X},{newStart.Y})");
        }

        if (Geometry2D.IsPointOnSegmentInterior(newEnd, existingStart, existingEnd, tolerance))
        {
            report.AppendLine($"SplitEdge WOULD fire on existing at newEnd ({newEnd.X},{newEnd.Y})");
        }

        SortPointsAlongSegment(breakpoints, newStart, newEnd, tolerance);
        report.AppendLine($"Final breakpoints after SortPointsAlongSegment: {FormatPoints(breakpoints)}");
        report.AppendLine($"Would create {Math.Max(0, breakpoints.Count - 1)} sub-segment(s) from breakpoints");
        report.AppendLine();
    }

    private static void DumpDocument(StringBuilder report, CadDocument document)
    {
        report.AppendLine($"Edges={document.Edges.Count}, Vertices={document.Vertices.Count}");
        foreach (var vertex in document.Vertices)
        {
            report.AppendLine($"  Vertex ({vertex.Position.X:G9}, {vertex.Position.Y:G9})");
        }

        foreach (var edge in document.Edges)
        {
            var start = TopologyService.GetEdgeStartPoint(document, edge);
            var end = TopologyService.GetEdgeEndPoint(document, edge);
            report.AppendLine($"  Edge ({start.X:G9},{start.Y:G9}) -> ({end.X:G9},{end.Y:G9})");
        }

        report.AppendLine();
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

    private static string FormatPoints(IReadOnlyList<PointF> points)
        => string.Join(", ", points.Select(p => $"({p.X},{p.Y})"));
}
