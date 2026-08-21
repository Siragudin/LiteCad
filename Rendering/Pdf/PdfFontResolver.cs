using PdfSharp.Fonts;
using System.IO;

namespace LiteCad.Rendering.Pdf;

internal sealed class PdfFontResolver : IFontResolver
{
    private static readonly object Gate = new();
    private static bool _registered;

    public static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.FontResolver = new PdfFontResolver();
            }

            _registered = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var faceName = familyName.ToLowerInvariant() switch
        {
            "segoe ui" => "SegoeUI",
            _ => "SegoeUI"
        };

        if (isBold && isItalic)
        {
            faceName += "-BoldItalic";
        }
        else if (isBold)
        {
            faceName += "-Bold";
        }
        else if (isItalic)
        {
            faceName += "-Italic";
        }

        return new FontResolverInfo(faceName);
    }

    public byte[]? GetFont(string faceName)
    {
        var fileName = faceName switch
        {
            "SegoeUI" => "segoeui.ttf",
            "SegoeUI-Bold" => "segoeuib.ttf",
            "SegoeUI-Italic" => "segoeuii.ttf",
            "SegoeUI-BoldItalic" => "segoeuiz.ttf",
            _ => "segoeui.ttf"
        };

        var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), fileName);
        return File.Exists(fontPath) ? File.ReadAllBytes(fontPath) : null;
    }
}
