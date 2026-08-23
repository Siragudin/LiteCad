using LiteCad.Texts;

namespace LiteCad.Services;

public sealed class TextToolOptions
{
    public TextNoteKind Kind { get; set; } = TextNoteKind.Plain;

    public double TextSize { get; set; } = TextNote.DefaultTextSize;
}
