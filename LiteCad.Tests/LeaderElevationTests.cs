using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Leaders;
using LiteCad.Resources;
using LiteCad.Services;
using Xunit;

namespace LiteCad.Tests;

public class LeaderElevationTests
{
    [Theory]
    [InlineData(0, 900, "+900")]
    [InlineData(0, 3200, "+3200")]
    [InlineData(0, -300, "-300")]
    [InlineData(0, 0, "0")]
    [InlineData(1500, 2400, "+900")]
    [InlineData(1500, 1200, "-300")]
    public void Elevation_UsesModelYUpWithoutInvertingSign(double baseY, double currentY, string expected)
    {
        var delta = ElevationFormatting.ComputeDelta(baseY, currentY);
        Assert.Equal(expected, ElevationFormatting.Format(delta));
    }

    [Theory]
    [InlineData(900, "+900")]
    [InlineData(3200, "+3200")]
    [InlineData(0, "0")]
    [InlineData(-300, "-300")]
    public void Format_RequiresExplicitSign(double delta, string expected)
    {
        Assert.Equal(expected, ElevationFormatting.Format(delta));
        if (delta > 0)
        {
            Assert.StartsWith("+", expected);
        }
    }

    [Fact]
    public void Camera_ModelYIncreasesUpward_SoHigherYIsPositiveElevation()
    {
        var camera = new LiteCad.Rendering.Camera();
        var viewport = new System.Windows.Size(200, 200);
        var low = camera.WorldToScreen(new PointF(0, 0), viewport);
        var high = camera.WorldToScreen(new PointF(0, 900), viewport);
        Assert.True(high.Y < low.Y);
        Assert.Equal("+900", ElevationFormatting.Format(ElevationFormatting.ComputeDelta(0, 900)));
    }

    [Fact]
    public void FirstElevation_StoresDocumentBaseYAndFormatsZero()
    {
        var document = new CadDocument();
        var leader = LeaderService.CreateElevation(
            document,
            new PointF(10, 1500),
            new PointF(80, 1500));

        Assert.Equal(1500, document.ElevationBaseY);
        Assert.Equal("0", leader.Text);
        Assert.Equal(LeaderKind.Elevation, leader.Kind);
    }

    [Fact]
    public void SubsequentElevation_UsesStoredBase()
    {
        var document = new CadDocument();
        LeaderService.CreateElevation(document, new PointF(0, 1500), new PointF(40, 1500));
        var next = LeaderService.CreateElevation(document, new PointF(0, 2400), new PointF(40, 2400));
        Assert.Equal(1500, document.ElevationBaseY);
        Assert.Equal("+900", next.Text);
    }

    [Fact]
    public void Undo_DoesNotClearBaseWhenLaterElevationIsRemoved()
    {
        var session = new CadSession();
        session.History.Record(session.Document);
        LeaderService.CreateElevation(session.Document, new PointF(0, 0), new PointF(40, 0));
        session.History.Record(session.Document);
        LeaderService.CreateElevation(session.Document, new PointF(0, 900), new PointF(40, 900));

        session.History.Undo(session.Document, 1e-4);
        Assert.Equal(0, session.Document.ElevationBaseY);
        Assert.Single(session.Document.Leaders);
        Assert.Equal("0", session.Document.Leaders[0].Text);
    }

    [Fact]
    public void Layout_FacesRightWhenLandingIsToTheRight()
    {
        var layout = LeaderGeometry.CreateLayout(new PointF(0, 0), new PointF(80, 10), zoom: 1);
        Assert.Equal(1, layout.Side);
        Assert.True(layout.LandingEnd.X > layout.Target.X);
        Assert.Equal(layout.Target.X, layout.Elbow.X);
        Assert.Equal(LeaderGeometry.StemHeightScreen, layout.Elbow.Y - layout.Target.Y, 6);
        Assert.Equal(LeaderGeometry.ShelfLengthScreen, layout.LandingEnd.X - layout.Elbow.X, 6);
        Assert.Equal((layout.Elbow.X + layout.LandingEnd.X) * 0.5, layout.TextPosition.X, 6);
    }

    [Fact]
    public void TextHeight_FitsTheFixedShelf()
    {
        Assert.True(LeaderGeometry.TextHeightScreen > 0);
        Assert.True(LeaderGeometry.TextHeightScreen < LeaderGeometry.ShelfLengthScreen);
        Assert.Equal(LeaderGeometry.ShelfLengthScreen / 3.0, LeaderGeometry.TextHeightScreen, 6);
    }

