using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Leaders;
using LiteCad.Texts;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace LiteCad.Services;

public static class ProjectDocumentSerializer
{
    public const int CurrentFormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(CadDocument document, LinearDisplayUnit linearDisplayUnit)
        => JsonSerializer.Serialize(ToDto(document, linearDisplayUnit), JsonOptions);

    public static ProjectDocumentDto ToDto(CadDocument document, LinearDisplayUnit linearDisplayUnit)
    {
        var dto = new ProjectDocumentDto
        {
            FormatVersion = CurrentFormatVersion,
            LinearDisplayUnit = linearDisplayUnit.ToString(),
            Vertices = document.Vertices
                .Select(vertex => new VertexDto
                {
                    Id = vertex.Id,
                    X = vertex.Position.X,
                    Y = vertex.Position.Y
                })
                .ToList(),
            Edges = document.Edges.Select(MapEdge).ToList(),
            UserPolygons = document.Polygons
                .Where(polygon => polygon.Type != PolygonType.Face)
                .Select(MapPolygon)
                .ToList(),
            Dimensions = document.Dimensions.Select(MapDimension).ToList(),
            Axes = document.Axes.Select(MapAxis).ToList(),
            Leaders = document.Leaders.Select(MapLeader).ToList(),
            Texts = document.Texts.Select(MapTextNote).ToList(),
            ElevationBaseY = document.ElevationBaseY,
            SuppressedFaceGeometryKeys = document.SuppressedFaceGeometryKeys.ToList(),
            FaceFillStyles = document.FaceFillStyles.ToDictionary(
                pair => pair.Key,
                pair => MapFaceFillStyle(pair.Value),
                StringComparer.Ordinal)
        };

        return dto;
    }

    public static ProjectDocumentDto Deserialize(string json)
        => JsonSerializer.Deserialize<ProjectDocumentDto>(json, JsonOptions)
            ?? throw new InvalidDataException("Project JSON is empty.");

    public static void Apply(CadDocument document, ProjectDocumentDto dto)
    {
        if (dto.FormatVersion != CurrentFormatVersion)
        {
            throw new NotSupportedException($"Unsupported project format version: {dto.FormatVersion}.");
        }

        document.Vertices.Clear();
        document.Edges.Clear();
        document.Polygons.Clear();
        document.Dimensions.Clear();
        document.Axes.Clear();
        document.Leaders.Clear();
        document.Texts.Clear();
        document.ElevationBaseY = dto.ElevationBaseY;
        document.SuppressedFaceGeometryKeys.Clear();
        document.FaceFillStyles.Clear();

        foreach (var vertex in dto.Vertices)
        {
            document.Vertices.Add(new Vertex(vertex.Id, new PointF((float)vertex.X, (float)vertex.Y)));
        }

        foreach (var edge in dto.Edges)
        {
            document.Edges.Add(new Edge(edge.Id, edge.StartVertexId, edge.EndVertexId)
            {
                IsAxis = edge.IsAxis,
                Color = ParseColor(edge.Color),
                Thickness = edge.Thickness,
                LineType = ParseEnum(edge.LineType, EdgeLineType.Solid),
                OrthoAlignment = ParseEnum(edge.OrthoAlignment, OrthoAlignment.None)
            });
        }

        foreach (var polygon in dto.UserPolygons)
        {
            document.Polygons.Add(MapPolygon(polygon));
        }

        foreach (var dimension in dto.Dimensions)
        {
            document.Dimensions.Add(new Dimension(
                dimension.Id,
                dimension.FirstVertexId,
                dimension.SecondVertexId,
                new PointF((float)dimension.FirstAnchorX, (float)dimension.FirstAnchorY),
                new PointF((float)dimension.SecondAnchorX, (float)dimension.SecondAnchorY),
                dimension.Offset,
                ParseEnum(dimension.ExtensionStyle, DimensionExtensionStyle.Full),
                dimension.IsOrthogonal,
                dimension.OrthogonalIsHorizontal,
                dimension.TextSize is > 0 ? dimension.TextSize.Value : Dimension.DefaultTextSize));
        }

        foreach (var axis in dto.Axes ?? [])
        {
            document.Axes.Add(new Axis(
                axis.Id,
                new PointF((float)axis.StartX, (float)axis.StartY),
                new PointF((float)axis.EndX, (float)axis.EndY)));
        }

        foreach (var leader in dto.Leaders ?? [])
        {
            document.Leaders.Add(new Leader(
                leader.Id,
                ParseEnum(leader.Kind, LeaderKind.Text),
                new PointF((float)leader.TargetX, (float)leader.TargetY),
                new PointF((float)leader.TextX, (float)leader.TextY),
                leader.Text ?? string.Empty));
        }

        foreach (var note in dto.Texts ?? [])
        {
            PointF? arrowTip = note.ArrowTipX is double tipX && note.ArrowTipY is double tipY
                ? new PointF((float)tipX, (float)tipY)
                : null;
            document.Texts.Add(new TextNote(
                note.Id == Guid.Empty ? Guid.NewGuid() : note.Id,
                ParseEnum(note.Kind, TextNoteKind.Plain),
                new PointF((float)note.OriginX, (float)note.OriginY),
                arrowTip,
                note.Text ?? string.Empty,
                note.TextSize));
        }

        foreach (var key in dto.SuppressedFaceGeometryKeys)
        {
            document.SuppressedFaceGeometryKeys.Add(key);
        }

        foreach (var (key, style) in dto.FaceFillStyles)
        {
            document.FaceFillStyles[key] = new FaceFillStyle
            {
                FillColor = ParseColor(style.FillColor),
                FillPattern = ParseEnum(style.FillPattern, FaceFillPattern.Solid)
            };
        }

        PolygonBuilder.SyncFaces(document, TopologyTolerance.ForMutation);
    }

