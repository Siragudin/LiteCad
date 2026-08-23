using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Services;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class FaceFillTests
{
    private const double Tolerance = 1e-4;

    [Fact]
    public void Face_DefaultFillPattern_IsSolid()
    {
        var document = CreateSquareDocument();
        var face = Assert.Single(document.Polygons);

        var fill = FaceFillService.GetFill(document, face);

        Assert.Equal(FaceFillPattern.Solid, fill.FillPattern);
    }

    [Fact]
    public void Face_DefaultFillColor_IsLightGray()
    {
        var document = CreateSquareDocument();
        var face = Assert.Single(document.Polygons);

        var fill = FaceFillService.GetFill(document, face);

        Assert.Equal(FaceFillStyle.Default.FillColor, fill.FillColor);
    }

    [Fact]
    public void ChangingFillColor_DoesNotChangeTopology()
    {
        var document = CreateSquareDocument();
        var face = Assert.Single(document.Polygons);
        var countsBefore = GetTopologyCounts(document);

        Assert.True(FaceFillService.TrySetFillColor(document, face, Colors.Red));

        Assert.Equal(countsBefore, GetTopologyCounts(document));
        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void ChangingFillPattern_DoesNotChangeTopology()
    {
        var document = CreateSquareDocument();
        var face = Assert.Single(document.Polygons);
        var countsBefore = GetTopologyCounts(document);

        Assert.True(FaceFillService.TrySetFillPattern(document, face, FaceFillPattern.Diagonal));

        Assert.Equal(countsBefore, GetTopologyCounts(document));
        TopologyValidator.AssertValid(document, Tolerance);
    }

    [Fact]
    public void FillColor_IsPersistedInDocument()
    {
        var document = CreateSquareDocument();
        var face = Assert.Single(document.Polygons);
        var key = FaceIdentity.Create(document, face);

        Assert.True(FaceFillService.TrySetFillColor(document, face, Colors.Blue));

        Assert.True(document.FaceFillStyles.TryGetValue(key, out var style));
        Assert.Equal(Colors.Blue, style.FillColor);
    }

    [Fact]
    public void FillPattern_IsPersistedInDocument()
    {
        var document = CreateSquareDocument();
        var face = Assert.Single(document.Polygons);
        var key = FaceIdentity.Create(document, face);

        Assert.True(FaceFillService.TrySetFillPattern(document, face, FaceFillPattern.Diagonal));

        Assert.True(document.FaceFillStyles.TryGetValue(key, out var style));
        Assert.Equal(FaceFillPattern.Diagonal, style.FillPattern);
    }

    [Fact]
    public void FillPattern_WideDiagonal_IsPersistedInDocument()
    {
        var document = CreateSquareDocument();
        var face = Assert.Single(document.Polygons);
        var key = FaceIdentity.Create(document, face);

        Assert.True(FaceFillService.TrySetFillPattern(document, face, FaceFillPattern.DiagonalWide));

        Assert.True(document.FaceFillStyles.TryGetValue(key, out var style));
        Assert.Equal(FaceFillPattern.DiagonalWide, style.FillPattern);
    }

    [Fact]
    public void WideDiagonalHatch_IsThreeTimesCoarserThanDiagonal()
    {
        Assert.Equal(
            FaceFillRenderer.GetHatchSpacingScreenPixels(FaceFillPattern.Diagonal) * 3,
            FaceFillRenderer.GetHatchSpacingScreenPixels(FaceFillPattern.DiagonalWide));
    }

    [Fact]
    public void FillColor_UndoRedo_RestoresPreviousValue()
    {
        var session = CreateSquareSession();
        var document = session.Document;
        var face = Assert.Single(document.Polygons);

        session.History.Record(document);
        Assert.True(FaceFillService.TrySetFillColor(document, face, Colors.Red));
        Assert.Equal(Colors.Red, FaceFillService.GetFill(document, face).FillColor);

        Assert.True(session.History.Undo(document, Tolerance));
        Assert.Equal(FaceFillStyle.Default.FillColor, FaceFillService.GetFill(document, face).FillColor);

        Assert.True(session.History.Redo(document, Tolerance));
        Assert.Equal(Colors.Red, FaceFillService.GetFill(document, face).FillColor);
    }

    [Fact]
    public void FillPattern_UndoRedo_RestoresPreviousValue()
    {
        var session = CreateSquareSession();
        var document = session.Document;
        var face = Assert.Single(document.Polygons);

        session.History.Record(document);
        Assert.True(FaceFillService.TrySetFillPattern(document, face, FaceFillPattern.Diagonal));
        Assert.Equal(FaceFillPattern.Diagonal, FaceFillService.GetFill(document, face).FillPattern);

        Assert.True(session.History.Undo(document, Tolerance));
        Assert.Equal(FaceFillPattern.Solid, FaceFillService.GetFill(document, face).FillPattern);

        Assert.True(session.History.Redo(document, Tolerance));
        Assert.Equal(FaceFillPattern.Diagonal, FaceFillService.GetFill(document, face).FillPattern);
    }

    [Fact]
    public void DeletingFace_RemovesFillSettings()
    {
        var session = CreateSquareSession();
        var document = session.Document;
        var face = Assert.Single(document.Polygons);
        var key = FaceIdentity.Create(document, face);

        Assert.True(FaceFillService.TrySetFillColor(document, face, Colors.Green));
        Assert.True(FaceFillService.TrySetFillPattern(document, face, FaceFillPattern.Diagonal));
        Assert.True(document.FaceFillStyles.ContainsKey(key));

        session.Selection.SelectedPolygonIds.Add(face.Id);
        session.History.Record(document);
        session.Edit.Delete(session);

        Assert.Empty(document.Polygons);
        Assert.DoesNotContain(key, document.FaceFillStyles.Keys);
    }

    [Fact]
    public void FillStyle_SurvivesFaceResyncWithSameTopology()
    {
        var document = CreateSquareDocument();
        var face = Assert.Single(document.Polygons);
        var key = FaceIdentity.Create(document, face);

        Assert.True(FaceFillService.TrySetFillColor(document, face, Colors.Red));
        Assert.True(FaceFillService.TrySetFillPattern(document, face, FaceFillPattern.Diagonal));

        PolygonBuilder.SyncFaces(document, Tolerance);

        var rebuiltFace = Assert.Single(document.Polygons);
        var fill = FaceFillService.GetFill(document, rebuiltFace);

        Assert.Equal(key, FaceIdentity.Create(document, rebuiltFace));
        Assert.Equal(Colors.Red, fill.FillColor);
        Assert.Equal(FaceFillPattern.Diagonal, fill.FillPattern);
    }

    [Fact]
    public void FillStyle_SurvivesObjectMove()
    {
        var session = CreateSquareSession();
        var document = session.Document;
        var face = Assert.Single(document.Polygons);
        var countsBefore = GetTopologyCounts(document);

        Assert.True(FaceFillService.TrySetFillColor(document, face, Colors.Red));
        Assert.True(FaceFillService.TrySetFillPattern(document, face, FaceFillPattern.Diagonal));

        session.Selection.SelectedPolygonIds.Add(face.Id);
        var snapshot = MoveOperations.CreateSnapshot(document, session.Selection);
        MoveOperations.ExecuteObjectMove(document, session.Selection, snapshot, new PointF(10, 20));

        Assert.Equal(countsBefore, GetTopologyCounts(document));
        var movedFace = Assert.Single(document.Polygons);
        var fill = FaceFillService.GetFill(document, movedFace);
        Assert.Equal(Colors.Red, fill.FillColor);
        Assert.Equal(FaceFillPattern.Diagonal, fill.FillPattern);
    }

    [Fact]
    public void FillStyle_SurvivesObjectRotate()
    {
        var session = CreateSquareSession();
        var document = session.Document;
        var face = Assert.Single(document.Polygons);
        var countsBefore = GetTopologyCounts(document);

        Assert.True(FaceFillService.TrySetFillColor(document, face, Colors.Blue));
        Assert.True(FaceFillService.TrySetFillPattern(document, face, FaceFillPattern.Diagonal));

        session.Selection.SelectedPolygonIds.Add(face.Id);
        var snapshot = MoveOperations.CreateSnapshot(document, session.Selection);
        RotateOperations.ExecuteObjectRotate(
            document,
            session.Selection,
            snapshot,
            new PointF(50, 50),
            Math.PI / 4);

        Assert.Equal(countsBefore, GetTopologyCounts(document));
        var rotatedFace = Assert.Single(document.Polygons);
        var fill = FaceFillService.GetFill(document, rotatedFace);
        Assert.Equal(Colors.Blue, fill.FillColor);
        Assert.Equal(FaceFillPattern.Diagonal, fill.FillPattern);
    }

    [Fact]
    public void FillStyle_IsCopiedWithFaceOnCopy()
    {
        var session = CreateSquareSession();
        var document = session.Document;
        var face = Assert.Single(document.Polygons);
        var countsBefore = GetTopologyCounts(document);

        Assert.True(FaceFillService.TrySetFillColor(document, face, Colors.Green));
        Assert.True(FaceFillService.TrySetFillPattern(document, face, FaceFillPattern.Diagonal));

        session.Selection.SelectedPolygonIds.Add(face.Id);
        var snapshot = MoveOperations.CreateSnapshot(document, session.Selection);
        CopyOperations.ExecuteObjectCopy(document, session.Selection, snapshot, new PointF(120, 0));

        Assert.Equal(countsBefore.Vertices * 2, document.Vertices.Count);
        Assert.Equal(countsBefore.Edges * 2, document.Edges.Count);
        Assert.Equal(2, document.Polygons.Count(polygon => polygon.Type == PolygonType.Face));

        foreach (var copiedFace in document.Polygons.Where(polygon => polygon.Type == PolygonType.Face))
        {
            var fill = FaceFillService.GetFill(document, copiedFace);
            Assert.Equal(Colors.Green, fill.FillColor);
            Assert.Equal(FaceFillPattern.Diagonal, fill.FillPattern);
        }
    }

    [Fact]
    public void FillToolOptions_DefaultToSolidLightGray()
    {
        var options = new FillToolOptions();
        Assert.Equal(FaceFillStyle.DefaultFillColor, options.FillColor);
        Assert.Equal(FaceFillPattern.Solid, options.FillPattern);
    }

    private static CadDocument CreateSquareDocument()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(100, 100), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 100), new PointF(0, 100), Tolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 100), new PointF(0, 0), Tolerance);
        PolygonBuilder.SyncFaces(document, Tolerance);
        return document;
    }

    private static CadSession CreateSquareSession()
    {
        var session = new CadSession();
        var template = TestDocumentHelpers.CreateTemplate();
        EdgeOperations.AddSegment(session.Document, new PointF(0, 0), new PointF(100, 0), template, Tolerance);
        EdgeOperations.AddSegment(session.Document, new PointF(100, 0), new PointF(100, 100), template, Tolerance);
        EdgeOperations.AddSegment(session.Document, new PointF(100, 100), new PointF(0, 100), template, Tolerance);
        EdgeOperations.AddSegment(session.Document, new PointF(0, 100), new PointF(0, 0), template, Tolerance);
        PolygonBuilder.SyncFaces(session.Document, Tolerance);
        return session;
    }

    private static (int Vertices, int Edges, int Faces) GetTopologyCounts(CadDocument document)
        => (
            document.Vertices.Count,
            document.Edges.Count,
            document.Polygons.Count(polygon => polygon.Type == PolygonType.Face));
}
