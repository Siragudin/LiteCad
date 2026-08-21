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

    public MirrorToolOptions MirrorToolOptions { get; } = new();

    public DimensionToolOptions DimensionToolOptions { get; } = new();

    public DisplayUnitSettings DisplayUnitSettings { get; } = new();

    public FillToolOptions FillToolOptions { get; } = new();

    public AxisToolOptions AxisToolOptions { get; } = new();

    public SnapService SnapService { get; } = new();

    public DocumentHistoryService History { get; } = new();

    public EditService Edit { get; } = new();

    public ProjectFileState ProjectFile { get; } = new();

    public void NewDocument()
    {
        ClearDocumentContent();
        ProjectFile.Reset();
    }

    public void LoadProject(ProjectDocumentDto dto, string filePath)
    {
        ClearDocumentContent();
        ProjectDocumentSerializer.Apply(Document, dto);
        DisplayUnitSettings.LinearUnit = ProjectDocumentSerializer.ParseLinearDisplayUnit(dto.LinearDisplayUnit);
        ProjectFile.MarkSaved(filePath);
    }

    private void ClearDocumentContent()
    {
        Document.Vertices.Clear();
        Document.Edges.Clear();
        Document.Polygons.Clear();
        Document.Dimensions.Clear();
        Document.Axes.Clear();
        Document.SuppressedFaceGeometryKeys.Clear();
        Document.FaceFillStyles.Clear();
        Selection.Clear();
        History.Clear();
        Camera.Reset();
    }
}
