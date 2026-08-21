using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Infrastructure;
using LiteCad.Services;
using System.Text.Json;
using Xunit;

namespace LiteCad.Tests;

public class AxisTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void CreateAxis_AddsToDocument()
    {
        var document = new CadDocument();
        var start = new PointF(0, 0);
        var end = new PointF(100, 0);

        var axis = AxisService.Create(document, start, end, Tol);

        Assert.NotNull(axis);
        Assert.Single(document.Axes);
        Assert.True(MathUtils.ArePointsEqual(axis!.Start, start, Tol));
        Assert.True(MathUtils.ArePointsEqual(axis.End, end, Tol));
    }

    [Fact]
    public void CreateAxis_DoesNotChangeTopology()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(100, 100), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 100), new PointF(0, 100), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 100), new PointF(0, 0), Tol);
        PolygonBuilder.SyncFaces(document, Tol);

        var vertexCount = document.Vertices.Count;
        var edgeCount = document.Edges.Count;
        var faceCount = document.Polygons.Count(polygon => polygon.Type == PolygonType.Face);

        AxisService.Create(document, new PointF(50, -10), new PointF(50, 110), Tol);

        Assert.Equal(vertexCount, document.Vertices.Count);
        Assert.Equal(edgeCount, document.Edges.Count);
        Assert.Equal(faceCount, document.Polygons.Count(polygon => polygon.Type == PolygonType.Face));
    }

    [Fact]
    public void AxisCrossingEdge_DoesNotSplitEdge()
    {
        var document = new CadDocument();
        var edge = TestDocumentHelpers.AddEdge(document, new PointF(0, 50), new PointF(100, 50), Tol);
        var edgeId = edge.Id;
        var vertexCount = document.Vertices.Count;

        AxisService.Create(document, new PointF(50, 0), new PointF(50, 100), Tol);

        Assert.Single(document.Edges, item => item.Id == edgeId);
        Assert.Equal(vertexCount, document.Vertices.Count);
        Assert.DoesNotContain(document.Polygons, polygon => polygon.Type == PolygonType.Face);
    }

    [Fact]
    public void MoveAxis_TranslatesBothEndpoints()
    {
        var document = new CadDocument();
        var axis = AxisService.Create(document, new PointF(0, 0), new PointF(10, 0), Tol)!;
        var selection = new Selection();
        selection.SelectedAxisIds.Add(axis.Id);
        var snapshot = MoveOperations.CreateSnapshot(document, selection);

        MoveOperations.ExecuteObjectMove(document, selection, snapshot, new PointF(5, 7));

        Assert.True(MathUtils.ArePointsEqual(axis.Start, new PointF(5, 7), Tol));
        Assert.True(MathUtils.ArePointsEqual(axis.End, new PointF(15, 7), Tol));
    }

    [Fact]
    public void RotateAxis_RotatesBothEndpoints()
    {
        var document = new CadDocument();
        var axis = AxisService.Create(document, new PointF(10, 0), new PointF(20, 0), Tol)!;
        var selection = new Selection();
        selection.SelectedAxisIds.Add(axis.Id);
        var snapshot = MoveOperations.CreateSnapshot(document, selection);
        var pivot = new PointF(10, 0);

        RotateOperations.ExecuteObjectRotate(document, selection, snapshot, pivot, Math.PI / 2);

        Assert.True(MathUtils.ArePointsEqual(axis.Start, pivot, Tol));
        Assert.True(MathUtils.ArePointsEqual(axis.End, new PointF(10, 10), Tol));
    }

    [Fact]
    public void CopyAxis_CreatesIndependentAxis()
    {
        var document = new CadDocument();
        var axis = AxisService.Create(document, new PointF(0, 0), new PointF(10, 0), Tol)!;
        var selection = new Selection();
        selection.SelectedAxisIds.Add(axis.Id);
        var snapshot = MoveOperations.CreateSnapshot(document, selection);

        CopyOperations.ExecuteObjectCopy(document, selection, snapshot, new PointF(0, 20));

        Assert.Equal(2, document.Axes.Count);
        var copy = document.Axes.Single(item => item.Id != axis.Id);
        axis.End = new PointF(99, 0);
        Assert.False(MathUtils.ArePointsEqual(copy.End, axis.End, Tol));
    }

    [Fact]
    public void DeleteAxis_RemovesOnlyAxis()
    {
        var session = new CadSession();
        TestDocumentHelpers.AddEdge(session.Document, new PointF(0, 0), new PointF(10, 0), Tol);
        var axis = AxisService.Create(session.Document, new PointF(0, 5), new PointF(10, 5), Tol)!;
        var edgeCount = session.Document.Edges.Count;

        session.Selection.SelectedAxisIds.Add(axis.Id);
        session.History.Record(session.Document);
        session.Edit.Delete(session);

        Assert.Empty(session.Document.Axes);
        Assert.Equal(edgeCount, session.Document.Edges.Count);
    }

    [Fact]
    public void UndoRedo_PreservesAxis()
    {
        var session = new CadSession();
        session.History.Record(session.Document);
        AxisService.Create(session.Document, new PointF(0, 0), new PointF(10, 0), Tol);

        Assert.Single(session.Document.Axes);
        session.History.Undo(session.Document, Tol);
        Assert.Empty(session.Document.Axes);
        session.History.Redo(session.Document, Tol);
        Assert.Single(session.Document.Axes);
    }

    [Fact]
    public void SerializeDeserialize_RoundTripsAxis()
    {
        var document = new CadDocument();
        var axis = AxisService.Create(document, new PointF(1, 2), new PointF(3, 4), Tol)!;

        var json = ProjectDocumentSerializer.Serialize(document, LinearDisplayUnit.Millimeters);
        var loaded = new CadDocument();
        ProjectDocumentSerializer.Apply(loaded, ProjectDocumentSerializer.Deserialize(json));

        Assert.Single(loaded.Axes);
        var restored = loaded.Axes[0];
        Assert.Equal(axis.Id, restored.Id);
        Assert.True(MathUtils.ArePointsEqual(restored.Start, axis.Start, Tol));
        Assert.True(MathUtils.ArePointsEqual(restored.End, axis.End, Tol));
    }

    [Fact]
    public void Deserialize_WithoutAxesProperty_LoadsEmptyAxes()
    {
        var json = """
            {
              "formatVersion": 1,
              "linearDisplayUnit": "Millimeters",
              "vertices": [],
              "edges": [],
              "userPolygons": [],
              "dimensions": []
            }
            """;

        var document = new CadDocument();
        ProjectDocumentSerializer.Apply(document, ProjectDocumentSerializer.Deserialize(json));

        Assert.Empty(document.Axes);
    }

    [Fact]
    public void Serialize_IncludesAxesArray()
    {
        var document = new CadDocument();
        AxisService.Create(document, new PointF(100, 200), new PointF(500, 200), Tol);

        var json = ProjectDocumentSerializer.Serialize(document, LinearDisplayUnit.Millimeters);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("axes", out var axes));
        Assert.Equal(JsonValueKind.Array, axes.ValueKind);
        Assert.Equal(1, axes.GetArrayLength());
    }

    [Fact]
    public void CreateAxis_ExactDuplicate_KeepsSingleAxis()
    {
        var document = new CadDocument();
        AxisService.Create(document, new PointF(0, 0), new PointF(100, 0), Tol);
        AxisService.Create(document, new PointF(0, 0), new PointF(100, 0), Tol);

        Assert.Single(document.Axes);
        Assert.Equal(100, MathUtils.Distance(document.Axes[0].Start, document.Axes[0].End), 3);
    }

    [Fact]
    public void CreateAxis_Extension_MergesIntoSingleAxis()
    {
        var document = new CadDocument();
        AxisService.Create(document, new PointF(0, 0), new PointF(100, 0), Tol);
        AxisService.Create(document, new PointF(100, 0), new PointF(150, 0), Tol);

        Assert.Single(document.Axes);
        Assert.True(MathUtils.ArePointsEqual(document.Axes[0].Start, new PointF(0, 0), Tol));
        Assert.True(MathUtils.ArePointsEqual(document.Axes[0].End, new PointF(150, 0), Tol));
    }

    [Fact]
    public void CreateAxis_PartialOverlap_MergesIntoSingleAxis()
    {
        var document = new CadDocument();
        AxisService.Create(document, new PointF(0, 0), new PointF(100, 0), Tol);
        AxisService.Create(document, new PointF(50, 0), new PointF(150, 0), Tol);

        Assert.Single(document.Axes);
        Assert.True(MathUtils.ArePointsEqual(document.Axes[0].Start, new PointF(0, 0), Tol));
        Assert.True(MathUtils.ArePointsEqual(document.Axes[0].End, new PointF(150, 0), Tol));
    }

    [Fact]
    public void CreateAxis_FullyContained_KeepsLongerAxis()
    {
        var document = new CadDocument();
        AxisService.Create(document, new PointF(0, 0), new PointF(200, 0), Tol);
        AxisService.Create(document, new PointF(50, 0), new PointF(100, 0), Tol);

        Assert.Single(document.Axes);
        Assert.True(MathUtils.ArePointsEqual(document.Axes[0].Start, new PointF(0, 0), Tol));
        Assert.True(MathUtils.ArePointsEqual(document.Axes[0].End, new PointF(200, 0), Tol));
    }

    [Fact]
    public void CreateAxis_PerpendicularAtEndpoint_DoesNotMerge()
    {
        var document = new CadDocument();
        AxisService.Create(document, new PointF(0, 0), new PointF(100, 0), Tol);
        AxisService.Create(document, new PointF(100, 0), new PointF(100, 100), Tol);

        Assert.Equal(2, document.Axes.Count);
    }

    [Fact]
    public void CreateAxis_PerpendicularCrossing_KeepsBothAxes()
    {
        var document = new CadDocument();
        AxisService.Create(document, new PointF(0, 50), new PointF(100, 50), Tol);
        AxisService.Create(document, new PointF(50, 0), new PointF(50, 100), Tol);

        Assert.Equal(2, document.Axes.Count);
        Assert.Empty(document.Edges);
        Assert.Empty(document.Polygons);
    }

    [Fact]
    public void AxisIntersection_IsAvailableAsSnap()
    {
        var document = new CadDocument();
        AxisService.Create(document, new PointF(0, 50), new PointF(100, 50), Tol);
        AxisService.Create(document, new PointF(50, 0), new PointF(50, 100), Tol);

        var snapService = new SnapService();
        var result = snapService.FindBestSnap(document, new PointF(50, 50), Tol);

        Assert.True(result.HasSnap);
        Assert.Equal(SnapKind.AxisIntersection, result.Snap!.Value.Kind);
        Assert.True(MathUtils.ArePointsEqual(result.Snap.Value.Position, new PointF(50, 50), Tol));
    }

    [Fact]
    public void AxisIntersection_ResolvesMeasurementAnchor()
    {
        var document = new CadDocument();
        AxisService.Create(document, new PointF(0, 50), new PointF(100, 50), Tol);
        AxisService.Create(document, new PointF(50, 0), new PointF(50, 100), Tol);

        var snapService = new SnapService();
        var result = snapService.FindBestSnap(document, new PointF(50, 50), Tol);
        Assert.True(result.HasSnap);

        Assert.True(SnapService.TryResolveMeasurementAnchor(
            document,
            result.Snap!.Value,
            Tol,
            out var vertexId,
            out var anchor));
        Assert.NotEqual(Guid.Empty, vertexId);
        Assert.True(MathUtils.ArePointsEqual(anchor, new PointF(50, 50), Tol));
    }

    [Fact]
    public void MergeExtension_UndoRestoresPreviousAxis()
    {
        var session = new CadSession();
        AxisService.Create(session.Document, new PointF(0, 0), new PointF(100, 0), Tol);
        session.History.Record(session.Document);

        AxisService.Create(session.Document, new PointF(100, 0), new PointF(150, 0), Tol);

        Assert.Single(session.Document.Axes);
        Assert.Equal(150, MathUtils.Distance(session.Document.Axes[0].Start, session.Document.Axes[0].End), 3);

        session.History.Undo(session.Document, Tol);

        Assert.Single(session.Document.Axes);
        Assert.Equal(100, MathUtils.Distance(session.Document.Axes[0].Start, session.Document.Axes[0].End), 3);
    }

    [Fact]
    public void MergeExtension_RedoRestoresMergedAxis()
    {
        var session = new CadSession();
        AxisService.Create(session.Document, new PointF(0, 0), new PointF(100, 0), Tol);
        session.History.Record(session.Document);
        AxisService.Create(session.Document, new PointF(100, 0), new PointF(150, 0), Tol);

        session.History.Undo(session.Document, Tol);
        session.History.Redo(session.Document, Tol);

        Assert.Single(session.Document.Axes);
        Assert.Equal(150, MathUtils.Distance(session.Document.Axes[0].Start, session.Document.Axes[0].End), 3);
    }

    [Fact]
    public void MergedAxis_SerializesAsSingleAxis()
    {
        var document = new CadDocument();
        AxisService.Create(document, new PointF(0, 0), new PointF(100, 0), Tol);
        AxisService.Create(document, new PointF(100, 0), new PointF(150, 0), Tol);

        var json = ProjectDocumentSerializer.Serialize(document, LinearDisplayUnit.Millimeters);
        var loaded = new CadDocument();
        ProjectDocumentSerializer.Apply(loaded, ProjectDocumentSerializer.Deserialize(json));

        Assert.Single(loaded.Axes);
        Assert.Equal(150, MathUtils.Distance(loaded.Axes[0].Start, loaded.Axes[0].End), 3);
    }
}
