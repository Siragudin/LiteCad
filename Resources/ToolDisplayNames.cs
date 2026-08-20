using LiteCad.Tools;

namespace LiteCad.Resources;

public static class ToolDisplayNames
{
    public static string Get(ToolId toolId)
        => toolId switch
        {
            ToolId.Selection => Strings.Tool_Selection,
            ToolId.Hand => Strings.Tool_Hand,
            ToolId.Line => Strings.Tool_Line,
            ToolId.Arc => Strings.Tool_Arc,
            ToolId.Rectangle => Strings.Tool_Rectangle,
            ToolId.Circle => Strings.Tool_Circle,
            ToolId.Sector => Strings.Tool_Sector,
            ToolId.Axis => Strings.Tool_Axis,
            ToolId.Wall => Strings.Tool_Wall,
            ToolId.Move => Strings.Tool_Move,
            ToolId.Rotate => Strings.Tool_Rotate,
            ToolId.Mirror => Strings.Tool_Mirror,
            ToolId.Offset => Strings.Tool_Offset,
            ToolId.Stretch => Strings.Tool_Stretch,
            ToolId.Extend => Strings.Tool_Extend,
            ToolId.Dimension => Strings.Tool_Dimension,
            ToolId.Eraser => Strings.Tool_Eraser,
            ToolId.Copy => Strings.Tool_Copy,
            ToolId.PolygonEdit => Strings.Tool_PolygonEdit,
            _ => toolId.ToString()
        };

    public static ToolId? TryParseTag(string tag)
        => Enum.TryParse<ToolId>(tag, out var toolId) ? toolId : null;
}