    [Fact]
    public void Layout_FacesLeftWhenLandingIsToTheLeft()
    {
        var layout = LeaderGeometry.CreateLayout(new PointF(0, 0), new PointF(-80, 10), zoom: 1);
        Assert.Equal(-1, layout.Side);
        Assert.True(layout.LandingEnd.X < layout.Target.X);
        Assert.Equal(LeaderGeometry.ShelfLengthScreen, layout.Elbow.X - layout.LandingEnd.X, 6);
        Assert.Equal(LeaderGeometry.StemHeightScreen, layout.Elbow.Y - layout.Target.Y, 6);
    }

    [Fact]
    public void Layout_DoesNotStretchShelfOrStemWithDragDistance()
    {
        var near = LeaderGeometry.CreateLayout(new PointF(10, 20), new PointF(12, 21), zoom: 1);
        var far = LeaderGeometry.CreateLayout(new PointF(10, 20), new PointF(400, 900), zoom: 1);
        Assert.Equal(near.LandingEnd.X - near.Elbow.X, far.LandingEnd.X - far.Elbow.X, 6);
        Assert.Equal(near.Elbow.Y - near.Target.Y, far.Elbow.Y - far.Target.Y, 6);
        Assert.Equal(near.Target, far.Target);
    }

    [Fact]
    public void CreateElevation_PlacesGraphicAtDropPointAndKeepsMeasureY()
    {
        var document = new CadDocument();
        var leader = LeaderService.CreateElevation(
            document,
            new PointF(0, 1500),
            new PointF(120, 1480));

        Assert.Equal(1500, document.ElevationBaseY);
        Assert.Equal("0", leader.Text);
        Assert.Equal(120, leader.Target.X);
        Assert.Equal(1500, leader.Target.Y);
    }

    [Fact]
    public void ConstrainHorizontal_IgnoresVerticalDrag()
    {
        var placed = LeaderGeometry.ConstrainHorizontal(new PointF(10, 1500), new PointF(80, 2100));
        Assert.Equal(80, placed.X);
        Assert.Equal(1500, placed.Y);
    }

    [Fact]
    public void SerializeRoundTrip_PreservesLeadersAndBase()
    {
        var document = new CadDocument();
        LeaderService.CreateElevation(document, new PointF(0, 100), new PointF(80, 100));
        LeaderService.CreateElevation(document, new PointF(0, 1000), new PointF(-80, 1000));

        var json = ProjectDocumentSerializer.Serialize(document, LinearDisplayUnit.Millimeters);
        var loaded = new CadDocument();
        ProjectDocumentSerializer.Apply(loaded, ProjectDocumentSerializer.Deserialize(json));

        Assert.Equal(2, loaded.Leaders.Count);
        Assert.Equal(100, loaded.ElevationBaseY);
        Assert.Equal("0", loaded.Leaders[0].Text);
        Assert.Equal("+900", loaded.Leaders[1].Text);
        Assert.True(loaded.Leaders[1].TextPosition.X < loaded.Leaders[1].Target.X);
    }

    [Fact]
    public void Export_WritesLeaderToPdf()
    {
        WpfTestUtilities.RunSta(() =>
        {
            var document = new CadDocument();
            TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(200, 0));
            LeaderService.CreateElevation(document, new PointF(0, 0), new PointF(80, 40));
            LeaderService.CreateElevation(document, new PointF(0, 900), new PointF(-80, 940));
            var path = Path.Combine(Path.GetTempPath(), "LiteCadLeaderPdf", Guid.NewGuid().ToString("N") + ".pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            LiteCad.Rendering.Pdf.PdfExporter.Export(document, LinearDisplayUnit.Millimeters, new LiteCad.Rendering.Renderer(), path);
            Assert.True(File.Exists(path));
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 512);
            File.Delete(path);
        });
    }

    [Fact]
    public void Localization_HasRussianAndEnglishLeaderStrings()
    {
        LocalizationManager.Instance.Initialize(AppLanguage.English);
        Assert.Equal("Leader", Strings.Tool_Leader);
        Assert.Equal("Elevation", Strings.Label_Elevation);
        Assert.Equal("Set Zero Elevation", Strings.Input_Leader_SetZero);

        LocalizationManager.Instance.SetLanguage(AppLanguage.Russian, persist: false);
        Assert.Equal("Выноска", Strings.Tool_Leader);
        Assert.Equal("Высотная отметка", Strings.Label_Elevation);
        Assert.Equal("Установить нулевую отметку", Strings.Input_Leader_SetZero);
        LocalizationManager.Instance.Initialize(AppLanguage.English);
    }
}
