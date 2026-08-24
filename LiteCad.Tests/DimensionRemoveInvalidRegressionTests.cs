using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Services;
using System.IO;
using Xunit;

namespace LiteCad.Tests;

public class DimensionRemoveInvalidRegressionTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void DeleteEdgeOnIndependentRegion_KeepsUnrelatedDimension()
    {
        var session = new CadSession();
        var doc = session.Document;

        var edgeA = TestDocumentHelpers.AddEdge(doc, new PointF(0, 0), new PointF(100, 0), Tol);
        var edgeB = TestDocumentHelpers.AddEdge(doc, new PointF(0, 500), new PointF(100, 500), Tol);
        PolygonBuilder.SyncFaces(doc, Tol);

        DimensionService.Create(doc, edgeA.StartVertexId, edgeA.EndVertexId, 10, Tol);
        var dimB = DimensionService.Create(doc, edgeB.StartVertexId, edgeB.EndVertexId, 10, Tol);

        session.Selection.SelectedEdgeIds.Add(edgeA.Id);
        session.History.Record(doc);
        session.Edit.Delete(session);

        Assert.Equal(2, doc.Dimensions.Count);
        Assert.Contains(doc.Dimensions, d => d.Id == dimB.Id);
    }

    [Fact]
    public void ModifyEdgeOnIndependentRegion_KeepsBothDimensionsWhenStillValid()
    {
        var session = new CadSession();
        var doc = session.Document;

        var edgeA = TestDocumentHelpers.AddEdge(doc, new PointF(0, 0), new PointF(100, 0), Tol);
        var edgeB = TestDocumentHelpers.AddEdge(doc, new PointF(0, 500), new PointF(100, 500), Tol);
        PolygonBuilder.SyncFaces(doc, Tol);

        var dimA = DimensionService.Create(doc, edgeA.StartVertexId, edgeA.EndVertexId, 10, Tol);
        var dimB = DimensionService.Create(doc, edgeB.StartVertexId, edgeB.EndVertexId, 10, Tol);

        TestDocumentHelpers.AddEdge(doc, new PointF(100, 0), new PointF(150, 0), Tol);
        TopologyService.PruneUnusedVertices(doc);
        PolygonBuilder.SyncFaces(doc, Tol);
        doc.NotifyChanged(DocumentChangeKind.Topology);
        DimensionService.RemoveInvalid(doc, Tol);

        Assert.Contains(doc.Dimensions, d => d.Id == dimA.Id);
        Assert.Contains(doc.Dimensions, d => d.Id == dimB.Id);
    }

    [Fact]
    public void TenIndependentRegions_DeleteOneEdge_KeepsOtherNineDimensions()
    {
        var session = new CadSession();
        var doc = session.Document;
        var dimensions = new List<Dimension>();

        for (var index = 0; index < 10; index++)
        {
            var y = index * 500f;
            var edge = TestDocumentHelpers.AddEdge(
                doc,
                new PointF(0, y),
                new PointF(100, y),
                Tol);
            dimensions.Add(DimensionService.Create(doc, edge.StartVertexId, edge.EndVertexId, 10, Tol));
        }

        PolygonBuilder.SyncFaces(doc, Tol);
        var edgeToDelete = doc.Edges.First(edge =>
            TopologyService.GetEdgeStartPoint(doc, edge).Y == 0
            && TopologyService.GetEdgeEndPoint(doc, edge).Y == 0);

        session.Selection.SelectedEdgeIds.Add(edgeToDelete.Id);
        session.History.Record(doc);
        session.Edit.Delete(session);

        Assert.Equal(10, doc.Dimensions.Count);
        foreach (var dimension in dimensions.Skip(1))
        {
            Assert.Contains(doc.Dimensions, d => d.Id == dimension.Id);
        }
    }

    [Fact]
    public void DimensionAnchorVerticesNotOnEdges_SurviveUnrelatedEdgeDelete()
    {
        var session = new CadSession();
        var doc = session.Document;

        var edgeA = TestDocumentHelpers.AddEdge(doc, new PointF(0, 0), new PointF(100, 0), Tol);
        var edgeB = TestDocumentHelpers.AddEdge(doc, new PointF(0, 500), new PointF(100, 500), Tol);
        PolygonBuilder.SyncFaces(doc, Tol);

        var firstAnchorId = TopologyService.FindOrCreateVertex(doc, new PointF(50, 0), Tol);
        var secondAnchorId = TopologyService.FindOrCreateVertex(doc, new PointF(80, 0), Tol);
        var dimA = DimensionService.Create(doc, firstAnchorId, secondAnchorId, 10, Tol);
        var dimB = DimensionService.Create(doc, edgeB.StartVertexId, edgeB.EndVertexId, 10, Tol);

        session.Selection.SelectedEdgeIds.Add(edgeA.Id);
        session.History.Record(doc);
        session.Edit.Delete(session);

        Assert.Contains(doc.Dimensions, d => d.Id == dimA.Id);
        Assert.Contains(doc.Dimensions, d => d.Id == dimB.Id);
        Assert.Contains(doc.Vertices, v => v.Id == firstAnchorId);
        Assert.Contains(doc.Vertices, v => v.Id == secondAnchorId);
    }

    [Fact]
    public void LoadedProject_IndependentLines_DeleteOneEdge_KeepsOtherDimension()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LiteCadTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var source = new CadDocument();
            var edgeA = TestDocumentHelpers.AddEdge(source, new PointF(0, 0), new PointF(100, 0), Tol);
            var edgeB = TestDocumentHelpers.AddEdge(source, new PointF(0, 500), new PointF(100, 500), Tol);
            PolygonBuilder.SyncFaces(source, Tol);

            DimensionService.Create(source, edgeA.StartVertexId, edgeA.EndVertexId, 10, Tol);
            var dimB = DimensionService.Create(source, edgeB.StartVertexId, edgeB.EndVertexId, 10, Tol);

            var filePath = Path.Combine(tempRoot, "IndependentLines.sit");
            var storage = new ProjectStorage(tempRoot);
            WpfTestUtilities.RunSta(() =>
                storage.SaveProject(source, LinearDisplayUnit.Millimeters, filePath, new Rendering.Renderer()));

            var session = new CadSession();
            var dto = storage.LoadProject(filePath, out _);
            session.LoadProject(dto, filePath);

            var doc = session.Document;
            Assert.Equal(2, doc.Dimensions.Count);

            var loadedEdgeA = doc.Edges.Single(edge =>
                Math.Abs(TopologyService.GetEdgeStartPoint(doc, edge).Y) < Tol);
            session.Selection.SelectedEdgeIds.Add(loadedEdgeA.Id);
            session.History.Record(doc);
            session.Edit.Delete(session);

            Assert.Equal(2, doc.Dimensions.Count);
            Assert.Contains(doc.Dimensions, d => d.Id == dimB.Id);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Project1_LoadedDimensions_AreValidBeforeMutation()
    {
        var projectPath = GetProject1Path();
        if (projectPath is null)
        {
            return;
        }

        var storage = new ProjectStorage();
        var dto = storage.LoadProject(projectPath, out _);
        var session = new CadSession();
        session.LoadProject(dto, projectPath);

        Assert.True(session.Document.Dimensions.Count > 1, "Project_1 should contain multiple dimensions.");

        foreach (var dimension in session.Document.Dimensions)
        {
            Assert.True(
                DimensionService.IsValid(session.Document, dimension, Tol),
                $"Dimension {dimension.Id} invalid before any mutation.");
        }
    }

    [Fact]
    public void Project1_DeleteOneEdge_DoesNotRemoveAllDimensions()
    {
        var projectPath = GetProject1Path();
        if (projectPath is null)
        {
            return;
        }

        var storage = new ProjectStorage();
        var dto = storage.LoadProject(projectPath, out _);
        var session = new CadSession();
        session.LoadProject(dto, projectPath);

        var doc = session.Document;
        var dimsBefore = doc.Dimensions.Count;
        Assert.True(dimsBefore > 1, "Project_1 should have multiple dimensions for this test.");

        var edgeToDelete = doc.Edges[0];
        session.Selection.SelectedEdgeIds.Add(edgeToDelete.Id);
        session.History.Record(doc);
        session.Edit.Delete(session);

        Assert.Equal(dimsBefore, doc.Dimensions.Count);
    }

    private static string? GetProject1Path()
    {
        var projectPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "LiteCad",
            "Projects",
            "Project_1.sit");

        return File.Exists(projectPath) ? projectPath : null;
    }
}
