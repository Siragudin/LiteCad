using LiteCad.Core.Document;
using LiteCad.Core.Geometry;

namespace LiteCad.Tests;

internal static class TestDocumentHelpers
{
    public static Edge AddEdge(
        CadDocument document,
        PointF start,
        PointF end,
        double tolerance = MathUtils.DefaultTolerance)
        => TopologyService.CreateEdgeFromPoints(document, start, end, CreateTemplate(), tolerance);

    public static Edge CreateTemplate()
        => Edge.CreateStyleTemplate();
}
