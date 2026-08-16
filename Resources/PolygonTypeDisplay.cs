using LiteCad.Core.Document;

namespace LiteCad.Resources;

public static class PolygonTypeDisplay
{
    public static string Get(PolygonType type)
        => type switch
        {
            PolygonType.Face => Strings.Label_PolygonType_Face,
            PolygonType.Wall => Strings.Label_PolygonType_Wall,
            PolygonType.Room => Strings.Label_PolygonType_Room,
            PolygonType.Axis => Strings.Label_PolygonType_Axis,
            _ => type.ToString()
        };
}
