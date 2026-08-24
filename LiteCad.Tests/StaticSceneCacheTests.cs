using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class StaticSceneCacheTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void DrawWorldGeometry_ReusesCacheWhenRevisionAndZoomUnchanged()
    {
        var session = CreateSessionWithSquare();
        var cache = session.Renderer.WorldGeometryCache;

        DrawWorldGeometry(cache, session.Document, session.Camera);
        DrawWorldGeometry(cache, session.Document, session.Camera);

        Assert.Equal(1, cache.BuildCount);
    }

    [Fact]
    public void DrawWorldGeometry_RebuildsWhenRevisionChanges()
    {
        var session = CreateSessionWithSquare();
        var cache = session.Renderer.WorldGeometryCache;

        DrawWorldGeometry(cache, session.Document, session.Camera);

        TestDocumentHelpers.AddEdge(session.Document, new PointF(200, 0), new PointF(200, 100), Tol);
        PolygonBuilder.SyncFaces(session.Document, Tol);
        session.Document.NotifyChanged(DocumentChangeKind.Topology);

        DrawWorldGeometry(cache, session.Document, session.Camera);

        Assert.Equal(2, cache.BuildCount);
    }

    [Fact]
    public void Invalidate_ForcesRebuildOnNextDraw()
    {
        var session = CreateSessionWithSquare();
        var cache = session.Renderer.WorldGeometryCache;

        DrawWorldGeometry(cache, session.Document, session.Camera);
        cache.Invalidate();
        DrawWorldGeometry(cache, session.Document, session.Camera);

        Assert.Equal(2, cache.BuildCount);
    }

    private static CadSession CreateSessionWithSquare()
    {
        var session = new CadSession();
        TestDocumentHelpers.AddEdge(session.Document, PointF.Zero, new PointF(100, 0), Tol);
        TestDocumentHelpers.AddEdge(session.Document, new PointF(100, 0), new PointF(100, 100), Tol);
        TestDocumentHelpers.AddEdge(session.Document, new PointF(100, 100), PointF.Zero, Tol);
        PolygonBuilder.SyncFaces(session.Document, Tol);
        session.Document.NotifyChanged(DocumentChangeKind.Topology);
        return session;
    }

    private static void DrawWorldGeometry(
        Rendering.StaticSceneCache cache,
        CadDocument document,
        Rendering.Camera camera)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            cache.DrawWorldGeometry(context, document, camera);
        }
    }
}
