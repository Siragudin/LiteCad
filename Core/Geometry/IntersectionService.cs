using LiteCad.Core.Document;

namespace LiteCad.Core.Geometry;

/// <summary>
/// Pure geometry intersection queries. Does not mutate the document.
/// </summary>
public static class IntersectionService
{
    public static bool TryGetSegmentIntersection(
        CadDocument document,
        Edge edgeA,
        Edge edgeB,
        out PointF intersection,
        double tolerance = MathUtils.DefaultTolerance)
    {
        var aStart = TopologyService.GetEdgeStartPoint(document, edgeA);
        var aEnd = TopologyService.GetEdgeEndPoint(document, edgeA);
        var bStart = TopologyService.GetEdgeStartPoint(document, edgeB);
        var bEnd = TopologyService.GetEdgeEndPoint(document, edgeB);
        return Geometry2D.TryGetSegmentIntersection(aStart, aEnd, bStart, bEnd, out intersection, tolerance);
    }

    public static bool TryGetSegmentIntersection(
        PointF aStart,
        PointF aEnd,
        PointF bStart,
        PointF bEnd,
        out PointF intersection,
        double tolerance = MathUtils.DefaultTolerance)
        => Geometry2D.TryGetSegmentIntersection(aStart, aEnd, bStart, bEnd, out intersection, tolerance);
}
