namespace LiteCad.Core.Geometry;

/// <summary>
/// World-space tolerance for topology mutation (vertex merge, split, collinearity, face rebuild).
/// Independent from zoom-based UI snap radius (<see cref="MathUtils.SnapToleranceWorld"/>).
/// </summary>
public static class TopologyTolerance
{
    public const double Default = MathUtils.DefaultTolerance;

    /// <summary>Fixed threshold for all topology mutation paths.</summary>
    public static double ForMutation => Default;

    /// <summary>
    /// Resolves a caller-supplied tolerance for non-mutation contexts (e.g. snap search radius).
    /// Does not widen topology decisions.
    /// </summary>
    public static double Resolve(double tolerance)
        => tolerance > 0 ? tolerance : Default;
}
