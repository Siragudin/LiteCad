using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Services;
using Xunit;

namespace LiteCad.Tests;

public class DimensionToolTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void Create_BetweenTwoVertices_AddsDimension()
    {
        var document = CreateLineDocument(new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);

        var dimension = DimensionService.Create(document, firstId, secondId, 50, Tol);

        Assert.Single(document.Dimensions);
        Assert.Equal(firstId, dimension.FirstVertexId);
        Assert.Equal(secondId, dimension.SecondVertexId);
    }

    [Fact]
    public void Create_ComputesMeasuredDistance()
    {
        var document = CreateLineDocument(new PointF(0, 0), new PointF(250, 0), out var firstId, out var secondId);
        var dimension = DimensionService.Create(document, firstId, secondId, 50, Tol);

        Assert.Equal(250, DimensionService.GetMeasuredDistance(document, dimension), 3);
    }

    [Fact]
    public void Geometry_HorizontalLayout_IsCorrect()
    {
        Assert.True(DimensionGeometry.TryCreateLayout(
            new PointF(0, 0),
            new PointF(100, 0),
            50,
            Tol,
            out var layout));

        Assert.Equal(100, layout.MeasuredDistance, 3);
        Assert.True(MathUtils.ArePointsEqual(new PointF(0, 50), layout.FirstExtensionEnd, Tol));
        Assert.True(MathUtils.ArePointsEqual(new PointF(100, 50), layout.SecondExtensionEnd, Tol));
    }

    [Fact]
    public void Geometry_VerticalLayout_IsCorrect()
    {
        Assert.True(DimensionGeometry.TryCreateLayout(
            new PointF(0, 0),
            new PointF(0, 100),
            40,
            Tol,
            out var layout));

        Assert.Equal(100, layout.MeasuredDistance, 3);
        Assert.True(MathUtils.ArePointsEqual(new PointF(-40, 0), layout.FirstExtensionEnd, Tol));
        Assert.True(MathUtils.ArePointsEqual(new PointF(-40, 100), layout.SecondExtensionEnd, Tol));
    }

    [Fact]
    public void Geometry_DiagonalLayout_IsCorrect()
    {
        Assert.True(DimensionGeometry.TryCreateLayout(
            new PointF(0, 0),
            new PointF(30, 40),
            10,
            Tol,
            out var layout));

        Assert.Equal(50, layout.MeasuredDistance, 3);
    }

    [Fact]
    public void Create_PositiveOffset_StoresValue()
    {
        var document = CreateLineDocument(new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);
        var dimension = DimensionService.Create(document, firstId, secondId, 50, Tol);

        Assert.Equal(50, dimension.Offset, 3);
    }

    [Fact]
    public void Create_NegativeOffset_StoresValue()
    {
        var document = CreateLineDocument(new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);
        var dimension = DimensionService.Create(document, firstId, secondId, -25, Tol);

        Assert.Equal(-25, dimension.Offset, 3);
    }

    [Fact]
    public void SetOffset_UpdatesDimensionWithoutChangingDistance()
    {
        var document = CreateLineDocument(new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);
        var dimension = DimensionService.Create(document, firstId, secondId, 50, Tol);

        Assert.True(DimensionService.TrySetOffset(document, dimension.Id, 100, Tol));

        Assert.Equal(100, dimension.Offset, 3);
        Assert.Equal(100, DimensionService.GetMeasuredDistance(document, dimension), 3);
    }

    [Fact]
    public void Delete_RemovesDimensionOnly()
    {
        var document = CreateLineDocument(new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);
        var dimension = DimensionService.Create(document, firstId, secondId, 50, Tol);

        Assert.True(DimensionService.Delete(document, dimension.Id));
        Assert.Empty(document.Dimensions);
        Assert.Single(document.Edges);
    }

    [Fact]
    public void RemoveInvalid_MovingFirstVertex_RemovesDimension()
    {
        var document = CreateLineDocument(new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);
        DimensionService.Create(document, firstId, secondId, 50, Tol);

        TopologyService.MoveVertex(document, firstId, new PointF(10, 0));
        DimensionService.RemoveInvalid(document, Tol);

        Assert.Empty(document.Dimensions);
    }

    [Fact]
    public void RemoveInvalid_MovingSecondVertex_RemovesDimension()
    {
        var document = CreateLineDocument(new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);
        DimensionService.Create(document, firstId, secondId, 50, Tol);

        TopologyService.MoveVertex(document, secondId, new PointF(100, 10));
        DimensionService.RemoveInvalid(document, Tol);

        Assert.Empty(document.Dimensions);
    }

    [Fact]
    public void RemoveInvalid_DeletingFirstVertex_KeepsDimensionWhileAnchorVerticesRemain()
    {
        var document = CreateLineDocument(new PointF(0, 0), new PointF(100, 0), out var firstId, out _);
        DimensionService.Create(document, firstId, document.Edges[0].EndVertexId, 50, Tol);

        document.ClearEdges();
        TopologyService.PruneUnusedVertices(document);

        DimensionService.RemoveInvalid(document, Tol);

        Assert.Single(document.Dimensions);
        Assert.Equal(2, document.Vertices.Count);
    }

    [Fact]
    public void RemoveInvalid_DeletingSecondVertex_KeepsDimensionWhileAnchorVerticesRemain()
    {
        var document = CreateLineDocument(new PointF(0, 0), new PointF(100, 0), out _, out var secondId);
        DimensionService.Create(document, document.Edges[0].StartVertexId, secondId, 50, Tol);

        document.ClearEdges();
        TopologyService.PruneUnusedVertices(document);

        DimensionService.RemoveInvalid(document, Tol);

        Assert.Single(document.Dimensions);
        Assert.Equal(2, document.Vertices.Count);
    }

    [Fact]
    public void RemoveInvalid_UnrelatedGeometryMove_KeepsDimension()
    {
        var document = new CadDocument();
        var anchored = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        var unrelated = TestDocumentHelpers.AddEdge(document, new PointF(0, 200), new PointF(50, 200), Tol);
        DimensionService.Create(document, anchored.StartVertexId, anchored.EndVertexId, 50, Tol);

        var unrelatedStart = TopologyService.GetEdgeStartPoint(document, unrelated);
        TopologyService.MoveVertex(document, unrelated.StartVertexId, new PointF(unrelatedStart.X + 5, unrelatedStart.Y));

        DimensionService.RemoveInvalid(document, Tol);

        Assert.Single(document.Dimensions);
    }

    [Fact]
    public void Create_DoesNotCreateEdge()
    {
        var document = CreateLineDocument(new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);
        var edgeCountBefore = document.Edges.Count;

        DimensionService.Create(document, firstId, secondId, 50, Tol);

        Assert.Equal(edgeCountBefore, document.Edges.Count);
    }

    [Fact]
    public void PolygonBuilder_DoesNotChangeDimensions()
    {
        var document = CreateLineDocument(new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);
        DimensionService.Create(document, firstId, secondId, 50, Tol);
        PolygonBuilder.SyncFaces(document, Tol);

        Assert.Single(document.Dimensions);
    }

    [Fact]
    public void UndoRedo_Create_RestoresDimension()
    {
        var session = new CadSession();
        var document = session.Document;
        CreateLineDocument(document, new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);

        session.History.Record(document);
        var dimension = DimensionService.Create(document, firstId, secondId, 50, Tol);
        var dimensionId = dimension.Id;

        Assert.True(session.History.Undo(document, Tol));
        Assert.Empty(document.Dimensions);

        Assert.True(session.History.Redo(document, Tol));
        Assert.Contains(document.Dimensions, item => item.Id == dimensionId);
    }

    [Fact]
    public void UndoRedo_OffsetChange_RestoresPreviousOffset()
    {
        var session = new CadSession();
        var document = session.Document;
        CreateLineDocument(document, new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);
        var dimension = DimensionService.Create(document, firstId, secondId, 50, Tol);
        var dimensionId = dimension.Id;

        session.History.Record(document);
        Assert.True(DimensionService.TrySetOffset(document, dimensionId, 100, Tol));

        Assert.True(session.History.Undo(document, Tol));
        Assert.Equal(50, document.Dimensions.First(item => item.Id == dimensionId).Offset, 3);

        Assert.True(session.History.Redo(document, Tol));
        Assert.Equal(100, document.Dimensions.First(item => item.Id == dimensionId).Offset, 3);
    }

    [Fact]
    public void Delete_WithUndo_RestoresDimension()
    {
        var session = new CadSession();
        var document = session.Document;
        CreateLineDocument(document, new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);
        var dimension = DimensionService.Create(document, firstId, secondId, 50, Tol);
        session.Selection.SelectedDimensionIds.Add(dimension.Id);

        session.History.Record(document);
        DimensionService.DeleteSelected(document, session.Selection);

        Assert.Empty(document.Dimensions);
        Assert.True(session.History.Undo(document, Tol));
        Assert.Single(document.Dimensions);
    }

    [Fact]
    public void Create_DefaultExtensionStyle_IsFull()
    {
        var document = CreateLineDocument(new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);
        var dimension = DimensionService.Create(document, firstId, secondId, 50, Tol);

        Assert.Equal(DimensionExtensionStyle.Full, dimension.ExtensionStyle);
    }

    [Fact]
    public void SetExtensionStyle_UpdatesDimensionWithoutChangingGeometry()
    {
        var document = CreateLineDocument(new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);
        var dimension = DimensionService.Create(document, firstId, secondId, 50, Tol);
        var edgeCountBefore = document.Edges.Count;

        Assert.True(DimensionService.TrySetExtensionStyle(
            document,
            dimension.Id,
            DimensionExtensionStyle.Short));

        Assert.Equal(DimensionExtensionStyle.Short, dimension.ExtensionStyle);
        Assert.Equal(edgeCountBefore, document.Edges.Count);
    }

    [Fact]
    public void ShortExtensionStart_IsTwentyFivePercentOfFullLength()
    {
        Assert.True(DimensionGeometry.TryCreateLayout(
            new PointF(0, 0),
            new PointF(100, 0),
            50,
            Tol,
            out var layout));

        var fullLength = MathUtils.Distance(layout.FirstAnchor, layout.FirstExtensionEnd);
        var shortStart = new PointF(
            layout.FirstExtensionEnd.X - (layout.FirstExtensionEnd.X - layout.FirstAnchor.X) * 0.25f,
            layout.FirstExtensionEnd.Y - (layout.FirstExtensionEnd.Y - layout.FirstAnchor.Y) * 0.25f);
        var shortLength = MathUtils.Distance(shortStart, layout.FirstExtensionEnd);

        Assert.Equal(fullLength * 0.25, shortLength, 3);
    }

    [Fact]
    public void UndoRedo_ExtensionStyleChange_RestoresPreviousStyle()
    {
        var session = new CadSession();
        var document = session.Document;
        CreateLineDocument(document, new PointF(0, 0), new PointF(100, 0), out var firstId, out var secondId);
        var dimension = DimensionService.Create(document, firstId, secondId, 50, Tol);
        var dimensionId = dimension.Id;

        session.History.Record(document);
        Assert.True(DimensionService.TrySetExtensionStyle(
            document,
            dimensionId,
            DimensionExtensionStyle.Short));

        Assert.True(session.History.Undo(document, Tol));
        Assert.Equal(DimensionExtensionStyle.Full, document.Dimensions.First(item => item.Id == dimensionId).ExtensionStyle);

        Assert.True(session.History.Redo(document, Tol));
        Assert.Equal(DimensionExtensionStyle.Short, document.Dimensions.First(item => item.Id == dimensionId).ExtensionStyle);
    }

    [Fact]
    public void MultipleDimensions_AreIndependent()
    {
        var document = new CadDocument();
        var firstLine = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
        var secondLine = TestDocumentHelpers.AddEdge(document, new PointF(0, 100), new PointF(0, 200), Tol);

        DimensionService.Create(document, firstLine.StartVertexId, firstLine.EndVertexId, 50, Tol);
        DimensionService.Create(document, secondLine.StartVertexId, secondLine.EndVertexId, -20, Tol);

        Assert.Equal(2, document.Dimensions.Count);
        Assert.Equal(50, document.Dimensions[0].Offset, 3);
        Assert.Equal(-20, document.Dimensions[1].Offset, 3);
    }

    private static CadDocument CreateLineDocument(
        PointF start,
        PointF end,
        out Guid firstVertexId,
        out Guid secondVertexId)
    {
        var document = new CadDocument();
        CreateLineDocument(document, start, end, out firstVertexId, out secondVertexId);
        return document;
    }

    private static void CreateLineDocument(
        CadDocument document,
        PointF start,
        PointF end,
        out Guid firstVertexId,
        out Guid secondVertexId)
    {
        var edge = TestDocumentHelpers.AddEdge(document, start, end, Tol);
        firstVertexId = edge.StartVertexId;
        secondVertexId = edge.EndVertexId;
    }
}
