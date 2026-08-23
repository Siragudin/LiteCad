using LiteCad.Leaders;

namespace LiteCad.Services;

public sealed class LeaderToolOptions
{
    public LeaderKind Kind { get; set; } = LeaderKind.Elevation;

    public string Text { get; set; } = string.Empty;
}
