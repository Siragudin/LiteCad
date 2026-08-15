using LiteCad.Core.Document;
using LiteCad.Core.Selection;
using LiteCad.Rendering;
using LiteCad.Services;

namespace LiteCad.Infrastructure;

public sealed class CadSession
{
    public CadDocument Document { get; } = new();

    public Selection Selection { get; } = new();

    public Camera Camera { get; } = new();

    public Renderer Renderer { get; } = new();

    public ToolService ToolService { get; } = new();

    public LineToolOptions LineToolOptions { get; } = new();

    public SnapService SnapService { get; } = new();

    public DocumentHistoryService History { get; } = new();

    public EditService Edit { get; } = new();

    public void NewDocument()
    {
        Document.Vertices.Clear();
        Document.Edges.Clear();
        Document.Polygons.Clear();
        Document.SuppressedFaceGeometryKeys.Clear();
        Selection.Clear();
        History.Clear();
        Camera.Reset();
    }
}
