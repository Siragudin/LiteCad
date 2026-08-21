using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Rendering;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LiteCad.Services;

public sealed class ProjectInfo
{
    public required string FilePath { get; init; }

    public required string Name { get; init; }

    public DateTime ModifiedUtc { get; init; }
}

public sealed class ProjectStorage
{
    public const string ProjectJsonEntry = "project.json";
    public const string PreviewPngEntry = "preview.png";
    public const string ManifestJsonEntry = "manifest.json";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string? _projectsRootOverride;

    public ProjectStorage(string? projectsRootOverride = null)
    {
        _projectsRootOverride = projectsRootOverride;
    }

    public string ProjectsDirectory
        => _projectsRootOverride
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "LiteCad",
                "Projects");

    public void EnsureProjectsDirectoryExists()
    {
        Directory.CreateDirectory(ProjectsDirectory);
    }

    public IReadOnlyList<ProjectInfo> ListProjects()
    {
        EnsureProjectsDirectoryExists();
        return Directory
            .EnumerateFiles(ProjectsDirectory, "*" + ProjectFileNameHelper.SitExtension, SearchOption.TopDirectoryOnly)
            .Select(path => new ProjectInfo
            {
                FilePath = path,
                Name = ProjectFileNameHelper.GetProjectDisplayName(path),
                ModifiedUtc = File.GetLastWriteTimeUtc(path)
            })
            .OrderByDescending(info => info.ModifiedUtc)
            .ToList();
    }

    public void SaveProject(
        CadDocument document,
        LinearDisplayUnit linearDisplayUnit,
        string filePath,
        Renderer renderer)
    {
        EnsureProjectsDirectoryExists();

        var normalizedPath = ProjectFileNameHelper.NormalizeSitFilePath(filePath);
        var projectJson = ProjectDocumentSerializer.Serialize(document, linearDisplayUnit);
        var previewBytes = ProjectPreviewGenerator.GeneratePng(document, renderer);
        var manifest = new SitManifestDto
        {
            Format = "LiteCad",
            Version = 1,
            ProjectName = ProjectFileNameHelper.GetProjectDisplayName(normalizedPath),
            ModifiedUtc = DateTime.UtcNow.ToString("O")
        };
        var manifestJson = JsonSerializer.Serialize(manifest, ManifestJsonOptions);

        var directory = Path.GetDirectoryName(normalizedPath)
            ?? throw new InvalidOperationException("Project path has no directory.");
        Directory.CreateDirectory(directory);

        var tempPath = normalizedPath + ".tmp";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        try
        {
            WriteSitArchive(tempPath, projectJson, previewBytes, manifestJson);
            if (File.Exists(normalizedPath))
            {
                File.Delete(normalizedPath);
            }

            File.Move(tempPath, normalizedPath);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    public ProjectDocumentDto LoadProject(string filePath, out LinearDisplayUnit linearDisplayUnit)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Project file was not found.", filePath);
        }

        using var archive = ZipFile.OpenRead(filePath);
        var projectEntry = RequireEntry(archive, ProjectJsonEntry);
        string projectJson;
        using (var stream = projectEntry.Open())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            projectJson = reader.ReadToEnd();
        }

        var dto = ProjectDocumentSerializer.Deserialize(projectJson);
        linearDisplayUnit = ProjectDocumentSerializer.ParseLinearDisplayUnit(dto.LinearDisplayUnit);
        return dto;
    }

    public void DeleteProject(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    public bool ProjectExists(string? filePath)
        => !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath);

    public static void WriteSitArchive(string path, string projectJson, byte[] previewBytes, string manifestJson)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteTextEntry(archive, ProjectJsonEntry, projectJson);
        WriteBinaryEntry(archive, PreviewPngEntry, previewBytes);
        WriteTextEntry(archive, ManifestJsonEntry, manifestJson);
    }

    internal static bool TryReadEntryBytes(string sitPath, string entryName, out byte[] bytes)
    {
        bytes = [];
        if (!File.Exists(sitPath))
        {
            return false;
        }

        using var archive = ZipFile.OpenRead(sitPath);
        var entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            return false;
        }

        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        bytes = memory.ToArray();
        return true;
    }

    private static ZipArchiveEntry RequireEntry(ZipArchive archive, string entryName)
        => archive.GetEntry(entryName)
            ?? throw new InvalidDataException($"Missing '{entryName}' in project archive.");

    private static void WriteTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void WriteBinaryEntry(ZipArchive archive, string entryName, byte[] content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }
}

public static class ProjectPreviewGenerator
{
    private const int PreviewWidth = 640;
    private const int PreviewHeight = 480;

    public static byte[] GeneratePng(CadDocument document, Renderer renderer)
    {
        var camera = new Camera();
        FitCameraToDocument(camera, document, new Size(PreviewWidth, PreviewHeight));

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            renderer.Render(
                context,
                document,
                new Selection(),
                camera,
                new Size(PreviewWidth, PreviewHeight),
                activeTool: null,
                LinearDisplayUnit.Millimeters);
        }

        var bitmap = new RenderTargetBitmap(
            PreviewWidth,
            PreviewHeight,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static void FitCameraToDocument(Camera camera, CadDocument document, Size viewport)
    {
        if (document.Vertices.Count == 0)
        {
            camera.Reset();
            return;
        }

        var minX = document.Vertices.Min(vertex => vertex.Position.X);
        var maxX = document.Vertices.Max(vertex => vertex.Position.X);
        var minY = document.Vertices.Min(vertex => vertex.Position.Y);
        var maxY = document.Vertices.Max(vertex => vertex.Position.Y);
        camera.FitWorldBounds(minX, minY, maxX, maxY, viewport);
    }
}
