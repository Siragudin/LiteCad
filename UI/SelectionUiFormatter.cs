using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Resources;

namespace LiteCad.UI;

internal static class SelectionUiFormatter
{
    public static string FormatSelectionInfo(CadSession session)
    {
        var document = session.Document;
        var selection = session.Selection;
        var edgeCount = selection.SelectedEdgeIds.Count;
        var polygonCount = selection.SelectedPolygonIds.Count;
        var vertexCount = selection.SelectedVertexIds.Count;

        if (edgeCount == 0 && polygonCount == 0 && vertexCount == 0)
        {
            return Strings.Selection_NothingSelected;
        }

        if (vertexCount == 1 && edgeCount == 0 && polygonCount == 0)
        {
            var vertex = document.Vertices.First(item => selection.SelectedVertexIds.Contains(item.Id));
            return Strings.Format(Strings.Selection_VertexAt, vertex.Position.X, vertex.Position.Y);
        }

        if (edgeCount == 1 && polygonCount == 0 && vertexCount == 0)
        {
            return Strings.Selection_Edge;
        }

        if (polygonCount == 1 && edgeCount == 0 && vertexCount == 0)
        {
            var polygon = document.Polygons.First(item => selection.SelectedPolygonIds.Contains(item.Id));
            var area = PolygonGeometry.GetArea(document, polygon, MathUtils.DefaultTolerance);
            return Strings.Format(Strings.Selection_PolygonWithArea, PolygonTypeDisplay.Get(polygon.Type), area);
        }

        return Strings.Format(Strings.Selection_MultipleCount, vertexCount, edgeCount, polygonCount);
    }
}
