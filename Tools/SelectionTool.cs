using LiteCad.Core.Document;

using LiteCad.Core.Geometry;

using LiteCad.Rendering;

using LiteCad.Services;

using System.Windows;

using System.Windows.Input;

using System.Windows.Media;



namespace LiteCad.Tools;



public sealed class SelectionTool : ToolBase

{

    public override string Name => "Selection";



    public override void OnMouseDown(MouseButtonEventArgs e, PointF world)

    {

        if (Context is null || e.ChangedButton != MouseButton.Left)

        {

            return;

        }



        var document = Context.Session.Document;

        var selection = Context.Session.Selection;

        selection.Clear();



        var tolerance = Context.SnapTolerance;



        Edge? closestEdge = null;

        var closestDistance = tolerance;

        foreach (var edge in document.Edges)

        {

            var start = TopologyService.GetEdgeStartPoint(document, edge);

            var end = TopologyService.GetEdgeEndPoint(document, edge);

            if (!Geometry2D.TryProjectPointOnSegment(world, start, end, out _, out var distance, tolerance) ||

                distance > closestDistance)

            {

                continue;

            }



            closestEdge = edge;

            closestDistance = distance;

        }



        if (closestEdge is not null)

        {

            selection.SelectedEdgeIds.Add(closestEdge.Id);

            var length = MathUtils.Distance(

                TopologyService.GetEdgeStartPoint(document, closestEdge),

                TopologyService.GetEdgeEndPoint(document, closestEdge));

            Context.SetLength(length);

            Context.SetArea(null);

            Context.SetSelectionInfo("Edge");

            Context.SetStatus("Edge selected");

            Context.RequestRedraw();

            e.Handled = true;

            return;

        }



        Polygon? closestPolygon = null;

        var closestArea = double.MaxValue;

        foreach (var polygon in document.Polygons)

        {

            if (polygon.OuterLoop.Edges.Count < 3 || !PolygonGeometry.ContainsPoint(document, polygon, world, tolerance))

            {

                continue;

            }



            var area = PolygonGeometry.GetArea(document, polygon, tolerance);

            if (area >= closestArea)

            {

                continue;

            }



            closestPolygon = polygon;

            closestArea = area;

        }



        if (closestPolygon is not null)

        {

            selection.SelectedPolygonIds.Add(closestPolygon.Id);

            Context.SetArea(closestArea);

            Context.SetSelectionInfo($"{closestPolygon.Type} ({closestArea:F2})");

            Context.SetStatus("Polygon selected");

            Context.RequestRedraw();

            e.Handled = true;

            return;

        }



        Context.SetArea(null);

        Context.SetLength(null);

        Context.SetSelectionInfo("Nothing selected");

        Context.SetStatus("Selection cleared");

        Context.RequestRedraw();

        e.Handled = true;

    }

}

