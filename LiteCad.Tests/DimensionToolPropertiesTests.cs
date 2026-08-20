using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using LiteCad.UI.Layout;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class DimensionToolPropertiesTests
{
    private const double Tol = 1e-4;

    [Fact]
    public void DimensionProperties_VisibleWhenToolActiveWithoutSelection()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateDimension();

            Assert.True(host.PropertiesPanel.IsDimensionToolPanelVisible);
            Assert.False(host.PropertiesPanel.IsDimensionSelectionPanelVisible);
            Assert.Equal(DimensionExtensionStyle.Full, host.Session.DimensionToolOptions.ExtensionStyle);
        });
    }

    [Fact]
    public void DimensionToolExtensionStyle_PersistsWhenSwitchingTools()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateDimension();
            host.SetToolExtensionStyle(DimensionExtensionStyle.Short);

            host.ActivateLine();
            Assert.False(host.PropertiesPanel.IsDimensionToolPanelVisible);

            host.ActivateDimension();
            Assert.True(host.PropertiesPanel.IsDimensionToolPanelVisible);
            Assert.Equal(DimensionExtensionStyle.Short, host.Session.DimensionToolOptions.ExtensionStyle);
        });
    }

    [Fact]
    public void DimensionToolExtensionStyle_DoesNotChangeExistingDimensions()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            var document = host.Session.Document;
            var edge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
            var dimension = DimensionService.Create(document, edge.StartVertexId, edge.EndVertexId, 50, Tol);

            host.ActivateDimension();
            host.SetToolExtensionStyle(DimensionExtensionStyle.Short);

            Assert.Equal(DimensionExtensionStyle.Full, dimension.ExtensionStyle);
        });
    }

    [Fact]
    public void CreateDimension_UsesToolExtensionStyle()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.ActivateDimension();
            host.SetToolExtensionStyle(DimensionExtensionStyle.Short);
            host.CreateHorizontalDimension(new PointF(0, 0), new PointF(100, 0), 50);

            var dimension = host.Session.Document.Dimensions.Single();
            Assert.Equal(DimensionExtensionStyle.Short, dimension.ExtensionStyle);
        });
    }

    [Fact]
    public void SelectedDimension_ShowsSelectionPanelOnlyInSelectionTool()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            var document = host.Session.Document;
            var edge = TestDocumentHelpers.AddEdge(document, new PointF(0, 0), new PointF(100, 0), Tol);
            var dimension = DimensionService.Create(document, edge.StartVertexId, edge.EndVertexId, 50, Tol);
            host.Session.Selection.SelectedDimensionIds.Add(dimension.Id);

            host.ActivateSelection();
            host.PropertiesPanel.SyncDimensionSelection(host.Session);
            Assert.True(host.PropertiesPanel.IsDimensionSelectionPanelVisible);

            host.ActivateDimension();
            Assert.False(host.PropertiesPanel.IsDimensionSelectionPanelVisible);
            Assert.True(host.PropertiesPanel.IsDimensionToolPanelVisible);
        });
    }

    private static void RunSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }

    private static DimensionPropertiesHost CreateHost()
        => new();

    private static MouseButtonEventArgs CreateMouseButton(MouseButton button, bool isUp = false)
        => new(Mouse.PrimaryDevice, 0, button)
        {
            RoutedEvent = isUp ? UIElement.MouseLeftButtonUpEvent : UIElement.MouseLeftButtonDownEvent
        };

    private sealed class DimensionPropertiesHost : IDisposable
    {
        public DimensionPropertiesHost()
        {
            Session = new CadSession();
            ToolService = Session.ToolService;
            DimensionTool = new DimensionTool();
            SelectionTool = new SelectionTool();
            PropertiesPanel = new PropertiesPanel();

            var context = new ToolContext(
                Session,
                () => ViewportSize,
                _ => PointF.Zero,
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                recordUndo: () => Session.History.Record(Session.Document));

            ToolService.Initialize(context);
            PropertiesPanel.BindSession(Session);
        }

        public CadSession Session { get; }

        public ToolService ToolService { get; }

        public DimensionTool DimensionTool { get; }

        public SelectionTool SelectionTool { get; }

        public PropertiesPanel PropertiesPanel { get; }

        public Size ViewportSize { get; } = new(800, 600);

        public bool IsDimensionSelectionPanelVisible
            => PropertiesPanel.IsDimensionSelectionPanelVisible;

        public void ActivateDimension()
        {
            ToolService.ActivateTool(DimensionTool);
            PropertiesPanel.SetActiveTool(ToolId.Dimension);
        }

        public void ActivateLine()
        {
            ToolService.ActivateTool(new LineTool());
            PropertiesPanel.SetActiveTool(ToolId.Line);
        }

        public void ActivateSelection()
        {
            ToolService.ActivateTool(SelectionTool);
            PropertiesPanel.SetActiveTool(ToolId.Selection);
        }

        public void SetToolExtensionStyle(DimensionExtensionStyle extensionStyle)
            => PropertiesPanel.SetDimensionToolExtensionStyle(extensionStyle);

        public void CreateHorizontalDimension(PointF start, PointF end, double offset)
        {
            TestDocumentHelpers.AddEdge(Session.Document, start, end, Tol);
            DimensionTool.OnMouseDown(CreateMouseButton(MouseButton.Left), start);
            DimensionTool.OnMouseDown(CreateMouseButton(MouseButton.Left), end);
            DimensionTool.TryApplyLength(offset);
        }

        public void Dispose()
        {
        }
    }
}
