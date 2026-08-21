using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class SelectionDimensionPickTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void Click_OnDimensionLine_SelectsDimensionNotEdge()
    {
        RunSta(() =>
        {
            var session = new CadSession();
            var document = session.Document;
            var edge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
            DimensionService.Create(document, edge.StartVertexId, edge.EndVertexId, 10, Tol);

            var harness = new SelectionToolHarness(session);
            harness.ToolService.ActivateTool(harness.SelectionTool);

            var pickPoint = new PointF(50, 10);
            harness.SelectionTool.OnMouseDown(CreateMouseButton(MouseButton.Left), pickPoint);
            harness.SelectionTool.OnMouseUp(CreateMouseButton(MouseButton.Left, isUp: true), pickPoint);

            Assert.Single(session.Selection.SelectedDimensionIds);
            Assert.Empty(session.Selection.SelectedEdgeIds);
            Assert.Empty(session.Selection.SelectedVertexIds);
        });
    }

    private static void RunSta(Action action) => WpfTestUtilities.RunSta(action);

    private static MouseButtonEventArgs CreateMouseButton(MouseButton button, bool isUp = false)
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, button)
        {
            RoutedEvent = isUp ? UIElement.MouseLeftButtonUpEvent : UIElement.MouseLeftButtonDownEvent
        };
        return args;
    }

    private sealed class SelectionToolHarness
    {
        public SelectionToolHarness(CadSession session)
        {
            Session = session;
            ToolService = session.ToolService;
            SelectionTool = new SelectionTool();
            ToolService.Initialize(new ToolContext(
                Session,
                () => new Size(800, 600),
                _ => PointF.Zero,
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { }));
        }

        public CadSession Session { get; }

        public ToolService ToolService { get; }

        public SelectionTool SelectionTool { get; }
    }
}
