using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Rendering.Pdf;
using LiteCad.Resources;
using LiteCad.Services;
using LiteCad.UI.Pdf;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class ThemeSettingsTests : IDisposable
{
    private readonly string _tempSettingsDirectory;
    private readonly string _tempRoot;

    public ThemeSettingsTests()
    {
        _tempSettingsDirectory = Path.Combine(Path.GetTempPath(), "LiteCad.Tests", Guid.NewGuid().ToString("N"));
        _tempRoot = Path.Combine(Path.GetTempPath(), "LiteCadThemeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempSettingsDirectory);
        Directory.CreateDirectory(_tempRoot);
        AppSettingsStore.SetSettingsDirectoryForTests(_tempSettingsDirectory);
        ThemeManager.Instance.Initialize(AppTheme.Light);
    }

    public void Dispose()
    {
        AppSettingsStore.SetSettingsDirectoryForTests(null);
        ThemeManager.Instance.Initialize(AppTheme.Light);
        if (Directory.Exists(_tempSettingsDirectory))
        {
            Directory.Delete(_tempSettingsDirectory, recursive: true);
        }

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void DefaultTheme_IsLight()
    {
        Assert.Equal(AppTheme.Light, AppSettingsStore.Load().Theme);
        Assert.Equal(AppTheme.Light, ThemeManager.Instance.Theme);
        Assert.False(CanvasTheme.IsDark);
    }

    [Fact]
    public void SwitchingLightToDark_UpdatesCanvasTheme()
    {
        ThemeManager.Instance.SetTheme(AppTheme.Dark);

        Assert.Equal(AppTheme.Dark, ThemeManager.Instance.Theme);
        Assert.True(CanvasTheme.IsDark);
        Assert.Equal(Color.FromRgb(0x1E, 0x1E, 0x1E), CanvasTheme.CanvasBackground);
    }

    [Fact]
    public void SwitchingDarkToLight_RestoresCanvasTheme()
    {
        ThemeManager.Instance.SetTheme(AppTheme.Dark);
        ThemeManager.Instance.SetTheme(AppTheme.Light);

        Assert.Equal(AppTheme.Light, ThemeManager.Instance.Theme);
        Assert.False(CanvasTheme.IsDark);
        Assert.Equal(Colors.White, CanvasTheme.CanvasBackground);
    }

    [Fact]
    public void Theme_PersistsBetweenRestarts()
    {
        ThemeManager.Instance.SetTheme(AppTheme.Dark);

        ThemeManager.Instance.Initialize(AppSettingsStore.Load().Theme);

        Assert.Equal(AppTheme.Dark, ThemeManager.Instance.Theme);
        Assert.True(CanvasTheme.IsDark);
    }

    [Fact]
    public void Theme_IsNotPartOfProjectDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(1, 0));
        var dto = ProjectDocumentSerializer.ToDto(document, LinearDisplayUnit.Millimeters);
        var json = JsonSerializer.Serialize(dto);

        ThemeManager.Instance.SetTheme(AppTheme.Dark);

        Assert.DoesNotContain("Theme", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("theme", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserFillColor_DoesNotChangeWhenSwitchingTheme()
    {
        var userRed = Color.FromRgb(0xE5, 0x39, 0x35);
        var stored = userRed;

        ThemeManager.Instance.SetTheme(AppTheme.Light);
        var lightDisplay = DocumentDisplayColors.ResolveFillColor(stored, forScreenDisplay: true);

        ThemeManager.Instance.SetTheme(AppTheme.Dark);
        var darkDisplay = DocumentDisplayColors.ResolveFillColor(stored, forScreenDisplay: true);

        Assert.Equal(userRed, lightDisplay);
        Assert.Equal(userRed, darkDisplay);
    }

    [Fact]
    public void UserEdgeColor_DoesNotChangeWhenSwitchingTheme()
    {
        var userBlue = Color.FromRgb(0x00, 0x00, 0xFF);
        var stored = userBlue;

        ThemeManager.Instance.SetTheme(AppTheme.Light);
        var lightDisplay = DocumentDisplayColors.ResolveEdgeColor(stored, forScreenDisplay: true);

        ThemeManager.Instance.SetTheme(AppTheme.Dark);
        var darkDisplay = DocumentDisplayColors.ResolveEdgeColor(stored, forScreenDisplay: true);

        Assert.Equal(userBlue, lightDisplay);
        Assert.Equal(userBlue, darkDisplay);
    }

    [Fact]
    public void DefaultEdgeColor_AdaptsForDarkCanvasOnlyOnScreen()
    {
        var stored = DocumentDisplayColors.DefaultEdgeColor;

        ThemeManager.Instance.SetTheme(AppTheme.Dark);
        var screenDisplay = DocumentDisplayColors.ResolveEdgeColor(stored, forScreenDisplay: true);
        var exportDisplay = DocumentDisplayColors.ResolveEdgeColor(stored, forScreenDisplay: false);

        Assert.Equal(CanvasTheme.DefaultEdgeDisplay, screenDisplay);
        Assert.Equal(stored, exportDisplay);
    }

    [Fact]
    public void PdfExport_UsesWhiteBackgroundRegardlessOfTheme()
    {
        ThemeManager.Instance.SetTheme(AppTheme.Dark);

        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var pdfPath = Path.Combine(_tempRoot, "Export.pdf");
            PdfExporter.Export(document, LinearDisplayUnit.Millimeters, new Renderer(), pdfPath);
            Assert.True(File.Exists(pdfPath));
        });

        Assert.True(CanvasTheme.IsDark);
    }

    [Fact]
    public void SaveLanguage_PreservesThemeSetting()
    {
        ThemeManager.Instance.SetTheme(AppTheme.Dark);
        AppSettingsStore.SaveLanguage(AppLanguage.Russian);

        Assert.Equal(AppTheme.Dark, AppSettingsStore.Load().Theme);
        Assert.Equal(AppLanguage.Russian, AppSettingsStore.Load().Language);
    }

    [Fact]
    public void DarkTheme_PreviewForeground_IsLight()
    {
        ThemeManager.Instance.SetTheme(AppTheme.Dark);

        Assert.Equal(Color.FromRgb(0xF5, 0xF5, 0xF5), CanvasTheme.PreviewForeground);
        Assert.Equal(Color.FromRgb(0xF5, 0xF5, 0xF5), PreviewLineRenderer.GetPreviewColor(Color.FromRgb(0x22, 0x22, 0x22)));
    }

    [Fact]
    public void LightTheme_PreviewForeground_KeepsCommitColor()
    {
        ThemeManager.Instance.SetTheme(AppTheme.Light);
        var commitColor = Color.FromRgb(0x22, 0x22, 0x22);

        Assert.Equal(commitColor, PreviewLineRenderer.GetPreviewColor(commitColor));
        Assert.Equal(Color.FromRgb(0x43, 0xA0, 0x47), PreviewLineRenderer.GetAnnotationPreviewColor());
    }

    [Fact]
    public void SavedProject_DoesNotStoreTheme()
    {
        var storage = new ProjectStorage(_tempRoot);
        var document = CreateSquareDocument();
        var filePath = Path.Combine(_tempRoot, "Square.sit");

        WpfTestUtilities.RunSta(() =>
        {
            storage.SaveProject(document, LinearDisplayUnit.Millimeters, filePath, new Renderer());
        });

        ThemeManager.Instance.SetTheme(AppTheme.Dark);

        var dto = storage.LoadProject(filePath, out _);
        Assert.NotNull(dto);
        Assert.DoesNotContain("Theme", JsonSerializer.Serialize(dto), StringComparison.OrdinalIgnoreCase);
    }

    private static CadDocument CreateSquareDocument()
    {
        const double tolerance = 1e-4;
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(100, 100), tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 100), new PointF(0, 100), tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 100), new PointF(0, 0), tolerance);
        PolygonBuilder.SyncFaces(document, tolerance);
        return document;
    }
}
