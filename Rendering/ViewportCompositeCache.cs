using LiteCad.Infrastructure;
using LiteCad.Services;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed record ViewportCompositeCacheKey(
    long Revision,
    double Zoom,
    double PanX,
    double PanY,
    double ViewportWidth,
    double ViewportHeight,
    string Theme,
    string SelectionKey,
    LinearDisplayUnit LinearUnit,
    bool ExcludeDimensions);

public sealed class ViewportCompositeCache
{
    private ViewportCompositeCacheKey? _key;
    private DrawingGroup? _composite;

    public int BuildCount { get; private set; }

    public void Invalidate()
    {
        _key = null;
        _composite = null;
    }

    public void DrawCompositeOrBuild(
        ViewportRenderPass pass,
        ViewportCompositeCacheKey key,
        Action<DrawingContext> buildComposite,
        DrawingContext target)
    {
        if (pass == ViewportRenderPass.OverlayOnly
            && _composite is not null
            && _key == key)
        {
            target.DrawDrawing(_composite);
            return;
        }

        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            buildComposite(context);
        }

        group.Freeze();
        _composite = group;
        _key = key;
        BuildCount++;
        target.DrawDrawing(_composite);
    }

    public static ViewportCompositeCacheKey CreateKey(
        Core.Document.CadDocument document,
        Core.Selection.Selection selection,
        Camera camera,
        Size viewport,
        LinearDisplayUnit linearUnit,
        bool excludeDimensions)
        => new(
            document.Revision,
            camera.Zoom,
            camera.PanOffset.X,
            camera.PanOffset.Y,
            viewport.Width,
            viewport.Height,
            ThemeManager.Instance.Theme,
            CreateSelectionKey(selection),
            linearUnit,
            excludeDimensions);

    private static string CreateSelectionKey(Core.Selection.Selection selection)
    {
        static string Join(IEnumerable<Guid> ids)
            => string.Join(",", ids.OrderBy(id => id));

        return string.Join(
            "|",
            Join(selection.SelectedVertexIds),
            Join(selection.SelectedEdgeIds),
            Join(selection.SelectedPolygonIds),
            Join(selection.SelectedAxisIds),
            Join(selection.SelectedDimensionIds),
            Join(selection.SelectedLeaderIds),
            Join(selection.SelectedTextIds));
    }
}
