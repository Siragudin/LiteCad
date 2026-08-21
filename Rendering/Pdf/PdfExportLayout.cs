using LiteCad.Core.Document;
using LiteCad.Services;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering.Pdf;

public sealed class PdfExportLayout
{
    public const double A4WidthMm = 210.0;

    public const double A4HeightMm = 297.0;

    public const double MarginMm = 12.0;

    public const double PointsPerInch = 72.0;

    public const double MmPerInch = 25.4;

    public const double DipPerInch = 96.0;

    private const int BoundsRefinementIterations = 4;

    private const double PrintableAreaToleranceDip = 1.0;

    private PdfExportLayout(
        Rect worldBounds,
        bool isLandscape,
        double pageWidthPoints,
        double pageHeightPoints,
        double marginPoints,
        Size contentSizeDip,
        Camera exportCamera,
        double requiredZoom,
        double zoomCompensation)
    {
        WorldBounds = worldBounds;
        IsLandscape = isLandscape;
        PageWidthPoints = pageWidthPoints;
        PageHeightPoints = pageHeightPoints;
        MarginPoints = marginPoints;
        ContentSizeDip = contentSizeDip;
        ExportCamera = exportCamera;
        RequiredZoom = requiredZoom;
        ZoomCompensation = zoomCompensation;
    }

    public Rect WorldBounds { get; }

    public bool IsLandscape { get; }

    public double PageWidthPoints { get; }

    public double PageHeightPoints { get; }

    public double MarginPoints { get; }

    public Size ContentSizeDip { get; }

    public Camera ExportCamera { get; }

    public double RequiredZoom { get; }

    public double ZoomCompensation { get; }

    public bool NeedsZoomCompensation => Math.Abs(ZoomCompensation - 1.0) > 1e-12;

    public double PageWidthDip => PointsToDip(PageWidthPoints);

    public double PageHeightDip => PointsToDip(PageHeightPoints);

    public double MarginDip => PointsToDip(MarginPoints);

    public double ContentWidthMm => MmFromPoints(PageWidthPoints - MarginPoints * 2);

    public double ContentHeightMm => MmFromPoints(PageHeightPoints - MarginPoints * 2);

    public double Scale => RequiredZoom;

    public static PdfExportLayout Create(CadDocument document, LinearDisplayUnit linearUnit = LinearDisplayUnit.Millimeters)
    {
        var provisionalBounds = DocumentBoundsCalculator.ComputeWorldBounds(document, linearUnit: linearUnit);
        var isLandscape = provisionalBounds.Width > provisionalBounds.Height;

        var pageWidthMm = isLandscape ? A4HeightMm : A4WidthMm;
        var pageHeightMm = isLandscape ? A4WidthMm : A4HeightMm;

        var pageWidthPoints = MmToPoints(pageWidthMm);
        var pageHeightPoints = MmToPoints(pageHeightMm);
        var marginPoints = MmToPoints(MarginMm);

        var contentWidthDip = PointsToDip(pageWidthPoints - marginPoints * 2);
        var contentHeightDip = PointsToDip(pageHeightPoints - marginPoints * 2);
        var contentSize = new Size(contentWidthDip, contentHeightDip);

        var worldBounds = provisionalBounds;
        var requiredZoom = ComputeRequiredZoom(worldBounds, contentSize);
        for (var iteration = 0; iteration < BoundsRefinementIterations; iteration++)
        {
            worldBounds = DocumentBoundsCalculator.ComputeWorldBounds(document, requiredZoom, linearUnit);
            requiredZoom = ComputeRequiredZoom(worldBounds, contentSize);
        }

        var exportCamera = new Camera();
        exportCamera.FitWorldBounds(
            worldBounds.X,
            worldBounds.Y,
            worldBounds.Right,
            worldBounds.Bottom,
            contentSize,
            paddingPixels: 0);

        var zoomCompensation = ComputeZoomCompensation(requiredZoom, exportCamera.Zoom);

        var layout = new PdfExportLayout(
            worldBounds,
            isLandscape,
            pageWidthPoints,
            pageHeightPoints,
            marginPoints,
            contentSize,
            exportCamera,
            requiredZoom,
            zoomCompensation);

        layout.EnsureFitsPrintableArea();
        return layout;
    }

    public Matrix GetEffectiveWorldToContentMatrix()
    {
        var matrix = ExportCamera.GetWorldToScreenMatrix(ContentSizeDip);
        if (!NeedsZoomCompensation)
        {
            return matrix;
        }

        var centerX = ContentSizeDip.Width / 2.0;
        var centerY = ContentSizeDip.Height / 2.0;
        var compensation = Matrix.Identity;
        compensation.ScaleAt(ZoomCompensation, ZoomCompensation, centerX, centerY);
        return compensation * matrix;
    }

    public Point TransformWorldToContent(Point worldPoint)
    {
        var transformed = ExportCamera.GetWorldToScreenMatrix(ContentSizeDip).Transform(worldPoint);
        if (!NeedsZoomCompensation)
        {
            return transformed;
        }

        var centerX = ContentSizeDip.Width / 2.0;
        var centerY = ContentSizeDip.Height / 2.0;
        return new Point(
            centerX + (transformed.X - centerX) * ZoomCompensation,
            centerY + (transformed.Y - centerY) * ZoomCompensation);
    }

    public Rect GetTransformedBoundsInContentDip()
        => TransformBounds(WorldBounds, TransformWorldToContent);

    internal void EnsureFitsPrintableArea()
    {
        var transformed = GetTransformedBoundsInContentDip();
        if (transformed.Left < -PrintableAreaToleranceDip
            || transformed.Top < -PrintableAreaToleranceDip
            || transformed.Right > ContentSizeDip.Width + PrintableAreaToleranceDip
            || transformed.Bottom > ContentSizeDip.Height + PrintableAreaToleranceDip)
        {
            throw new InvalidOperationException(
                $"PDF export content exceeds printable area: {transformed} in {ContentSizeDip}.");
        }
    }

    internal static double ComputeRequiredZoom(Rect worldBounds, Size contentViewportDip)
    {
        var worldWidth = Math.Max(worldBounds.Width, 1e-6);
        var worldHeight = Math.Max(worldBounds.Height, 1e-6);
        var zoomX = contentViewportDip.Width / worldWidth;
        var zoomY = contentViewportDip.Height / worldHeight;
        return Math.Min(zoomX, zoomY);
    }

    internal static double ComputeZoomCompensation(double requiredZoom, double actualZoom)
    {
        if (requiredZoom >= Camera.MinZoom)
        {
            return 1.0;
        }

        return requiredZoom / Math.Max(actualZoom, 1e-12);
    }

    internal static Rect TransformBounds(Rect worldBounds, Func<Point, Point> transformPoint)
    {
        var points = new[]
        {
            transformPoint(new Point(worldBounds.Left, worldBounds.Top)),
            transformPoint(new Point(worldBounds.Right, worldBounds.Top)),
            transformPoint(new Point(worldBounds.Right, worldBounds.Bottom)),
            transformPoint(new Point(worldBounds.Left, worldBounds.Bottom))
        };

        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxY = points.Max(point => point.Y);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    internal static Rect TransformBounds(Rect worldBounds, Matrix matrix)
        => TransformBounds(worldBounds, point => matrix.Transform(point));

    public static double MmToPoints(double millimeters)
        => millimeters / MmPerInch * PointsPerInch;

    public static double MmFromPoints(double points)
        => points / PointsPerInch * MmPerInch;

    public static double PointsToDip(double points)
        => points * DipPerInch / PointsPerInch;
}
