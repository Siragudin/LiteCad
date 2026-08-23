using LiteCad.Core.Document;
using LiteCad.Services;
using PdfSharp.Drawing;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.Rendering.Pdf;

internal static class WpfDrawingPdfConverter
{
    private const double DipToPoint = PdfExportLayout.PointsPerInch / PdfExportLayout.DipPerInch;

    public static void Draw(
        DrawingVisual visual,
        XGraphics graphics,
        PdfExportLayout layout,
        PdfSheet sheet,
        LinearDisplayUnit linearUnit)
    {
        var drawing = VisualTreeHelper.GetDrawing(visual) as DrawingGroup;
        if (drawing is null)
        {
            return;
        }

        graphics.Save();
        graphics.ScaleTransform(DipToPoint, DipToPoint);
        DrawGroup(drawing, graphics, Matrix.Identity, skipDimensionGlyphs: true, layout.ExportCamera.Zoom);
        PdfDimensionTextRenderer.Draw(graphics, sheet.Document, layout, sheet.PrimaryView, linearUnit);
        graphics.Restore();
    }

    private static void DrawGroup(
        DrawingGroup group,
        XGraphics graphics,
        Matrix parentTransform,
        bool skipDimensionGlyphs,
        double exportZoom)
    {
        var groupTransform = group.Transform?.Value ?? Matrix.Identity;
        var combinedTransform = Multiply(groupTransform, parentTransform);

        if (group.ClipGeometry is Geometry clipGeometry)
        {
            var clipPath = CreatePath(clipGeometry, parentTransform, GetFillRule(clipGeometry));
            if (clipPath is not null)
            {
                graphics.Save();
                graphics.IntersectClip(clipPath);
                DrawGroupChildren(group, graphics, combinedTransform, skipDimensionGlyphs, exportZoom);
                graphics.Restore();
                return;
            }
        }

        DrawGroupChildren(group, graphics, combinedTransform, skipDimensionGlyphs, exportZoom);
    }

    private static void DrawGroupChildren(
        DrawingGroup group,
        XGraphics graphics,
        Matrix transform,
        bool skipDimensionGlyphs,
        double exportZoom)
    {
        foreach (var child in group.Children)
        {
            switch (child)
            {
                case GeometryDrawing geometryDrawing:
                    DrawGeometryDrawing(geometryDrawing, graphics, transform, exportZoom);
                    break;
                case GlyphRunDrawing when skipDimensionGlyphs:
                    break;
                case GlyphRunDrawing glyphRunDrawing:
                    DrawGlyphRun(glyphRunDrawing, graphics, transform);
                    break;
                case DrawingGroup nestedGroup:
                    DrawGroup(nestedGroup, graphics, transform, skipDimensionGlyphs, exportZoom);
                    break;
            }
        }
    }

    private static void DrawGeometryDrawing(
        GeometryDrawing drawing,
        XGraphics graphics,
        Matrix transform,
        double exportZoom)
    {
        if (drawing.Geometry is null)
        {
            return;
        }

        var path = CreatePath(drawing.Geometry, transform);
        if (path is null)
        {
            return;
        }

        var uniformScale = GetUniformScale(transform);

        if (drawing.Brush is SolidColorBrush fillBrush)
        {
            graphics.DrawPath(null, CreateBrush(fillBrush), path);
        }

        if (drawing.Pen is Pen pen && pen.Brush is SolidColorBrush strokeBrush)
        {
            var strokeWidth = ResolveStrokeWidth(pen, uniformScale, exportZoom);
            graphics.DrawPath(CreatePen(pen, strokeBrush.Color, strokeWidth), null, path);
        }
    }

    private static double ResolveStrokeWidth(Pen pen, double uniformScale, double exportZoom)
    {
        if (PdfStyledSolidColorBrush.IsPdfExportPen(pen))
        {
            return pen.Thickness;
        }

        if (TryMapScreenPenToPhysicalDip(pen, exportZoom, out var physicalDip))
        {
            return physicalDip;
        }

        return ScaleStrokeWidth(pen.Thickness, uniformScale);
    }

    private static bool TryMapScreenPenToPhysicalDip(Pen pen, double exportZoom, out double thicknessDip)
    {
        thicknessDip = 0;
        if (exportZoom <= 1e-12)
        {
            return false;
        }

        var screenPx = pen.Thickness * exportZoom;
        var dashes = pen.DashStyle.Dashes;

        if (DashesEqual(dashes, 12, 4, 2, 4) && Approximately(screenPx, 1.5))
        {
            thicknessDip = PhysicalDip(PenStyle.Axis);
            return true;
        }

        if (DashesEqual(dashes, 6, 4) && Approximately(screenPx, 1.5))
        {
            thicknessDip = PhysicalDip(PenStyle.AxisEdge);
            return true;
        }

        if (dashes.Count == 0
            && pen.Brush is SolidColorBrush brush
            && IsDimensionExportColor(brush.Color))
        {
            if (Approximately(screenPx, 1.0))
            {
                thicknessDip = PhysicalDip(PenStyle.Extension);
                return true;
            }

            if (Approximately(screenPx, 1.5) || Approximately(screenPx, 2.0))
            {
                thicknessDip = PhysicalDip(PenStyle.Dimension);
                return true;
            }
        }

        return false;
    }

    private static double PhysicalDip(PenStyle style)
        => PdfLineweightTable.ToPoints(style) / DipToPoint;

