using LiteCad.Core.Document;

using LiteCad.Core.Geometry;

using LiteCad.Core.Selection;

using LiteCad.Dimensions;

using LiteCad.Infrastructure;



namespace LiteCad.Services;



public sealed class GeometryClipboard

{

    public List<ClipboardEdge> Edges { get; } = [];



    public List<Polygon> Polygons { get; } = [];



    public PointF Anchor { get; set; }



    public bool IsEmpty => Edges.Count == 0 && Polygons.Count == 0;

}



public sealed class ClipboardEdge(Edge edge, PointF start, PointF end)

{

    public Edge Edge { get; } = edge;



    public PointF Start { get; } = start;



    public PointF End { get; } = end;

}



public sealed class EditService

{

    private readonly GeometryClipboard _clipboard = new();

    private int _pasteCounter;



    public GeometryClipboard Clipboard => _clipboard;



    public bool Delete(CadSession session)

    {

        if (session.Selection.IsEmpty)

        {

            return false;

        }



        session.History.Record(session.Document);

        DimensionService.DeleteSelected(session.Document, session.Selection);

        RemoveSelection(session);

        session.Selection.Clear();

        return true;

    }



    public bool DeleteAt(CadSession session, PointF world, double tolerance)

    {

        var pick = SelectionPickOperations.PickAt(
            session.Document,
            world,
            tolerance,
            MathUtils.SelectionPickToleranceWorld(session.Camera.Zoom));

        if (pick is null)

        {

            return false;

        }



        SelectionPickOperations.ApplyToSelection(pick.Value, session.Selection);

        return Delete(session);

    }



    public bool Copy(CadSession session)

    {

        if (session.Selection.IsEmpty)

        {

            return false;

        }



        _clipboard.Edges.Clear();

        _clipboard.Polygons.Clear();

        _pasteCounter = 0;



        var copiedEdgeIds = new HashSet<Guid>();

        foreach (var edge in session.Document.Edges)

        {

            if (!session.Selection.SelectedEdgeIds.Contains(edge.Id))

            {

                continue;

            }



            _clipboard.Edges.Add(new ClipboardEdge(

                edge.CloneGeometry(),

                TopologyService.GetEdgeStartPoint(session.Document, edge),

                TopologyService.GetEdgeEndPoint(session.Document, edge)));

            copiedEdgeIds.Add(edge.Id);

        }



        foreach (var polygon in session.Document.Polygons)

        {

            if (!session.Selection.SelectedPolygonIds.Contains(polygon.Id))

            {

                continue;

            }



            foreach (var edgeId in PolygonGeometry.GetAllEdgeIds(polygon))

            {

                if (copiedEdgeIds.Contains(edgeId))

                {

                    continue;

                }



                var edge = session.Document.Edges.FirstOrDefault(item => item.Id == edgeId);

                if (edge is not null)

                {

                    _clipboard.Edges.Add(new ClipboardEdge(

                        edge.CloneGeometry(),

                        TopologyService.GetEdgeStartPoint(session.Document, edge),

                        TopologyService.GetEdgeEndPoint(session.Document, edge)));

                    copiedEdgeIds.Add(edgeId);

                }

            }



            var clone = new Polygon { Type = polygon.Type };

            clone.OuterLoop.Edges.AddRange(polygon.OuterLoop.Edges);

            foreach (var innerLoop in polygon.InnerLoops)

            {

                var loopClone = new Loop();

                loopClone.Edges.AddRange(innerLoop.Edges);

                clone.InnerLoops.Add(loopClone);

            }



            _clipboard.Polygons.Add(clone);

        }



        _clipboard.Anchor = GetSelectionCenter(session);

        return true;

    }



    public bool Cut(CadSession session)

    {

        if (!Copy(session))

        {

            return false;

        }



        session.History.Record(session.Document);

        RemoveSelection(session);

        session.Selection.Clear();

        return true;

    }



    public bool Paste(CadSession session)

