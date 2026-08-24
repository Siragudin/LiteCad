using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class ToolAltRightClickTests
{
    [Fact]
    public void AltRightClick_SwitchesAnyToolToSelection()
    {
        WpfTestUtilities.RunSta(() =>
        {
            using var harness = new Harness();
            harness.Service.ActivateTool(harness.Line);

            Assert.True(harness.Service.TryHandleAltRightClick(CreateRightClick(), ModifierKeys.Alt));
            Assert.Equal(1, harness.SelectionActivateCount);
            Assert.Same(harness.Selection, harness.Service.ActiveTool);
        });
    }

    [Fact]
    public void RightClickWithoutAlt_DoesNotSwitchTool()
    {
        WpfTestUtilities.RunSta(() =>
        {
            using var harness = new Harness();
            harness.Service.ActivateTool(harness.Line);

            Assert.False(harness.Service.TryHandleAltRightClick(CreateRightClick(), ModifierKeys.None));
            Assert.Equal(0, harness.SelectionActivateCount);
            Assert.Same(harness.Line, harness.Service.ActiveTool);
        });
    }

    [Fact]
    public void AltRightClick_WhenAlreadySelection_DoesNotReactivate()
    {
        WpfTestUtilities.RunSta(() =>
        {
            using var harness = new Harness();
            harness.Service.ActivateTool(harness.Selection);

            Assert.True(harness.Service.TryHandleAltRightClick(CreateRightClick(), ModifierKeys.Alt));
            Assert.Equal(0, harness.SelectionActivateCount);
            Assert.Same(harness.Selection, harness.Service.ActiveTool);
        });
    }

    private static MouseButtonEventArgs CreateRightClick()
        => new(Mouse.PrimaryDevice, 0, MouseButton.Right)
        {
            RoutedEvent = UIElement.MouseRightButtonDownEvent
        };

    private sealed class Harness : IDisposable
    {
        public Harness()
        {
            Session = new CadSession();
            Selection = new SelectionTool();
            Line = new LineTool();
            Service = Session.ToolService;
            Service.Initialize(new ToolContext(
                Session,
                () => new Size(800, 600),
                screen => Session.Camera.ScreenToWorld(screen, new Size(800, 600)),
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                () => { },
                activateSelectionTool: () =>
                {
                    SelectionActivateCount++;
                    Service.ActivateTool(Selection);
                }));
        }

        public CadSession Session { get; }

        public ToolService Service { get; }

        public SelectionTool Selection { get; }

        public LineTool Line { get; }

        public int SelectionActivateCount { get; private set; }

        public void Dispose()
        {
        }
    }
}
