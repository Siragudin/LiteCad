using LiteCad.Core.Geometry;

using LiteCad.Dimensions;

using System.Globalization;

using System.Windows;

using System.Windows.Media;



namespace LiteCad.Rendering;



public static class DimensionAnnotationDrawing

{

    private const double TextScreenHeight = 18.0;

    private const double TextGapScreen = 12.0;

    private const double TickHalfLengthScreen = 5.0;

    private const double ExtensionThicknessScreen = 1.0;

    private const double DimensionLineThicknessScreen = 1.5;

    private const double SelectedDimensionLineThicknessScreen = 2.0;

    private const double ShortExtensionFraction = 0.25;



    public static void Draw(

        DrawingContext context,

        DimensionLayout layout,

        double offset,

        double zoom,

        Color color,

        bool isSelected,

        Camera camera,

        Size viewport,

        string? distanceText = null,

        DimensionExtensionStyle extensionStyle = DimensionExtensionStyle.Full)

    {

        var brush = new SolidColorBrush(color);

        var extensionPen = RenderStyles.CreateScreenPen(brush, ExtensionThicknessScreen, zoom);

        var dimensionPen = RenderStyles.CreateScreenPen(

            brush,

            isSelected ? SelectedDimensionLineThicknessScreen : DimensionLineThicknessScreen,

            zoom);



        DrawExtensionLine(
            context,
            extensionPen,
            layout.FirstAnchor,
            layout.FirstExtensionEnd,
            extensionStyle);
        DrawExtensionLine(
            context,
            extensionPen,
            layout.SecondAnchor,
            layout.SecondExtensionEnd,
            extensionStyle);

        context.DrawLine(

            dimensionPen,

            ToPoint(layout.DimensionLineStart),

            ToPoint(layout.DimensionLineEnd));



        DrawTick(context, layout.FirstExtensionEnd, layout.TextAngleRadians, zoom, dimensionPen);

        DrawTick(context, layout.SecondExtensionEnd, layout.TextAngleRadians, zoom, dimensionPen);



        var text = distanceText ?? layout.MeasuredDistance.ToString("F2", CultureInfo.InvariantCulture);

        DrawDistanceText(context, color, text, camera, viewport, layout);

    }



    private static void DrawExtensionLine(
        DrawingContext context,
        Pen pen,
        PointF anchor,
        PointF extensionEnd,
        DimensionExtensionStyle extensionStyle)
    {
        var start = extensionStyle == DimensionExtensionStyle.Full
            ? anchor
            : GetShortExtensionStart(anchor, extensionEnd);
        context.DrawLine(pen, ToPoint(start), ToPoint(extensionEnd));
    }

    private static PointF GetShortExtensionStart(PointF anchor, PointF extensionEnd)
    {
        var dx = extensionEnd.X - anchor.X;
        var dy = extensionEnd.Y - anchor.Y;
        var fullLength = Math.Sqrt(dx * dx + dy * dy);
        if (fullLength <= 1e-9)
        {
            return extensionEnd;
        }

        return new PointF(
            (float)(extensionEnd.X - dx * ShortExtensionFraction),
            (float)(extensionEnd.Y - dy * ShortExtensionFraction));
    }

    private static void DrawTick(

        DrawingContext context,

        PointF center,

        double dimensionAngleRadians,

        double zoom,

        Pen pen)

    {

        var tickAngle = dimensionAngleRadians + Math.PI * 0.25;

        var halfLength = TickHalfLengthScreen / zoom;

        var dx = Math.Cos(tickAngle) * halfLength;

        var dy = Math.Sin(tickAngle) * halfLength;

        context.DrawLine(

            pen,

            new Point(center.X - dx, center.Y - dy),

            new Point(center.X + dx, center.Y + dy));

    }



    private static void DrawDistanceText(
        DrawingContext context,
        Color color,
        string text,
        Camera camera,
        Size viewport,
        DimensionLayout layout)
    {
        var startScreen = camera.WorldToScreen(layout.DimensionLineStart, viewport);
        var endScreen = camera.WorldToScreen(layout.DimensionLineEnd, viewport);

        var dx = endScreen.X - startScreen.X;
        var dy = endScreen.Y - startScreen.Y;

        var center = new Point(
            (startScreen.X + endScreen.X) * 0.5,
            (startScreen.Y + endScreen.Y) * 0.5);

        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length > 1e-9)
        {
            var nx = -dy / length;
            var ny = dx / length;
            if (ny > 0)
            {
                nx = -nx;
                ny = -ny;
            }

            center = new Point(
                center.X + nx * TextGapScreen,
                center.Y + ny * TextGapScreen);
        }

        double angle;
        if (Math.Abs(dy) < Math.Abs(dx) * 0.01)
        {
            // HORIZONTAL: always left-to-right, no rotation.
            angle = 0.0;
        }
        else if (Math.Abs(dx) < Math.Abs(dy) * 0.01)
        {
            // VERTICAL: read bottom-to-top regardless of line direction.
            angle = -Math.PI / 2;
        }
        else
        {
            // DIAGONAL: existing logic (unchanged).
            angle = Math.Atan2(dy, dx);
            if (angle > Math.PI / 2 || angle <= -Math.PI / 2)
            {
                angle += Math.PI;
            }
        }

        var formattedText = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            TextScreenHeight,
            new SolidColorBrush(color),
            1.0)
        {
            TextAlignment = TextAlignment.Center
        };

        // Cancel the outer world-to-screen transform so text is drawn in viewport pixels.
        context.PushTransform(new MatrixTransform(camera.GetScreenToWorldMatrix(viewport)));
        context.PushTransform(new TranslateTransform(center.X, center.Y));
        context.PushTransform(new RotateTransform(angle * 180.0 / Math.PI));
        context.DrawText(
            formattedText,
            new Point(
                -formattedText.Width * 0.5,
                -formattedText.Height * 0.5));
        context.Pop();
        context.Pop();
        context.Pop();
    }



    private static Point ToPoint(PointF point)

        => new(point.X, point.Y);

}


