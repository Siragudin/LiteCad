using LiteCad.Core.Geometry;

namespace LiteCad.Dimensions;

public sealed class Dimension
{
    public Dimension(
        Guid id,
        Guid firstVertexId,
        Guid secondVertexId,
        PointF firstAnchorPosition,
        PointF secondAnchorPosition,
        double offset,
        DimensionExtensionStyle extensionStyle = DimensionExtensionStyle.Full,
        bool isOrthogonal = false,
        bool orthogonalIsHorizontal = false)
    {
        Id = id;
        FirstVertexId = firstVertexId;
        SecondVertexId = secondVertexId;
        FirstAnchorPosition = firstAnchorPosition;
        SecondAnchorPosition = secondAnchorPosition;
        Offset = offset;
        ExtensionStyle = extensionStyle;
        IsOrthogonal = isOrthogonal;
        OrthogonalIsHorizontal = orthogonalIsHorizontal;
    }

    public Guid Id { get; }

    public Guid FirstVertexId { get; }

    public Guid SecondVertexId { get; }

    public PointF FirstAnchorPosition { get; }

    public PointF SecondAnchorPosition { get; }

    public double Offset { get; set; }

    public DimensionExtensionStyle ExtensionStyle { get; set; }

    public bool IsOrthogonal { get; set; }

    public bool OrthogonalIsHorizontal { get; set; }

    public Dimension Clone()
        => new(
            Id,
            FirstVertexId,
            SecondVertexId,
            FirstAnchorPosition,
            SecondAnchorPosition,
            Offset,
            ExtensionStyle,
            IsOrthogonal,
            OrthogonalIsHorizontal);
}
