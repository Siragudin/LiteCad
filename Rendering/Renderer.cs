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
    private readonly SelectionRenderer _selectionRenderer = new();

    public void Render(
        DrawingContext context,
        CadDocument document,
        Selection selection,
        Camera camera,
        Size viewport,
        ITool? activeTool,
        LinearDisplayUnit linearUnit)
    {
        context.DrawRectangle(CanvasTheme.CreateFrozenBrush(CanvasTheme.CanvasBackground), null, new Rect(0, 0, viewport.Width, viewport.Height));

        context.PushTransform(new MatrixTransform(camera.GetWorldToScreenMatrix(viewport)));
        try
        {
            _gridRenderer.Render(context, camera, viewport);
            _polygonRenderer.Render(context, document, camera, forScreenDisplay: true);
            _edgeRenderer.Render(context, document, camera, forScreenDisplay: true);
            _axisRenderer.Render(context, document, camera);
            _dimensionRenderer.Render(context, document, selection, camera, viewport, linearUnit, forScreenDisplay: true);
            _selectionRenderer.Render(context, document, selection, camera);
            activeTool?.RenderOverlay(context, camera, viewport);
        }
        finally
        {
            context.Pop();
        }
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
        _axisRenderer.Render(context, document, exportCamera);
        _dimensionRenderer.Render(
            context,
            document,
            new Selection(),
            exportCamera,
            contentViewport,
            linearUnit,
            forScreenDisplay: false);
    }
}
