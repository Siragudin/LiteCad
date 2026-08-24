using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Services;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class DocumentRevisionTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void NewDocument_StartsAtRevisionZero()
    {
        var document = new CadDocument();

        Assert.Equal(0, document.Revision);
    }

    [Fact]
    public void NotifyChanged_IncrementsRevisionAndRaisesChanged()
    {
        var document = new CadDocument();
        DocumentChangedEventArgs? args = null;
        document.Changed += (_, eventArgs) => args = eventArgs;

        document.NotifyChanged(DocumentChangeKind.Topology);

        Assert.Equal(1, document.Revision);
        Assert.NotNull(args);
        Assert.Equal(1, args!.Revision);
        Assert.Equal(DocumentChangeKind.Topology, args.Kind);
    }

    [Fact]
    public void MutationBatch_CoalescesMultipleNotificationsIntoSingleRevisionBump()
    {
        var document = new CadDocument();
        var eventCount = 0;
        DocumentChangeKind lastKind = DocumentChangeKind.None;
        document.Changed += (_, eventArgs) =>
        {
            eventCount++;
            lastKind = eventArgs.Kind;
        };

        using (document.BeginMutationBatch())
        {
            document.NotifyChanged(DocumentChangeKind.Topology);
            document.NotifyChanged(DocumentChangeKind.Annotations);
        }

        Assert.Equal(1, document.Revision);
        Assert.Equal(1, eventCount);
        Assert.Equal(DocumentChangeKind.Topology | DocumentChangeKind.Annotations, lastKind);
    }

    [Fact]
    public void AddSegmentAndSyncFaces_BumpsTopologyRevision()
    {
        var document = new CadDocument();
        var revisionBefore = document.Revision;

        TestDocumentHelpers.AddEdge(document, PointF.Zero, new PointF(100, 0), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        document.NotifyChanged(DocumentChangeKind.Topology);

        Assert.Equal(revisionBefore + 1, document.Revision);
    }

    [Fact]
    public void LineToolCommitPath_BumpsTopologyRevision()
    {
        var document = new CadDocument();
        var revisionBefore = document.Revision;

        EdgeOperations.AddSegment(document, PointF.Zero, new PointF(50, 0), TestDocumentHelpers.CreateTemplate(), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        document.NotifyChanged(DocumentChangeKind.Topology);

        Assert.Equal(revisionBefore + 1, document.Revision);
    }

    [Fact]
    public void DimensionServiceCreate_BumpsAnnotationsRevision()
    {
        var document = new CadDocument();
        var edge = TestDocumentHelpers.AddEdge(document, PointF.Zero, new PointF(100, 0), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        document.NotifyChanged(DocumentChangeKind.Topology);

        var revisionBefore = document.Revision;
        DocumentChangeKind? kind = null;
        document.Changed += (_, eventArgs) => kind = eventArgs.Kind;

        DimensionService.Create(document, edge.StartVertexId, edge.EndVertexId, 10, Tol);

        Assert.Equal(revisionBefore + 1, document.Revision);
        Assert.Equal(DocumentChangeKind.Annotations, kind);
    }

    [Fact]
    public void FaceFillServiceTrySetFillColor_BumpsFaceFillRevision()
    {
        var document = CreateSquareWithFace();
        var face = document.Polygons.Single(polygon => polygon.Type == PolygonType.Face);
        var revisionBefore = document.Revision;
        DocumentChangeKind? kind = null;
        document.Changed += (_, eventArgs) => kind = eventArgs.Kind;

        Assert.True(FaceFillService.TrySetFillColor(document, face, Colors.Red));

        Assert.Equal(revisionBefore + 1, document.Revision);
        Assert.Equal(DocumentChangeKind.FaceFill, kind);
    }

    [Fact]
    public void AxisServiceCreate_BumpsAxesRevision()
    {
        var document = new CadDocument();
        var revisionBefore = document.Revision;

        AxisService.Create(document, PointF.Zero, new PointF(100, 0), Tol);

        Assert.Equal(revisionBefore + 1, document.Revision);
    }

    [Fact]
    public void DocumentSnapshotRestore_BumpsFullReplaceOnce()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, PointF.Zero, new PointF(100, 0), Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        document.NotifyChanged(DocumentChangeKind.Topology);

        var snapshot = DocumentSnapshot.Capture(document);
        document.NotifyChanged(DocumentChangeKind.Annotations);

        var revisionBefore = document.Revision;
        var eventCount = 0;
        DocumentChangeKind? kind = null;
        document.Changed += (_, eventArgs) =>
        {
            eventCount++;
            kind = eventArgs.Kind;
        };

        snapshot.Restore(document, Tol);

        Assert.Equal(revisionBefore + 1, document.Revision);
        Assert.Equal(1, eventCount);
        Assert.Equal(DocumentChangeKind.FullReplace, kind);
    }

    [Fact]
    public void ProjectDocumentSerializerApply_BumpsFullReplaceOnce()
    {
        var original = CreateSquareWithFace();
        var dto = ProjectDocumentSerializer.ToDto(original, LinearDisplayUnit.Millimeters);
        var document = new CadDocument();

        var revisionBefore = document.Revision;
        var eventCount = 0;
        document.Changed += (_, _) => eventCount++;

        ProjectDocumentSerializer.Apply(document, dto);

        Assert.Equal(revisionBefore + 1, document.Revision);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void LoadProject_BumpsFullReplaceOnce()
    {
        var original = CreateSquareWithFace();
        var dto = ProjectDocumentSerializer.ToDto(original, LinearDisplayUnit.Millimeters);
        var session = new CadSession();
        TestDocumentHelpers.AddEdge(session.Document, PointF.Zero, new PointF(50, 0), Tol);
        session.Document.NotifyChanged(DocumentChangeKind.Topology);

        var revisionBefore = session.Document.Revision;
        var eventCount = 0;
        session.Document.Changed += (_, _) => eventCount++;

        session.LoadProject(dto, "test.sit");

        Assert.Equal(revisionBefore + 1, session.Document.Revision);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void CadSessionClearDocumentContent_BumpsFullReplaceOnce()
    {
        var session = new CadSession();
        TestDocumentHelpers.AddEdge(session.Document, PointF.Zero, new PointF(100, 0), Tol);
        PolygonBuilder.SyncFaces(session.Document, Tol);
        session.Document.NotifyChanged(DocumentChangeKind.Topology);

        var revisionBefore = session.Document.Revision;
        var eventCount = 0;
        session.Document.Changed += (_, _) => eventCount++;

        session.NewDocument();

        Assert.Equal(revisionBefore + 1, session.Document.Revision);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void SelectionChange_DoesNotBumpRevision()
    {
        var document = new CadDocument();
        var edge = TestDocumentHelpers.AddEdge(document, PointF.Zero, new PointF(100, 0), Tol);
        var selection = new Selection();
        var revisionBefore = document.Revision;

        selection.SelectedEdgeIds.Add(edge.Id);
        selection.Clear();

        Assert.Equal(revisionBefore, document.Revision);
    }

    [Fact]
    public void EditServiceDelete_BumpsRevisionOnce()
    {
        var session = new CadSession();
        var edge = TestDocumentHelpers.AddEdge(session.Document, PointF.Zero, new PointF(100, 0), Tol);
        PolygonBuilder.SyncFaces(session.Document, Tol);
        session.Document.NotifyChanged(DocumentChangeKind.Topology);
        session.Selection.SelectedEdgeIds.Add(edge.Id);

        var revisionBefore = session.Document.Revision;
        var eventCount = 0;
        session.Document.Changed += (_, _) => eventCount++;

        Assert.True(session.Edit.Delete(session));

        Assert.Equal(revisionBefore + 1, session.Document.Revision);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void MoveOperationsExecuteObjectMove_BumpsRevision()
    {
        var session = new CadSession();
        var edge = TestDocumentHelpers.AddEdge(session.Document, PointF.Zero, new PointF(100, 0), Tol);
        PolygonBuilder.SyncFaces(session.Document, Tol);
        session.Document.NotifyChanged(DocumentChangeKind.Topology);
        session.Selection.SelectedEdgeIds.Add(edge.Id);

        var snapshot = MoveOperations.CreateSnapshot(session.Document, session.Selection);
        var revisionBefore = session.Document.Revision;

        MoveOperations.ExecuteObjectMove(
            session.Document,
            session.Selection,
            snapshot,
            new PointF(10, 0));

        Assert.True(session.Document.Revision > revisionBefore);
    }

    private static CadDocument CreateSquareWithFace()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, PointF.Zero, new PointF(100, 0), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 0), new PointF(100, 100), Tol);
        TestDocumentHelpers.AddEdge(document, new PointF(100, 100), PointF.Zero, Tol);
        PolygonBuilder.SyncFaces(document, Tol);
        document.NotifyChanged(DocumentChangeKind.Topology);
        return document;
    }
}
