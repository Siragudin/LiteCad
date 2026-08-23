namespace LiteCad.Core.Document;

public sealed class ProjectDocumentDto
{
    public int FormatVersion { get; set; } = 1;

    public string LinearDisplayUnit { get; set; } = "Millimeters";

    public List<VertexDto> Vertices { get; set; } = [];

    public List<EdgeDto> Edges { get; set; } = [];

    public List<PolygonDto> UserPolygons { get; set; } = [];

    public List<DimensionDto> Dimensions { get; set; } = [];

    public List<AxisDto> Axes { get; set; } = [];

    public List<LeaderDto> Leaders { get; set; } = [];

    public double? ElevationBaseY { get; set; }

    public List<string> SuppressedFaceGeometryKeys { get; set; } = [];

    public Dictionary<string, FaceFillStyleDto> FaceFillStyles { get; set; } = new(StringComparer.Ordinal);
}

public sealed class VertexDto
{
    public Guid Id { get; set; }

    public double X { get; set; }

    public double Y { get; set; }
}

public sealed class EdgeDto
{
    public Guid Id { get; set; }

    public Guid StartVertexId { get; set; }

    public Guid EndVertexId { get; set; }

    public bool IsAxis { get; set; }

    public string Color { get; set; } = "#FF222222";

    public double Thickness { get; set; } = 1.5;

    public string LineType { get; set; } = "Solid";

    public string OrthoAlignment { get; set; } = "None";
}

public sealed class PolygonDto
{
    public string Type { get; set; } = "Face";

    public List<DirectedEdgeReferenceDto> OuterLoop { get; set; } = [];

    public List<List<DirectedEdgeReferenceDto>> InnerLoops { get; set; } = [];
}

public sealed class DirectedEdgeReferenceDto
{
    public Guid EdgeId { get; set; }

    public bool Forward { get; set; }
}

public sealed class AxisDto
{
    public Guid Id { get; set; }

    public double StartX { get; set; }

    public double StartY { get; set; }

    public double EndX { get; set; }

    public double EndY { get; set; }
}

public sealed class LeaderDto
{
    public Guid Id { get; set; }

    public string Kind { get; set; } = "Text";

    public double TargetX { get; set; }

    public double TargetY { get; set; }

    public double TextX { get; set; }

    public double TextY { get; set; }

    public string Text { get; set; } = string.Empty;
}

public sealed class DimensionDto
{
    public Guid Id { get; set; }

    public Guid FirstVertexId { get; set; }

    public Guid SecondVertexId { get; set; }

    public double FirstAnchorX { get; set; }

    public double FirstAnchorY { get; set; }

    public double SecondAnchorX { get; set; }

    public double SecondAnchorY { get; set; }

    public double Offset { get; set; }

    public string ExtensionStyle { get; set; } = "Full";

    public bool IsOrthogonal { get; set; }

    public bool OrthogonalIsHorizontal { get; set; }

    public double? TextSize { get; set; }
}

public sealed class FaceFillStyleDto
{
    public string FillColor { get; set; } = "#FFF0F0F0";

    public string FillPattern { get; set; } = "Solid";
}

public sealed class SitManifestDto
{
    public string Format { get; set; } = "LiteCad";

    public int Version { get; set; } = 1;

    public string? ProjectName { get; set; }

    public string? ModifiedUtc { get; set; }
}
