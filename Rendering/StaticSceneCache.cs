using LiteCad.Core.Document;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class StaticSceneCache
{
    private sealed record CacheKey(long Revision, double Zoom, string Theme);

    private CacheKey? _key;
    private DrawingGroup? _worldGeometry;
    private readonly StaticSceneBuilder _builder = new();

    public int BuildCount { get; private set; }

    public void Invalidate()
    {
        _key = null;
        _worldGeometry = null;
    }

    public void DrawWorldGeometry(
        DrawingContext context,
        CadDocument document,
        Camera camera,
        bool forScreenDisplay = true)
    {
        var key = new CacheKey(document.Revision, camera.Zoom, _builder.GetThemeKey());
        if (_worldGeometry is null || _key != key)
        {
            _worldGeometry = _builder.BuildWorldGeometry(document, camera, forScreenDisplay);
            _key = key;
            BuildCount++;
        }

        context.DrawDrawing(_worldGeometry);
    }
}
