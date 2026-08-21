namespace LiteCad.Services;

using System.IO;

public static class PdfFileNameHelper
{
    public const string PdfExtension = ".pdf";

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public static bool IsPdfPath(string path)
        => Path.GetExtension(path).Equals(PdfExtension, StringComparison.OrdinalIgnoreCase);

    public static string NormalizePdfFilePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        var fileName = Path.GetFileName(fullPath);

        while (fileName.EndsWith(PdfExtension, StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^PdfExtension.Length];
        }

        fileName = SanitizeFileName(fileName);
        return Path.Combine(directory, fileName + PdfExtension);
    }

    private static string SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Drawing";
        }

        var trimmed = name.Trim();
        var invalid = new HashSet<char>(InvalidFileNameChars);
        var chars = trimmed
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();
        var sanitized = new string(chars).Trim('_', '.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? "Drawing" : sanitized;
    }
}
