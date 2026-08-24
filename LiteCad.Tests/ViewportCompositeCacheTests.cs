using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Rendering;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace LiteCad.Tests;

public class ViewportCompositeCacheTests
{
    private const double Tol = 1e-4;
    private static readonly Size Viewport = new(800, 600);

    [Fact]
    public void OverlayOnly_ReusesCompositeWhenCacheKeyUnchanged()
    {
        var session = CreateSessionWithSquare();
        var renderer = session.Renderer;
        var cache = renderer.ViewportCompositeCache;

        Render(renderer, session, ViewportRenderPass.Full);
        Render(renderer, session, ViewportRenderPass.OverlayOnly);

        Assert.Equal(1, cache.BuildCount);
    }

    [Fact]
    public void FullPass_RebuildsComposite()
    {
        var session = CreateSessionWithSquare();
        var renderer = session.Renderer;
        var cache = renderer.ViewportCompositeCache;

        Render(renderer, session, ViewportRenderPass.Full);
        Render(renderer, session, ViewportRenderPass.Full);

        Assert.Equal(2, cache.BuildCount);
    }

    [Fact]
    public void OverlayOnly_RebuildsWhenRevisionChanges()
    {
        var session = CreateSessionWithSquare();
        var renderer = session.Renderer;
        var cache = renderer.ViewportCompositeCache;

        Render(renderer, session, ViewportRenderPass.Full);

        TestDocumentHelpers.AddEdge(session.Document, new PointF(200, 0), new PointF(200, 100), Tol);
        PolygonBuilder.SyncFaces(session.Document, Tol);
        session.Document.NotifyChanged(DocumentChangeKind.Topology);

        Render(renderer, session, ViewportRenderPass.OverlayOnly);

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

    private static void Render(CadSession session, ViewportRenderPass pass)
        => Render(session.Renderer, session, pass);

    private static void Render(Renderer renderer, CadSession session, ViewportRenderPass pass)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            renderer.Render(
                context,
                session.Document,
                session.Selection,
                session.Camera,
                Viewport,
                activeTool: null,
                session.DisplayUnitSettings.LinearUnit,
                pass);
        }
    }
}
