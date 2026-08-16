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
            ToolId.Rectangle => Strings.Tool_Rectangle,
            ToolId.Circle => Strings.Tool_Circle,
            ToolId.Axis => Strings.Tool_Axis,
            ToolId.Wall => Strings.Tool_Wall,
            ToolId.Move => Strings.Tool_Move,
            ToolId.Stretch => Strings.Tool_Stretch,
            ToolId.Copy => Strings.Tool_Copy,
            ToolId.Delete => Strings.Tool_Delete,
            ToolId.PolygonEdit => Strings.Tool_PolygonEdit,
            _ => toolId.ToString()
        };

    public static ToolId? TryParseTag(string tag)
        => Enum.TryParse<ToolId>(tag, out var toolId) ? toolId : null;
}