    public static LinearDisplayUnit ParseLinearDisplayUnit(string? value)
        => Enum.TryParse<LinearDisplayUnit>(value, ignoreCase: true, out var unit)
            ? unit
            : LinearDisplayUnit.Millimeters;

    private static EdgeDto MapEdge(Edge edge)
        => new()
        {
            Id = edge.Id,
            StartVertexId = edge.StartVertexId,
            EndVertexId = edge.EndVertexId,
            IsAxis = edge.IsAxis,
            Color = FormatColor(edge.Color),
            Thickness = edge.Thickness,
            LineType = edge.LineType.ToString(),
            OrthoAlignment = edge.OrthoAlignment.ToString()
        };

    private static PolygonDto MapPolygon(Polygon polygon)
        => new()
        {
            Type = polygon.Type.ToString(),
            OuterLoop = polygon.OuterLoop.Edges.Select(MapReference).ToList(),
            InnerLoops = polygon.InnerLoops
                .Select(loop => loop.Edges.Select(MapReference).ToList())
                .ToList()
        };

    private static Polygon MapPolygon(PolygonDto polygon)
    {
        var mapped = new Polygon { Type = ParseEnum(polygon.Type, PolygonType.Face) };
        mapped.OuterLoop.Edges.AddRange(polygon.OuterLoop.Select(MapReference));
        foreach (var innerLoop in polygon.InnerLoops)
        {
            var loop = new Loop();
            loop.Edges.AddRange(innerLoop.Select(MapReference));
            mapped.InnerLoops.Add(loop);
        }

        return mapped;
    }

    private static AxisDto MapAxis(Axis axis)
        => new()
        {
            Id = axis.Id,
            StartX = axis.Start.X,
            StartY = axis.Start.Y,
            EndX = axis.End.X,
            EndY = axis.End.Y
        };

    private static LeaderDto MapLeader(Leader leader)
        => new()
        {
            Id = leader.Id,
            Kind = leader.Kind.ToString(),
            TargetX = leader.Target.X,
            TargetY = leader.Target.Y,
            TextX = leader.TextPosition.X,
            TextY = leader.TextPosition.Y,
            Text = leader.Text
        };

    private static TextNoteDto MapTextNote(TextNote note)
        => new()
        {
            Id = note.Id,
            Kind = note.Kind.ToString(),
            OriginX = note.Origin.X,
            OriginY = note.Origin.Y,
            ArrowTipX = note.ArrowTip?.X,
            ArrowTipY = note.ArrowTip?.Y,
            Text = note.Text,
            TextSize = note.TextSize
        };

    private static DimensionDto MapDimension(Dimension dimension)
        => new()
        {
            Id = dimension.Id,
            FirstVertexId = dimension.FirstVertexId,
            SecondVertexId = dimension.SecondVertexId,
            FirstAnchorX = dimension.FirstAnchorPosition.X,
            FirstAnchorY = dimension.FirstAnchorPosition.Y,
            SecondAnchorX = dimension.SecondAnchorPosition.X,
            SecondAnchorY = dimension.SecondAnchorPosition.Y,
            Offset = dimension.Offset,
            ExtensionStyle = dimension.ExtensionStyle.ToString(),
            IsOrthogonal = dimension.IsOrthogonal,
            OrthogonalIsHorizontal = dimension.OrthogonalIsHorizontal,
            TextSize = dimension.TextSize
        };

    private static FaceFillStyleDto MapFaceFillStyle(FaceFillStyle style)
        => new()
        {
            FillColor = FormatColor(style.FillColor),
            FillPattern = style.FillPattern.ToString()
        };

    private static DirectedEdgeReferenceDto MapReference(DirectedEdgeReference reference)
        => new() { EdgeId = reference.EdgeId, Forward = reference.Forward };

    private static DirectedEdgeReference MapReference(DirectedEdgeReferenceDto reference)
        => new(reference.EdgeId, reference.Forward);

    private static string FormatColor(Color color)
        => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private static Color ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Colors.Black;
        }

        var text = value.Trim();
        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        if (text.Length == 6)
        {
            text = "FF" + text;
        }

        if (text.Length != 8
            || !byte.TryParse(text[..2], System.Globalization.NumberStyles.HexNumber, null, out var a)
            || !byte.TryParse(text[2..4], System.Globalization.NumberStyles.HexNumber, null, out var r)
            || !byte.TryParse(text[4..6], System.Globalization.NumberStyles.HexNumber, null, out var g)
            || !byte.TryParse(text[6..8], System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return Colors.Black;
        }

        return Color.FromArgb(a, r, g, b);
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}
