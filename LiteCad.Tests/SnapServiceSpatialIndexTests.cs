using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Services;
using Xunit;

namespace LiteCad.Tests;

public class SnapServiceSpatialIndexTests
{
    private const double GeometryTolerance = 1e-4;
    private const double SnapTolerance = 12.0;

    [Fact]
    public void ManyParallelLines_ProduceNoIntersectionSnaps()
    {
        var document = new CadDocument();
        for (var i = 0; i < 40; i++)
        {
            var y = i * 200;
            TestDocumentHelpers.AddEdge(document, new PointF(0, y), new PointF(1000, y), GeometryTolerance);
        }

        var service = new SnapService();
        service.InvalidateCache();
        var query = service.Query(document, new PointF(500, 100), SnapTolerance, includeOnEdge: true);

        Assert.DoesNotContain(query.Visible, snap => snap.Kind == SnapKind.Intersection);
    }

    [Fact]
    public void GridOfCrossingLines_FindsAllInteriorIntersections()
    {
        var document = new CadDocument();
        for (var i = 0; i < 10; i++)
        {
            var coordinate = i * 100;
            TestDocumentHelpers.AddEdge(document, new PointF(coordinate, 0), new PointF(coordinate, 900), GeometryTolerance);
            TestDocumentHelpers.AddEdge(document, new PointF(0, coordinate), new PointF(900, coordinate), GeometryTolerance);
        }

        var service = new SnapService();
        var interiorCrossings = new List<PointF>();
        for (var x = 100; x < 900; x += 100)
        {
            for (var y = 100; y < 900; y += 100)
            {
                interiorCrossings.Add(new PointF(x, y));
            }
        }

        foreach (var crossing in interiorCrossings)
        {
            var snap = service.FindBestSnap(document, crossing, SnapTolerance, includeOnEdge: true);
            Assert.True(snap.HasSnap, $"Expected snap at {crossing}");
            Assert.Equal(SnapKind.Intersection, snap.Snap!.Value.Kind);
        }
    }

    [Fact]
    public void LongSegmentsAcrossGrid_StillDetectEndpointContacts()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(10_000, 0), GeometryTolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(5000, -50), new PointF(5000, 50), GeometryTolerance);

        var touch = new PointF(5000, 0);
        var snap = new SnapService().FindBestSnap(document, touch, SnapTolerance, includeOnEdge: true);

        Assert.True(snap.HasSnap);
        Assert.True(
            snap.Snap!.Value.Kind is SnapKind.Intersection or SnapKind.Endpoint,
            $"Expected intersection or endpoint snap, got {snap.Snap.Value.Kind}");
    }

    [Fact]
    public void ColdQuery_StaticCandidateCount_MatchesKnownCrossingFixture()
    {
        var document = new CadDocument();
        TestDocumentHelpers.AddEdge(document, new PointF(0, 50), new PointF(100, 50), GeometryTolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(50, 0), new PointF(50, 100), GeometryTolerance);
        TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 100), GeometryTolerance);

        var service = new SnapService();
        service.InvalidateCache();
        _ = service.GetVisibleSnaps(document, new PointF(50, 50), SnapTolerance);

        var cache = typeof(SnapService)
            .GetField("_cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(service);
        Assert.NotNull(cache);

        var staticCandidates = cache!.GetType().GetProperty("StaticCandidates")!.GetValue(cache)!;
        var count = ((System.Collections.IEnumerable)staticCandidates).Cast<object>().Count();

        Assert.Equal(7, count);
    }

    [Fact]
    public void ColdQuery_Results_AreStableAcrossRepeatedRebuilds()
    {
        var document = BuildDenseCrossingDocument();
        var cursor = new PointF(250, 250);

        var first = CaptureVisible(document, cursor);
        var second = CaptureVisible(document, cursor);

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].Kind, second[i].Kind);
            Assert.True(MathUtils.ArePointsEqual(first[i].Position, second[i].Position, GeometryTolerance));
        }
    }

    private static CadDocument BuildDenseCrossingDocument()
    {
        var document = new CadDocument();
        for (var i = 0; i < 8; i++)
        {
            var offset = i * 80;
            TestDocumentHelpers.AddEdge(document, new PointF(offset, 0), new PointF(offset, 640), GeometryTolerance);
            TestDocumentHelpers.AddEdge(document, new PointF(0, offset), new PointF(640, offset), GeometryTolerance);
        }

        return document;
    }

    private static List<SnapPoint> CaptureVisible(CadDocument document, PointF cursor)
    {
        var service = new SnapService();
        service.InvalidateCache();
        return service.GetVisibleSnaps(document, cursor, SnapTolerance).ToList();
    }
}