    private static bool IsDimensionExportColor(Color color)
        => (color.R == 0x15 && color.G == 0x65 && color.B == 0xC0)
            || (color.R == 0x1E && color.G == 0x88 && color.B == 0xE5);

    private static bool DashesEqual(DoubleCollection dashes, params double[] expected)
    {
        if (dashes.Count != expected.Length)
        {
            return false;
        }

        for (var i = 0; i < expected.Length; i++)
        {
            if (Math.Abs(dashes[i] - expected[i]) > 1e-9)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Approximately(double actual, double expected)
        => Math.Abs(actual - expected) <= 0.05;

    private static void DrawGlyphRun(GlyphRunDrawing drawing, XGraphics graphics, Matrix transform)
    {
        if (drawing.GlyphRun is null || drawing.ForegroundBrush is not SolidColorBrush brush)
        {
            return;
        }

        var text = new string(drawing.GlyphRun.Characters.ToArray());
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var glyphRun = drawing.GlyphRun;
        var (uniformScale, angleDegrees) = DecomposeScaleAndRotation(transform);
        var alignmentBox = glyphRun.ComputeAlignmentBox();
        var localDrawPoint = new Point(
            glyphRun.BaselineOrigin.X + alignmentBox.X,
            glyphRun.BaselineOrigin.Y + alignmentBox.Y);
        var origin = transform.Transform(new Point(0, 0));
        var fontSize = Math.Max(4.0, glyphRun.FontRenderingEmSize * uniformScale);
        var font = new XFont("Segoe UI", fontSize, XFontStyleEx.Regular);
        var format = new XStringFormat
        {
            Alignment = XStringAlignment.Near,
            LineAlignment = XLineAlignment.Near
        };

        graphics.Save();
        graphics.TranslateTransform(origin.X, origin.Y);

        if (Math.Abs(angleDegrees) > 1e-6)
        {
            graphics.RotateTransform(angleDegrees);
        }

        graphics.DrawString(
            text,
            font,
            CreateBrush(brush),
            localDrawPoint.X * uniformScale,
            localDrawPoint.Y * uniformScale,
            format);
        graphics.Restore();
    }

    private static XGraphicsPath? CreatePath(Geometry geometry, Matrix transform, FillRule fillRule = FillRule.EvenOdd)
    {
        var flattened = geometry.GetFlattenedPathGeometry(0.25, ToleranceType.Absolute);
        var path = new XGraphicsPath
        {
            FillMode = fillRule == FillRule.EvenOdd ? XFillMode.Alternate : XFillMode.Winding
        };
        var hasFigure = false;

        foreach (var figure in flattened.Figures)
        {
            var current = transform.Transform(figure.StartPoint);
            path.StartFigure();
            path.AddLine(current.X, current.Y, current.X, current.Y);
            hasFigure = true;

            foreach (var segment in figure.Segments)
            {
                switch (segment)
                {
                    case LineSegment lineSegment:
                    {
                        var end = transform.Transform(lineSegment.Point);
                        path.AddLine(current.X, current.Y, end.X, end.Y);
                        current = end;
                        break;
                    }
                    case PolyLineSegment polyLineSegment:
                    {
                        foreach (var point in polyLineSegment.Points)
                        {
                            var end = transform.Transform(point);
                            path.AddLine(current.X, current.Y, end.X, end.Y);
                            current = end;
                        }

                        break;
                    }
                }
            }

            if (figure.IsClosed)
            {
                path.CloseFigure();
            }
        }

        return hasFigure ? path : null;
    }

    private static FillRule GetFillRule(Geometry geometry)
        => geometry switch
        {
            PathGeometry pathGeometry => pathGeometry.FillRule,
            StreamGeometry streamGeometry => streamGeometry.FillRule,
            _ => FillRule.EvenOdd
        };

    private static XPen CreatePen(Pen pen, Color color, double strokeWidth)
    {
        var xPen = new XPen(ToXColor(color), strokeWidth);
        if (pen.DashStyle.Dashes.Count > 0 && pen.Thickness > 1e-9)
        {
            xPen.DashStyle = XDashStyle.Custom;
            xPen.DashPattern = pen.DashStyle.Dashes.ToArray();
            xPen.DashOffset = pen.DashStyle.Offset;
        }

        return xPen;
    }

    private static double ScaleStrokeWidth(double penThickness, double uniformScale)
        => Math.Max(0.25, penThickness * uniformScale);

    private static double GetUniformScale(Matrix matrix)
    {
        var scaleX = Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12);
        var scaleY = Math.Sqrt(matrix.M21 * matrix.M21 + matrix.M22 * matrix.M22);
        return Math.Max(scaleX, scaleY);
    }

    private static (double Scale, double AngleDegrees) DecomposeScaleAndRotation(Matrix matrix)
    {
        var scaleX = Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12);
        var scaleY = Math.Sqrt(matrix.M21 * matrix.M21 + matrix.M22 * matrix.M22);
        var scale = Math.Max(scaleX, scaleY);
        if (scaleX <= 1e-9)
        {
            return (scale, 0.0);
        }

        var angle = Math.Atan2(matrix.M12, matrix.M11) * 180.0 / Math.PI;
        return (scale, angle);
    }

    private static XBrush CreateBrush(SolidColorBrush brush)
        => new XSolidBrush(ToXColor(brush.Color));

    private static XColor ToXColor(Color color)
        => XColor.FromArgb(color.A, color.R, color.G, color.B);

    private static Matrix Multiply(Matrix left, Matrix right)
        => left * right;
}
