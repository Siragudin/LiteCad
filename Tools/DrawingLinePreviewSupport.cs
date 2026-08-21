using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Services;

namespace LiteCad.Tools;

internal static class DrawingLinePreviewSupport
{
    internal readonly struct MouseMoveResult
    {
        public required PointF PreviewEnd { get; init; }

        public required IReadOnlyList<SnapPoint> VisibleSnaps { get; init; }
    }

    internal static MouseMoveResult UpdateFromMouseMove(
        SnapService snapService,
        CadDocument document,
        PointF world,
        double tolerance,
        bool hasStart,
        PointF startPoint,
        bool orthoEnabled,
        bool includeOnEdge)
    {
        var query = snapService.Query(document, world, tolerance, includeOnEdge: includeOnEdge);

        var visibleSnaps = new List<SnapPoint>(query.Visible);
        var previewEnd = world;

        if (hasStart)
        {
            previewEnd = snapService.ResolveDrawingSnap(
                document,
                query,
                world,
                startPoint,
                tolerance,
                orthoEnabled);

            var alignmentSnap = snapService.FindVisibleDrawingAlignmentSnap(
                document,
                startPoint,
                previewEnd,
                tolerance);
            if (alignmentSnap is not null)
            {
                visibleSnaps.Add(alignmentSnap.Value);
            }
        }

        return new MouseMoveResult
        {
            PreviewEnd = previewEnd,
            VisibleSnaps = visibleSnaps
        };
    }
}
