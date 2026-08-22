namespace LiteCad.Rendering.Pdf;

public static class PdfAnnotationTable
{
    public const double DimensionTextHeightMm = 2.5;

    public const double TextGapMm = 1.0;

    public static double DimensionTextHeightPoints
        => DimensionTextHeightMm * PdfExportLayout.PointsPerInch / PdfExportLayout.MmPerInch;

    public static double TextGapPoints
        => TextGapMm * PdfExportLayout.PointsPerInch / PdfExportLayout.MmPerInch;
}
