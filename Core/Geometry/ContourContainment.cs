using LiteCad.Core.Document;

namespace LiteCad.Core.Geometry;

/// <summary>
/// Derived containment relationships between bounded contours.
/// Does not affect Face entity creation.
/// </summary>
public static class ContourContainment
{
    public static IReadOnlyDictionary<string, string?> BuildParentMap(CadDocument document, double tolerance)
        => PolygonBuilder.BuildContainmentParentMap(document, tolerance);
}
