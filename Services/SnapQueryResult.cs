using LiteCad.Core.Geometry;

namespace LiteCad.Services;

public readonly struct SnapQueryResult
{
    public SnapQueryResult(SnapResult best, IReadOnlyList<SnapPoint> visible)
    {
        Best = best;
        Visible = visible;
    }

    public SnapResult Best { get; }

    public IReadOnlyList<SnapPoint> Visible { get; }
}
