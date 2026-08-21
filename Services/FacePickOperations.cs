using LiteCad.Core.Document;
using LiteCad.Core.Geometry;

namespace LiteCad.Services;

public static class FacePickOperations
{
    public static bool TryPickFacePolygonAt(
        CadDocument document,
        PointF world,
        double tolerance,
        out Polygon face)
    {
        face = null!;

        Polygon? closestFace = null;
        var closestArea = double.MaxValue;

        foreach (var polygon in document.Polygons)
        {
            if (polygon.Type != PolygonType.Face
                || polygon.OuterLoop.Edges.Count < 3
                || !PolygonGeometry.ContainsPoint(document, polygon, world, tolerance))
            {
                continue;
            }

            var area = PolygonGeometry.GetArea(document, polygon, tolerance);
            if (area >= closestArea)
            {
                continue;
            }

            closestFace = polygon;
            closestArea = area;
        }

        if (closestFace is null)
        {
            return false;
        }

        face = closestFace;
        return true;
    }

    public static bool TryPickFaceAt(
        CadDocument document,
        PointF world,
        double tolerance,
        out Polygon face)
    {
        face = null!;

        Polygon? closestFace = null;
        var closestArea = double.MaxValue;

        foreach (var polygon in document.Polygons)
        {
            if (!OffsetOperations.CanOffsetFace(polygon)
                || !PolygonGeometry.ContainsPoint(document, polygon, world, tolerance))
            {
                continue;
            }

            var area = PolygonGeometry.GetArea(document, polygon, tolerance);
            if (area >= closestArea)
            {
                continue;
            }

            closestFace = polygon;
            closestArea = area;
        }

        if (closestFace is null)
        {
            return false;
        }

        face = closestFace;
        return true;
    }
}
