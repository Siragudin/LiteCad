namespace LiteCad.Rendering.Pdf;

public enum PenStyle
{
    Axis,
    AxisEdge,
    Edge,
    EdgeDashed,
    EdgeDotted,
    Dimension,
    Extension
}

public static class PdfLineweightTable
{
    public const double AxisMm = 0.25;
    public const double AxisEdgeMm = 0.25;
    public const double EdgeMm = 0.18;
    public const double EdgeDashedMm = 0.18;
    public const double EdgeDottedMm = 0.13;
    public const double DimensionMm = 0.13;
    public const double ExtensionMm = 0.13;

    public const double MinVisiblePrintThicknessMm = 0.1;

    private const double PointsPerMm = PdfExportLayout.PointsPerInch / PdfExportLayout.MmPerInch;

    public static double MinVisiblePrintThicknessPoints
        => MinVisiblePrintThicknessMm * PointsPerMm;

    public static double ToMillimeters(PenStyle style)
        => style switch
        {
            PenStyle.Axis => AxisMm,
            PenStyle.AxisEdge => AxisEdgeMm,
            PenStyle.Edge => EdgeMm,
            PenStyle.EdgeDashed => EdgeDashedMm,
            PenStyle.EdgeDotted => EdgeDottedMm,
            PenStyle.Dimension => DimensionMm,
            PenStyle.Extension => ExtensionMm,
            _ => EdgeMm
        };

    public static double ToPoints(PenStyle style)
        => ToMillimeters(style) * PointsPerMm;
}