    {

        if (_clipboard.IsEmpty)

        {

            return false;

        }



        session.History.Record(session.Document);

        _pasteCounter++;



        var offset = new PointF(

            _clipboard.Anchor.X + 20 * _pasteCounter,

            _clipboard.Anchor.Y + 20 * _pasteCounter);



        var delta = new PointF(

            offset.X - _clipboard.Anchor.X,

            offset.Y - _clipboard.Anchor.Y);



        session.Selection.Clear();

        var tolerance = MathUtils.SnapToleranceWorld(session.Camera.Zoom);



        foreach (var entry in _clipboard.Edges)

        {

            var start = Translate(entry.Start, delta);

            var end = Translate(entry.End, delta);

            var clone = TopologyService.CreateEdgeFromPoints(

                session.Document,

                start,

                end,

                entry.Edge,

                tolerance);

            session.Selection.SelectedEdgeIds.Add(clone.Id);

        }



        foreach (var polygon in _clipboard.Polygons)

        {

            if (polygon.Type == PolygonType.Face)

            {

                continue;

            }



            var clone = new Polygon { Type = polygon.Type };

            clone.OuterLoop.Edges.AddRange(polygon.OuterLoop.Edges);

            foreach (var innerLoop in polygon.InnerLoops)

            {

                var loopClone = new Loop();

                loopClone.Edges.AddRange(innerLoop.Edges);

                clone.InnerLoops.Add(loopClone);

            }



            session.Document.Polygons.Add(clone);

            session.Selection.SelectedPolygonIds.Add(clone.Id);

        }



        SyncFaces(session);

        return true;

    }



    private static void RemoveSelection(CadSession session)

    {

        var document = session.Document;

        var selection = session.Selection;

        var tolerance = MathUtils.SnapToleranceWorld(session.Camera.Zoom);

        var removedEdgeIds = document.Edges

            .Where(edge => selection.SelectedEdgeIds.Contains(edge.Id))

            .Select(edge => edge.Id)

            .ToList();



        SuppressSelectedFaces(document, selection, tolerance);



        document.Edges.RemoveAll(edge => selection.SelectedEdgeIds.Contains(edge.Id));

        document.Axes.RemoveAll(axis => selection.SelectedAxisIds.Contains(axis.Id));

        document.Polygons.RemoveAll(polygon => selection.SelectedPolygonIds.Contains(polygon.Id));



        if (removedEdgeIds.Count > 0)

        {

            PolygonBuilder.InvalidateSuppressionOnEdgeTopologyChange(document);

            TopologyService.PruneUnusedVertices(document);

        }



        SyncFaces(session);

    }



    private static void SuppressSelectedFaces(CadDocument document, Selection selection, double tolerance)

    {

        foreach (var polygon in document.Polygons)

        {

            if (!selection.SelectedPolygonIds.Contains(polygon.Id) || polygon.Type != PolygonType.Face)

            {

                continue;

            }



            PolygonBuilder.SuppressFaceGeometry(document, polygon, tolerance);
            FaceFillService.RemoveFill(document, polygon);

        }

    }



    private static void SyncFaces(CadSession session)

    {

        PolygonBuilder.SyncFaces(session.Document, TopologyTolerance.ForMutation);

    }



    private static PointF GetSelectionCenter(CadSession session)

    {

        var points = new List<PointF>();



        foreach (var edge in session.Document.Edges)

        {

            if (session.Selection.SelectedEdgeIds.Contains(edge.Id))

            {

                points.Add(TopologyService.GetEdgeStartPoint(session.Document, edge));

                points.Add(TopologyService.GetEdgeEndPoint(session.Document, edge));

            }

        }



        foreach (var axis in session.Document.Axes)
        {
            if (session.Selection.SelectedAxisIds.Contains(axis.Id))
            {
                points.Add(axis.Start);
                points.Add(axis.End);
            }
        }



        foreach (var polygon in session.Document.Polygons)

        {

            if (!session.Selection.SelectedPolygonIds.Contains(polygon.Id))

            {

                continue;

            }



            points.AddRange(PolygonGeometry.GetOuterBoundaryPoints(session.Document, polygon));

        }



        if (points.Count == 0)

        {

            return PointF.Zero;

        }



        return new PointF(

            points.Average(point => point.X),

            points.Average(point => point.Y));

    }



    private static PointF Translate(PointF point, PointF delta)

        => new(point.X + delta.X, point.Y + delta.Y);

}

