using LiteCad.Core.Document;
using LiteCad.Infrastructure;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class StaticSceneBuilder
{
    private readonly PolygonRenderer _polygonRenderer = new();
    private readonly EdgeRenderer _edgeRenderer = new();
    private readonly AxisRenderer _axisRenderer = new();

    public DrawingGroup BuildWorldGeometry(CadDocument document, Camera camera, bool forScreenDisplay = true)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            _polygonRenderer.RenderSolidGeometry(context, document, camera, forScreenDisplay);
            _edgeRenderer.Render(context, document, camera, forScreenDisplay);
            _axisRenderer.Render(context, document, camera, forScreenDisplay);
        }

        group.Freeze();
        return group;
    }

    public string GetThemeKey()
        => ThemeManager.Instance.Theme;
}
