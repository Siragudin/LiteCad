using LiteCad.Core.Document;
using LiteCad.Core.Geometry;

namespace LiteCad.Texts;

public static class TextPickOperations
{
    public static bool TryPickAt(
        CadDocument document,
        PointF world,
        double tolerance,
        out Guid textId,
        double zoom = 1.0)
    {
        textId = Guid.Empty;
        TextNote? closest = null;
        var closestDistance = Math.Max(tolerance, 8.0 / Math.Max(zoom, 1e-6));

        foreach (var note in document.Texts)
        {
            if (!TryGetPickDistance(world, note, zoom, out var distance) || distance > closestDistance)
            {
                continue;
            }

            closest = note;
            closestDistance = distance;
        }

        if (closest is null)
        {
            return false;
        }

        textId = closest.Id;
        return true;
    }

    private static bool TryGetPickDistance(PointF world, TextNote note, double zoom, out double distance)
    {
        distance = double.MaxValue;
        var found = false;
        var layout = TextGeometry.CreateLayout(note, zoom);
        var pickTolerance = Math.Max(1e-6, 4.0 / Math.Max(zoom, 1e-6));

        foreach (var (start, end) in TextGeometry.GetSegments(layout))
        {
            if (!Geometry2D.TryHitTestSegment(world, start, end, pickTolerance, out var segmentDistance))
            {
                continue;
            }

            found = true;
            if (segmentDistance < distance)
            {
                distance = segmentDistance;
            }
        }

        var scale = 1.0 / Math.Max(zoom, 1e-6);
        var height = layout.TextScreenSize.Height * scale + TextGeometry.TextGapAboveLineScreen * scale;
        var width = Math.Max(layout.TextScreenSize.Width * scale, 8.0 * scale);
        if (world.X >= note.Origin.X - pickTolerance
            && world.X <= note.Origin.X + width + pickTolerance
            && world.Y >= note.Origin.Y - pickTolerance
            && world.Y <= note.Origin.Y + height + pickTolerance)
        {
            found = true;
            distance = 0;
        }

        return found;
    }
}
