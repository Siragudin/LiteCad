using LiteCad.Core.Document;
using LiteCad.Core.Selection;
using LiteCad.Tools;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public sealed class Renderer
{
    private readonly GridRenderer _gridRenderer = new();
    private readonly PolygonRenderer _polygonRenderer = new();
    private readonly EdgeRenderer _edgeRenderer = new();
    private readonly SelectionRenderer _selectionRenderer = new();

    public void Render(
        DrawingContext context,
        CadDocument document,
        Selection selection,
        Camera camera,
        Size viewport,
        ITool? activeTool)
    {
        context.DrawRectangle(Brushes.White, null, new Rect(0, 0, viewport.Width, viewport.Height));

        context.PushTransform(new MatrixTransform(camera.GetWorldToScreenMatrix(viewport)));
        try
        {
            _gridRenderer.Render(context, camera, viewport);
            _polygonRenderer.Render(context, document, camera);
            _edgeRenderer.Render(context, document, camera);
            _selectionRenderer.Render(context, document, selection, camera);
            activeTool?.RenderOverlay(context, camera, viewport);
        }
        finally
        {
            context.Pop();
        }
    }
}
