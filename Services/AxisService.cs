using LiteCad.Core.Document;
using LiteCad.Core.Geometry;

namespace LiteCad.Services;

public static class AxisService
{
    public static Axis? Create(CadDocument document, PointF start, PointF end, double tolerance)
    {
        tolerance = TopologyTolerance.Resolve(tolerance);
        if (MathUtils.Distance(start, end) <= tolerance)
        {
            return null;
        }

        var mergedStart = start;
        var mergedEnd = end;
        var axesToRemove = new HashSet<Guid>();
        var changed = true;

        while (changed)
        {
            changed = false;

            foreach (var existing in document.Axes)
            {
                if (axesToRemove.Contains(existing.Id))
                {
                    continue;
                }

                if (!Geometry2D.TryGetCollinearSegmentsUnion(
                        mergedStart,
                        mergedEnd,
                        existing.Start,
                        existing.End,
                        tolerance,
                        out var unionStart,
                        out var unionEnd))
                {
                    continue;
                }

                mergedStart = unionStart;
                mergedEnd = unionEnd;
                axesToRemove.Add(existing.Id);
                changed = true;
            }
        }

        if (axesToRemove.Count > 0)
        {
            document.Axes.RemoveAll(axis => axesToRemove.Contains(axis.Id));
        }

        if (MathUtils.Distance(mergedStart, mergedEnd) <= tolerance)
        {
            return null;
        }

        var axis = new Axis(mergedStart, mergedEnd);
        document.Axes.Add(axis);
        return axis;
    }

    public static bool Delete(CadDocument document, Guid axisId)
        => document.Axes.RemoveAll(axis => axis.Id == axisId) > 0;

    public static Axis? Find(CadDocument document, Guid axisId)
        => document.Axes.FirstOrDefault(axis => axis.Id == axisId);
}
