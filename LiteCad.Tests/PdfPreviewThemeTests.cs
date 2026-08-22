using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Services;
using LiteCad.UI.Pdf;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class PdfPreviewThemeTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "LiteCadPdfPreviewThemeTests_" + Guid.NewGuid().ToString("N"));

    public PdfPreviewThemeTests()
    {
        Directory.CreateDirectory(_tempRoot);
        ThemeManager.Instance.Initialize(AppTheme.Dark);
    }

    public void Dispose()
    {
        ThemeManager.Instance.Initialize(AppTheme.Light);
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void PdfPreviewWindow_UsesLightPalette_WhenApplicationThemeIsDark()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var window = CreatePreviewWindow(document);
            window.Show();
            window.UpdateLayout();

            AssertLightPreviewChrome(window);

            window.Close();
        });
    }

    [Fact]
    public void PdfPreviewWindow_LooksSameInLightApplicationTheme()
    {
        ThemeManager.Instance.SetTheme(AppTheme.Light);

        WpfTestUtilities.RunSta(() =>
        {
            var document = CreateSquareDocument();
            var window = CreatePreviewWindow(document);
            window.Show();
            window.UpdateLayout();

            AssertLightPreviewChrome(window);

            window.Close();
        });
    }

    private static void AssertLightPreviewChrome(PdfPreviewWindow window)
    {
        var background = Assert.IsType<SolidColorBrush>(window.Background);
        Assert.Equal(Color.FromRgb(0xF0, 0xF0, 0xF0), background.Color);

        var orientationCombo = window.FindName("OrientationCombo") as ComboBox;
        Assert.NotNull(orientationCombo);
        AssertLightBrush(orientationCombo.Background);

        var cancelButton = window.FindName("CancelButton") as Button;
        Assert.NotNull(cancelButton);
        AssertLightBrush(cancelButton.Background);
    }

    private static void AssertLightBrush(Brush? brush)
    {
        var solid = Assert.IsType<SolidColorBrush>(brush);
        Assert.True(solid.Color.R >= 240 && solid.Color.G >= 240 && solid.Color.B >= 240, $"Expected light brush, got {solid.Color}");
    }

    private static PdfPreviewWindow CreatePreviewWindow(CadDocument document)
        => new(
            document,
            LinearDisplayUnit.Millimeters,
            new Renderer(),
            Path.Combine(Path.GetTempPath(), "preview.pdf"),
            null);

    private static CadDocument CreateSquareDocument()
    {
        const double tolerance = 1e-4;
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(100, 100), tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 100), new PointF(0, 100), tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 100), new PointF(0, 0), tolerance);
        PolygonBuilder.SyncFaces(document, tolerance);
        return document;
    }
}
