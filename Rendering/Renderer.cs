using LiteCad.Core.Document;
using LiteCad.Core.Selection;
using LiteCad.Services;
using LiteCad.Tools;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class Renderer
{
    private readonly GridRenderer _gridRenderer = new();
    private readonly PolygonRenderer _polygonRenderer = new();
    private readonly EdgeRenderer _edgeRenderer = new();
    private readonly AxisRenderer _axisRenderer = new();
    private readonly DimensionRenderer _dimensionRenderer = new();
    private readonly LeaderRenderer _leaderRenderer = new();
    private readonly TextRenderer _textRenderer = new();
    private readonly SelectionRenderer _selectionRenderer = new();
    private readonly StaticSceneCache _worldGeometryCache = new();
    private readonly ViewportCompositeCache _viewportCompositeCache = new();

    public StaticSceneCache WorldGeometryCache => _worldGeometryCache;

    public ViewportCompositeCache ViewportCompositeCache => _viewportCompositeCache;

    public void InvalidateWorldGeometryCache()
    {
        _worldGeometryCache.Invalidate();
        _viewportCompositeCache.Invalidate();
    }

    public void InvalidateViewportCompositeCache()
        => _viewportCompositeCache.Invalidate();

    public void Render(
        DrawingContext context,
        CadDocument document,
        Selection selection,
        Camera camera,
        Size viewport,
        ITool? activeTool,
        LinearDisplayUnit linearUnit,
        ViewportRenderPass pass = ViewportRenderPass.Full)
    {
        context.DrawRectangle(CanvasTheme.CreateFrozenBrush(CanvasTheme.CanvasBackground), null, new Rect(0, 0, viewport.Width, viewport.Height));

        var excludeDimensions = activeTool is DimensionTool { HasOffsetPhase: true };
        var cacheKey = ViewportCompositeCache.CreateKey(
            document,
            selection,
            camera,
            viewport,
            linearUnit,
            excludeDimensions);

        context.PushTransform(new MatrixTransform(camera.GetWorldToScreenMatrix(viewport)));
        try
        {
            _viewportCompositeCache.DrawCompositeOrBuild(
                pass,
                cacheKey,
                compositeContext => RenderCompositeLayers(
                    compositeContext,
                    document,
                    selection,
                    camera,
                    viewport,
                    activeTool,
                    linearUnit,
                    excludeDimensions),
                context);

            if (excludeDimensions)
            {
                _dimensionRenderer.Render(
                    context,
                    document,
                    selection,
                    camera,
                    viewport,
                    linearUnit,
                    activeTool,
                    forScreenDisplay: true);
            }

            activeTool?.RenderOverlay(context, camera, viewport);
        }
        finally
        {
            context.Pop();
        }
    }

    private void RenderCompositeLayers(
        DrawingContext context,
        CadDocument document,
        Selection selection,
        Camera camera,
        Size viewport,
        ITool? activeTool,
        LinearDisplayUnit linearUnit,
        bool excludeDimensions)
    {
        _gridRenderer.Render(context, camera, viewport);
        _worldGeometryCache.DrawWorldGeometry(context, document, camera, forScreenDisplay: true);
        _polygonRenderer.RenderFaceHatches(context, document, camera, forScreenDisplay: true, viewport);

        if (!excludeDimensions)
        {
            _dimensionRenderer.Render(
                context,
                document,
                selection,
                camera,
                viewport,
                linearUnit,
                activeTool,
                forScreenDisplay: true);
        }

        _leaderRenderer.Render(
            context,
            document,
            selection,
            camera,
            viewport,
            forScreenDisplay: true);
        _textRenderer.Render(
            context,
            document,
            selection,
            camera,
            viewport,
            forScreenDisplay: true);
        _selectionRenderer.Render(context, document, selection, camera);
    }

    public void RenderForExport(
        DrawingContext context,
        CadDocument document,
        Camera exportCamera,
        Size contentViewport,
        LinearDisplayUnit linearUnit)
    {
        context.PushTransform(new MatrixTransform(exportCamera.GetWorldToScreenMatrix(contentViewport)));
        try
        {
            RenderForExportContent(context, document, exportCamera, contentViewport, linearUnit);
        }
        finally
        {
            context.Pop();
        }
    }

    public void RenderForExportContent(
        DrawingContext context,
        CadDocument document,
        Camera exportCamera,
        Size contentViewport,
        LinearDisplayUnit linearUnit)
    {
        _polygonRenderer.Render(context, document, exportCamera, forScreenDisplay: false);
        _edgeRenderer.Render(context, document, exportCamera, forScreenDisplay: false);
        _axisRenderer.Render(context, document, exportCamera, forScreenDisplay: false);
        _dimensionRenderer.Render(
            context,
            document,
            new Selection(),
            exportCamera,
            contentViewport,
            linearUnit,
            activeTool: null,
            forScreenDisplay: false);
        _leaderRenderer.Render(
            context,
            document,
            new Selection(),
            exportCamera,
            contentViewport,
            forScreenDisplay: false);
        _textRenderer.Render(
            context,
            document,
            new Selection(),
            exportCamera,
            contentViewport,
            forScreenDisplay: false);
    }
}
