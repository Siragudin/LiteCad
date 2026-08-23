using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Leaders;
using LiteCad.Resources;
using LiteCad.Services;
using LiteCad.UI;

namespace LiteCad.UI;

internal static class SelectionUiFormatter
{
    public static string FormatSelectionInfo(CadSession session)
    {
        var document = session.Document;
        var selection = session.Selection;
        var unit = session.DisplayUnitSettings.LinearUnit;
        var edgeCount = selection.SelectedEdgeIds.Count;
        var polygonCount = selection.SelectedPolygonIds.Count;
        var vertexCount = selection.SelectedVertexIds.Count;
        var dimensionCount = selection.SelectedDimensionIds.Count;
        var axisCount = selection.SelectedAxisIds.Count;

        var leaderCount = selection.SelectedLeaderIds.Count;

        if (edgeCount == 0 && polygonCount == 0 && vertexCount == 0 && dimensionCount == 0 && axisCount == 0 && leaderCount == 0)
        {
            return Strings.Selection_NothingSelected;
        }

        if (leaderCount == 1 && edgeCount == 0 && polygonCount == 0 && vertexCount == 0 && dimensionCount == 0 && axisCount == 0)
        {
            var leader = document.Leaders.First(item => selection.SelectedLeaderIds.Contains(item.Id));
            return Strings.Format(Strings.Selection_LeaderWithText, leader.Text);
        }

        if (dimensionCount == 1 && edgeCount == 0 && polygonCount == 0 && vertexCount == 0)
        {
            var dimension = document.Dimensions.First(item => selection.SelectedDimensionIds.Contains(item.Id));
            var measured = DimensionService.GetMeasuredDistance(document, dimension);
            return Strings.Format(
                Strings.Selection_DimensionWithDistance,
                UnitDisplayFormatter.FormatLinear(measured, unit),
                UnitDisplayFormatter.FormatLinear(dimension.Offset, unit));
        }

        if (vertexCount == 1 && edgeCount == 0 && polygonCount == 0 && dimensionCount == 0)
        {
            var vertex = document.Vertices.First(item => selection.SelectedVertexIds.Contains(item.Id));
            return Strings.Format(
                Strings.Selection_VertexAt,
                UnitDisplayFormatter.FormatCoordinate(vertex.Position.X, unit),
                UnitDisplayFormatter.FormatCoordinate(vertex.Position.Y, unit));
        }

        if (axisCount == 1 && edgeCount == 0 && polygonCount == 0 && vertexCount == 0 && dimensionCount == 0)
        {
            var axis = document.Axes.First(item => selection.SelectedAxisIds.Contains(item.Id));
            var length = MathUtils.Distance(axis.Start, axis.End);
            return Strings.Format(
                Strings.Selection_AxisWithLength,
                UnitDisplayFormatter.FormatLinear(length, unit));
        }

        if (edgeCount == 1 && polygonCount == 0 && vertexCount == 0 && dimensionCount == 0 && axisCount == 0)
        {
            return Strings.Selection_Edge;
        }

        if (polygonCount == 1 && edgeCount == 0 && vertexCount == 0 && dimensionCount == 0)
        {
            var polygon = document.Polygons.First(item => selection.SelectedPolygonIds.Contains(item.Id));
            var area = PolygonGeometry.GetArea(document, polygon, MathUtils.DefaultTolerance);
            return Strings.Format(
                Strings.Selection_PolygonWithArea,
                PolygonTypeDisplay.Get(polygon.Type),
                UnitDisplayFormatter.FormatArea(area));
        }

        return Strings.Format(Strings.Selection_MultipleCount, vertexCount, edgeCount, polygonCount);
    }
}
