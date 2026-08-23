using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Leaders;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering;

public static class LeaderAnnotationDrawing
{
    private static readonly Typeface ItalicTypeface = new(
        new FontFamily("Segoe UI"),
        FontStyles.Italic,
        FontWeights.Normal,
        FontStretches.Normal);

    private const double LineThicknessScreen = 1.5;

    private const double SelectedLineThicknessScreen = 2.0;

    public static void Draw(
        DrawingContext context,
        Leader leader,
        double zoom,
        Color color,
        Camera camera,
        Size viewport,
        bool isSelected)
    {
        var brush = new SolidColorBrush(color);
        var pen = RenderStyles.CreateScreenPen(
            brush,
            isSelected ? SelectedLineThicknessScreen : LineThicknessScreen,
            zoom);
        var layout = LeaderGeometry.CreateLayout(leader.Target, leader.TextPosition, zoom);

        foreach (var (start, end) in LeaderGeometry.GetSegments(layout))
        {
            context.DrawLine(pen, ToPoint(start), ToPoint(end));
        }

        DrawText(context, leader.Text, layout, color, camera, viewport, zoom);
    }

    private static void DrawText(
        DrawingContext context,
        string text,
        LeaderLayout layout,
        Color color,
        Camera camera,
        Size viewport,
        double zoom)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var screenFontSize = Dimension.NormalizeTextSize(Dimension.DefaultTextSize) * zoom;
        var formattedText = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            ItalicTypeface,
            screenFontSize,
            new SolidColorBrush(color),
            GetPixelsPerDip());

        var elbowScreen = camera.WorldToScreen(layout.Elbow, viewport);
        var landingScreen = camera.WorldToScreen(layout.LandingEnd, viewport);
        var midX = (elbowScreen.X + landingScreen.X) * 0.5;
        var shelfY = (elbowScreen.Y + landingScreen.Y) * 0.5;
        var originX = midX - formattedText.Width * 0.5;
        var originY = shelfY - formattedText.Height - LeaderGeometry.TextGapAboveShelfScreen;

        context.PushTransform(new MatrixTransform(camera.GetScreenToWorldMatrix(viewport)));
        context.PushTransform(new TranslateTransform(originX, originY));
        context.DrawText(formattedText, new Point(0, 0));
        context.Pop();
        context.Pop();
    }

    private static Point ToPoint(PointF point)
        => new(point.X, point.Y);

    private static double GetPixelsPerDip()
    {
        try
        {
            return VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip;
        }
        catch
        {
            return 1.0;
        }
    }
}
