namespace LiteCad.Services;

using LiteCad.Resources;
using System.IO;

public static class ProjectFileNameHelper
{
    public const string SitExtension = ".sit";

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public static string SanitizeProjectName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Strings.Label_DefaultProjectName;
        }

        var trimmed = name.Trim();
        var invalid = new HashSet<char>(InvalidFileNameChars);
        var chars = trimmed
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();
        var sanitized = new string(chars).Trim('_', '.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? Strings.Label_DefaultProjectName : sanitized;
    }

    public static string NormalizeSitFilePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        var fileName = Path.GetFileName(fullPath);

        while (fileName.EndsWith(SitExtension, StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^SitExtension.Length];
        }

        fileName = SanitizeProjectName(fileName);
        return Path.Combine(directory, fileName + SitExtension);
    }

    public static string BuildSitPath(string directory, string projectName)
        => Path.Combine(directory, SanitizeProjectName(RemoveSitExtension(projectName)) + SitExtension);

    public static string GetProjectDisplayName(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        return RemoveSitExtension(Path.GetFileName(filePath));
    }

    private static string RemoveSitExtension(string fileName)
    {
        while (fileName.EndsWith(SitExtension, StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^SitExtension.Length];
        }

        return fileName;
    }
}
