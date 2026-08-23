using LiteCad.Core.Document;
using LiteCad.Core.Selection;
using LiteCad.Rendering;
using LiteCad.Rendering.Pdf;
using LiteCad.Services;
using System.Windows;

namespace LiteCad.Infrastructure;

public sealed class CadSession
{
    public const double OpenProjectFitPaddingFraction = 0.15;
    public CadDocument Document { get; } = new();

    public Selection Selection { get; } = new();

    public Camera Camera { get; } = new();

    public Renderer Renderer { get; } = new();

    public ToolService ToolService { get; } = new();

    public LineToolOptions LineToolOptions { get; } = new();

    public MirrorToolOptions MirrorToolOptions { get; } = new();

    public DimensionToolOptions DimensionToolOptions { get; } = new();

    public LeaderToolOptions LeaderToolOptions { get; } = new();

    public DisplayUnitSettings DisplayUnitSettings { get; } = new();

    public FillToolOptions FillToolOptions { get; } = new();

    public AxisToolOptions AxisToolOptions { get; } = new();

    public OffsetToolOptions OffsetToolOptions { get; } = new();

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

    public void FitCameraToDocument(Size viewport)
    {
        if (viewport.Width < 1 || viewport.Height < 1)
        {
            return;
        }

        if (!DocumentBoundsCalculator.TryComputeWorldBounds(
                Document,
                DisplayUnitSettings.LinearUnit,
                out var bounds))
        {
            return;
        }

        var padding = Math.Min(viewport.Width, viewport.Height) * OpenProjectFitPaddingFraction;
        Camera.FitWorldBounds(
            bounds.Left,
            bounds.Top,
            bounds.Right,
            bounds.Bottom,
            viewport,
            padding);
    }

    private void ClearDocumentContent()
    {
        Document.Vertices.Clear();
        Document.Edges.Clear();
        Document.Polygons.Clear();
        Document.Dimensions.Clear();
        Document.Axes.Clear();
        Document.Leaders.Clear();
        Document.ElevationBaseY = null;
        Document.SuppressedFaceGeometryKeys.Clear();
        Document.FaceFillStyles.Clear();
        Selection.Clear();
        History.Clear();
        Camera.Reset();
    }
}
