using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;

namespace LiteCad.Dimensions;

public static class DimensionService
{
    public static Dimension Create(
        CadDocument document,
        Guid firstVertexId,
        Guid secondVertexId,
        double offset,
        double tolerance,
        double textSize = Dimension.DefaultTextSize)
    {
        var first = document.Vertices.First(vertex => vertex.Id == firstVertexId);
        var second = document.Vertices.First(vertex => vertex.Id == secondVertexId);
        var dimension = new Dimension(
            Guid.NewGuid(),
            firstVertexId,
            secondVertexId,
            first.Position,
            second.Position,
            offset,
            textSize: textSize);

        if (!DimensionGeometry.TryCreateLayout(first.Position, second.Position, offset, tolerance, out _))
        {
            throw new InvalidOperationException("Cannot create dimension layout for the selected vertices.");
        }

        document.Dimensions.Add(dimension);
        document.NotifyChanged(DocumentChangeKind.Annotations);
        return dimension;
    }

    public static bool TrySetOffset(CadDocument document, Guid dimensionId, double offset, double tolerance)
    {
        var dimension = document.Dimensions.FirstOrDefault(item => item.Id == dimensionId);
        if (dimension is null)
        {
            return false;
        }

        if (!DimensionGeometry.TryGetAnchorPoints(document, dimension, out var firstAnchor, out var secondAnchor)
            || !DimensionGeometry.TryCreateLayout(
                firstAnchor,
                secondAnchor,
                dimension,
                tolerance,
                out _))
        {
            return false;
        }

        dimension.Offset = offset;
        document.NotifyChanged(DocumentChangeKind.Annotations);
        return true;
    }

    public static bool TrySetExtensionStyle(
        CadDocument document,
        Guid dimensionId,
        DimensionExtensionStyle extensionStyle)
    {
        var dimension = document.Dimensions.FirstOrDefault(item => item.Id == dimensionId);
        if (dimension is null)
        {
            return false;
        }

        dimension.ExtensionStyle = extensionStyle;
        document.NotifyChanged(DocumentChangeKind.Annotations);
        return true;
    }

    public static bool TrySetTextSize(CadDocument document, Guid dimensionId, double textSize)
    {
        var dimension = document.Dimensions.FirstOrDefault(item => item.Id == dimensionId);
        if (dimension is null)
        {
            return false;
        }

        dimension.TextSize = Dimension.NormalizeTextSize(textSize);
        document.NotifyChanged(DocumentChangeKind.Annotations);
        return true;
    }

    public static bool Delete(CadDocument document, Guid dimensionId)
    {
        if (document.Dimensions.RemoveAll(item => item.Id == dimensionId) == 0)
        {
            return false;
        }

        document.NotifyChanged(DocumentChangeKind.Annotations);
        return true;
    }

    public static int DeleteSelected(CadDocument document, Selection selection)
    {
        if (selection.SelectedDimensionIds.Count == 0)
        {
            return 0;
        }

        var removed = document.Dimensions.RemoveAll(item => selection.SelectedDimensionIds.Contains(item.Id));
        selection.SelectedDimensionIds.Clear();
        if (removed > 0)
        {
            document.NotifyChanged(DocumentChangeKind.Annotations);
        }

        return removed;
    }

    public static int RemoveInvalid(CadDocument document, double tolerance)
    {
        tolerance = TopologyTolerance.ForMutation;
        var removed = 0;

        for (var index = document.Dimensions.Count - 1; index >= 0; index--)
        {
            if (IsValid(document, document.Dimensions[index], tolerance))
            {
                continue;
            }

            document.Dimensions.RemoveAt(index);
            removed++;
        }

        if (removed > 0)
        {
            document.NotifyChanged(DocumentChangeKind.Annotations);
        }

        return removed;
    }

    public static bool IsValid(CadDocument document, Dimension dimension, double tolerance)
    {
        tolerance = TopologyTolerance.ForMutation;
        var first = document.Vertices.FirstOrDefault(vertex => vertex.Id == dimension.FirstVertexId);
        if (first is null)
        {
            return false;
        }

        var second = document.Vertices.FirstOrDefault(vertex => vertex.Id == dimension.SecondVertexId);
        if (second is null)
        {
            return false;
        }

        return Geometry2D.ArePointsSame(first.Position, dimension.FirstAnchorPosition, tolerance)
            && Geometry2D.ArePointsSame(second.Position, dimension.SecondAnchorPosition, tolerance);
    }

    public static double GetMeasuredDistance(CadDocument document, Dimension dimension)
    {
        if (!DimensionGeometry.TryGetAnchorPoints(document, dimension, out var firstAnchor, out var secondAnchor))
        {
            return 0;
        }

        if (dimension.IsOrthogonal)
        {
            return dimension.OrthogonalIsHorizontal
                ? Math.Abs(secondAnchor.X - firstAnchor.X)
                : Math.Abs(secondAnchor.Y - firstAnchor.Y);
        }

        return MathUtils.Distance(firstAnchor, secondAnchor);
    }
}
