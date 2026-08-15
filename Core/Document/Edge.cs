using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using System.Windows.Media;

namespace LiteCad.Core.Document;

public sealed class Edge
{
    public Guid Id { get; }

    public Guid StartVertexId { get; set; }

    public Guid EndVertexId { get; set; }

    public bool IsAxis { get; set; }

    public Color Color { get; set; } = Color.FromRgb(0x22, 0x22, 0x22);

    public double Thickness { get; set; } = 1.5;

    public EdgeLineType LineType { get; set; } = EdgeLineType.Solid;

    public OrthoAlignment OrthoAlignment { get; set; } = OrthoAlignment.None;

    public Edge(Guid startVertexId, Guid endVertexId)
        : this(Guid.NewGuid(), startVertexId, endVertexId)
    {
    }

    public Edge(Guid id, Guid startVertexId, Guid endVertexId)
    {
        Id = id;
        StartVertexId = startVertexId;
        EndVertexId = endVertexId;
    }

    public static Edge CreateStyleTemplate()
        => new(Guid.Empty, Guid.Empty);

    public PointF StartPoint(CadDocument document)
        => TopologyService.GetEdgeStartPoint(document, this);

    public PointF EndPoint(CadDocument document)
        => TopologyService.GetEdgeEndPoint(document, this);

    public Edge CloneGeometry()
    {
        return new Edge(StartVertexId, EndVertexId)
        {
            IsAxis = IsAxis,
            Color = Color,
            Thickness = Thickness,
            LineType = LineType,
            OrthoAlignment = OrthoAlignment
        };
    }
}
