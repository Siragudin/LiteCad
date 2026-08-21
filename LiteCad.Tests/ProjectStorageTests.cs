using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Services;
using System.IO.Compression;
using System.Text;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class ProjectStorageTests : IDisposable
{
    private const double Tolerance = 1e-4;

    private readonly string _tempRoot;
    private readonly ProjectStorage _storage;

    public ProjectStorageTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "LiteCadTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _storage = new ProjectStorage(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ProjectsDirectory_PointsToDocumentsLiteCadProjectsByDefault()
    {
        var defaultStorage = new ProjectStorage();
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "LiteCad",
            "Projects");

        Assert.Equal(expected, defaultStorage.ProjectsDirectory);
    }

    [Fact]
    public void EnsureProjectsDirectoryExists_CreatesMissingDirectory()
    {
        var path = Path.Combine(_tempRoot, "Nested", "Projects");
        var storage = new ProjectStorage(path);

        Assert.False(Directory.Exists(path));
        storage.EnsureProjectsDirectoryExists();
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void SaveProject_RecreatesMissingDirectory()
    {
        var document = CreateSquareDocument();
        var filePath = Path.Combine(_tempRoot, "RestoreMe", "Square.sit");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        WpfTestUtilities.RunSta(() =>
        {
            _storage.SaveProject(
                document,
                LinearDisplayUnit.Millimeters,
                filePath,
                new Renderer());
        });

        Directory.Delete(_tempRoot, recursive: true);
        Assert.False(Directory.Exists(_tempRoot));

        WpfTestUtilities.RunSta(() =>
        {
            _storage.SaveProject(
                document,
                LinearDisplayUnit.Millimeters,
                filePath,
                new Renderer());
        });

        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void ProjectDocumentSerializer_RoundTripsDocumentGeometry()
    {
        var original = CreateSquareDocument();
        FaceFillService.TrySetFillColor(original, original.Polygons[0], Colors.Red);

        var json = ProjectDocumentSerializer.Serialize(original, LinearDisplayUnit.Meters);
        var dto = ProjectDocumentSerializer.Deserialize(json);
        var restored = new CadDocument();
        ProjectDocumentSerializer.Apply(restored, dto);

        Assert.Equal(original.Vertices.Count, restored.Vertices.Count);
        Assert.Equal(original.Edges.Count, restored.Edges.Count);
        Assert.Equal(
            original.Polygons.Count(polygon => polygon.Type == PolygonType.Face),
            restored.Polygons.Count(polygon => polygon.Type == PolygonType.Face));
        Assert.Equal(original.FaceFillStyles.Count, restored.FaceFillStyles.Count);
        TopologyValidator.AssertValid(restored, Tolerance);
    }

    [Fact]
    public void WriteSitArchive_ContainsRequiredEntries()
    {
        var sitPath = Path.Combine(_tempRoot, "Sample.sit");
        var projectJson = ProjectDocumentSerializer.Serialize(CreateSquareDocument(), LinearDisplayUnit.Millimeters);
        var preview = CreateMinimalPngBytes();
        var manifest = """{"format":"LiteCad","version":1}""";

        ProjectStorage.WriteSitArchive(sitPath, projectJson, preview, manifest);

        Assert.True(File.Exists(sitPath));
        using var archive = ZipFile.OpenRead(sitPath);
        Assert.NotNull(archive.GetEntry(ProjectStorage.ProjectJsonEntry));
        Assert.NotNull(archive.GetEntry(ProjectStorage.PreviewPngEntry));
        Assert.NotNull(archive.GetEntry(ProjectStorage.ManifestJsonEntry));
    }

    [Fact]
    public void SaveAndLoadProject_RestoresGeometry()
    {
        var source = CreateSquareDocument();
        var filePath = Path.Combine(_tempRoot, "RoundTrip.sit");

        WpfTestUtilities.RunSta(() =>
        {
            _storage.SaveProject(
                source,
                LinearDisplayUnit.Millimeters,
                filePath,
                new Renderer());
        });

        var dto = _storage.LoadProject(filePath, out var unit);
        var restored = new CadDocument();
        ProjectDocumentSerializer.Apply(restored, dto);

        Assert.Equal(LinearDisplayUnit.Millimeters, unit);
        Assert.Equal(4, restored.Vertices.Count);
        Assert.Equal(4, restored.Edges.Count);
        Assert.Single(restored.Polygons, polygon => polygon.Type == PolygonType.Face);
        TopologyValidator.AssertValid(restored, Tolerance);
    }

    [Fact]
    public void NormalizeSitFilePath_AddsSitOnce()
    {
        var path = Path.Combine(_tempRoot, "Дом.sit.sit");
        var normalized = ProjectFileNameHelper.NormalizeSitFilePath(path);

        Assert.EndsWith("Дом.sit", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain(".sit.sit", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSitPath_AddsSitExtension()
    {
        var path = ProjectFileNameHelper.BuildSitPath(_tempRoot, "Квартира");
        Assert.Equal(Path.Combine(_tempRoot, "Квартира.sit"), path);
    }

    [Fact]
    public void DeleteProject_RemovesOnlyTargetFile()
    {
        var first = Path.Combine(_tempRoot, "First.sit");
        var second = Path.Combine(_tempRoot, "Second.sit");
        File.WriteAllText(first, "first");
        File.WriteAllText(second, "second");

        _storage.DeleteProject(first);

        Assert.False(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    [Fact]
    public void DeleteProject_DoesNotThrowWhenFileAlreadyRemoved()
    {
        var path = Path.Combine(_tempRoot, "Missing.sit");
        _storage.DeleteProject(path);
    }

    [Fact]
    public void LoadProject_ClearsHistoryAccordingToSessionArchitecture()
    {
        var session = CreateSquareSession();
        session.History.Record(session.Document);
        EdgeOperations.AddSegment(
            session.Document,
            new PointF(50, 0),
            new PointF(50, 100),
            TestDocumentHelpers.CreateTemplate(),
            Tolerance);
        Assert.True(session.History.CanUndo);

        var filePath = Path.Combine(_tempRoot, "History.sit");
        WpfTestUtilities.RunSta(() =>
        {
            _storage.SaveProject(
                session.Document,
                session.DisplayUnitSettings.LinearUnit,
                filePath,
                session.Renderer);
        });

        var dto = _storage.LoadProject(filePath, out _);
        session.LoadProject(dto, filePath);

        Assert.False(session.History.CanUndo);
        Assert.False(session.History.CanRedo);
        Assert.False(session.ProjectFile.IsDirty);
        Assert.Equal(filePath, session.ProjectFile.CurrentFilePath);
    }

    [Fact]
    public void ListProjects_ReturnsOnlySitFilesInProjectsDirectory()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "A.sit"), "a");
        File.WriteAllText(Path.Combine(_tempRoot, "B.sit"), "b");
        File.WriteAllText(Path.Combine(_tempRoot, "ignore.txt"), "x");

        var projects = _storage.ListProjects();

        Assert.Equal(2, projects.Count);
        Assert.Contains(projects, project => project.Name == "A");
        Assert.Contains(projects, project => project.Name == "B");
    }

    private static CadDocument CreateSquareDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(100, 100), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 100), new PointF(0, 100), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 100), new PointF(0, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }

    private static CadSession CreateSquareSession()
    {
        var session = new CadSession();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(session.Document, new PointF(0, 0), new PointF(100, 0), template, Tolerance);
        EdgeOperations.AddSegment(session.Document, new PointF(100, 0), new PointF(100, 100), template, Tolerance);
        EdgeOperations.AddSegment(session.Document, new PointF(100, 100), new PointF(0, 100), template, Tolerance);
        EdgeOperations.AddSegment(session.Document, new PointF(0, 100), new PointF(0, 0), template, Tolerance);
        PolygonBuilder.SyncFaces(session.Document, Tolerance);
        return session;
    }

    private static byte[] CreateMinimalPngBytes()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
            0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
            0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
            0x42, 0x60, 0x82
        ];
    }
}
