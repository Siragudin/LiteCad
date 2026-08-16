using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
using LiteCad.UI.Layout;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace LiteCad.Tests;

public class RectangleSizeInputTests
{
    [Fact]
    public void FirstClick_ActivatesWidthField()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.FirstClick(new PointF(0, 0));

            Assert.True(host.StatusBar.IsRectangleInputActive);
            Assert.Equal(RectangleSizeField.Width, host.StatusBar.ActiveRectangleField);
            Assert.True(host.StatusBar.IsWidthFieldFocused);
        });
    }

    [Fact]
    public void Alt_SwitchesFromWidthToHeight()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.FirstClick(new PointF(0, 0));
            host.PressAlt();

            Assert.Equal(RectangleSizeField.Height, host.StatusBar.ActiveRectangleField);
            Assert.True(host.StatusBar.IsHeightFieldFocused);
        });
    }

    [Fact]
    public void Alt_SwitchesBackToWidth()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.FirstClick(new PointF(0, 0));
            host.PressAlt();
            host.PressAlt();

            Assert.Equal(RectangleSizeField.Width, host.StatusBar.ActiveRectangleField);
            Assert.True(host.StatusBar.IsWidthFieldFocused);
        });
    }

    [Fact]
    public void Alt_RepeatedTenTimes_AlternatesFields()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.FirstClick(new PointF(0, 0));

            var expected = RectangleSizeField.Height;
            for (var i = 0; i < 10; i++)
            {
                host.PressAlt();
                Assert.Equal(expected, host.StatusBar.ActiveRectangleField);
                expected = expected == RectangleSizeField.Width
                    ? RectangleSizeField.Height
                    : RectangleSizeField.Width;
            }
        });
    }

    [Fact]
    public void Alt_PreservesWidthText()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.FirstClick(new PointF(0, 0));
            host.StatusBar.SetRectangleWidthInputText("4000");
            host.PressAlt();
            host.StatusBar.SetRectangleHeightInputText("3000");
            host.PressAlt();

            Assert.Equal("4000", host.StatusBar.RectangleWidthText);
        });
    }

    [Fact]
    public void Alt_PreservesHeightText()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.FirstClick(new PointF(0, 0));
            host.StatusBar.SetRectangleWidthInputText("4000");
            host.PressAlt();
            host.StatusBar.SetRectangleHeightInputText("3000");
            host.PressAlt();
            host.PressAlt();

            Assert.Equal("3000", host.StatusBar.RectangleHeightText);
        });
    }

    [Fact]
    public void Alt_DoesNotCreateRectangle()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.FirstClick(new PointF(0, 0));
            host.StatusBar.SetRectangleWidthInputText("4000");
            host.PressAlt();
            host.StatusBar.SetRectangleHeightInputText("3000");
            host.PressAlt();

            Assert.Empty(host.Session.Document.Edges);
            Assert.Empty(host.Session.Document.Vertices);
        });
    }

    [Fact]
    public void Alt_DoesNotCancelRectangle()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.FirstClick(new PointF(0, 0));
            host.PressAlt();
            host.PressAlt();

            host.Move(new PointF(100, 80));
            Assert.Empty(host.Session.Document.Edges);
        });
    }

    [Fact]
    public void AfterAltSwitch_CanContinueTyping()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.FirstClick(new PointF(0, 0));
            host.StatusBar.SetRectangleWidthInputText("4000");
            host.PressAlt();
            host.StatusBar.SetRectangleHeightInputText("3000");

            Assert.Equal("4000", host.StatusBar.RectangleWidthText);
            Assert.Equal("3000", host.StatusBar.RectangleHeightText);
        });
    }

    [Fact]
    public void Enter_AppliesTypedSizes()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.FirstClick(new PointF(0, 0));
            host.StatusBar.SetRectangleWidthInputText("4000");
            host.StatusBar.SetRectangleHeightInputText("3000");
            host.CommitRectangleSize();

            Assert.Equal(4, host.Session.Document.Edges.Count);
            Assert.Equal(4, host.Session.Document.Vertices.Count);
            Assert.Single(host.Session.Document.Polygons);
        });
    }

    [Fact]
    public void Escape_CancelsRectangle()
    {
        RunSta(() =>
        {
            using var host = CreateHost();
            host.FirstClick(new PointF(0, 0));
            host.StatusBar.SetRectangleWidthInputText("4000");
            host.PressEscape();

            Assert.False(host.StatusBar.IsRectangleInputActive);
            Assert.Empty(host.Session.Document.Edges);
        });
    }

    private static RectangleSizeInputTestHost CreateHost() => new();

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

    private sealed class RectangleSizeInputTestHost : IDisposable
    {
        private readonly Window _window;

        public RectangleSizeInputTestHost()
        {
            Session = new CadSession();
            RectangleTool = new RectangleTool();
            StatusBar = new StatusBar();
            _window = new Window
            {
                Content = StatusBar,
                Width = 800,
                Height = 120,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Visibility = Visibility.Hidden
            };
            _window.Show();

            var context = new ToolContext(
                Session,
                () => new Size(800, 600),
                screen => Session.Camera.ScreenToWorld(screen, new Size(800, 600)),
                _ => new Point(0, 0),
                () => { },
                () => { },
                () => { },
                setRectangleSizeInputEnabled: enabled => StatusBar.SetRectangleSizeInputEnabled(enabled),
                setRectangleSizePreview: StatusBar.SetRectangleSizePreview,
                resetRectangleSizeInput: StatusBar.ResetRectangleSizeInput,
                processRectangleSizeKey: StatusBar.ProcessRectangleSizeKey);

            Session.ToolService.Initialize(context);
            Session.ToolService.ActivateTool(RectangleTool);

            StatusBar.RectangleSizeCommitted += (_, sizes) =>
                RectangleTool.TryApplyRectangleSize(sizes.Width, sizes.Height);
        }

        public CadSession Session { get; }

        public StatusBar StatusBar { get; }

        private RectangleTool RectangleTool { get; }

        public void FirstClick(PointF world)
            => RectangleTool.OnMouseDown(CreateMouseDown(), world);

        public void Move(PointF world)
            => RectangleTool.OnMouseMove(CreateMouseMove(), world);

        public void PressEscape()
        {
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(_window),
                0,
                Key.Escape)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };

            RectangleTool.OnKeyDown(args);
        }

        public void PressAlt()
        {
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(_window),
                0,
                Key.LeftAlt)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };

            StatusBar.ProcessRectangleSizeKey(args);
        }

        public void CommitRectangleSize()
        {
            var args = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(_window),
                0,
                Key.Enter)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            };

            StatusBar.ProcessRectangleSizeKey(args);
        }

        public void Dispose()
        {
            _window.Close();
        }

        private static MouseButtonEventArgs CreateMouseDown()
            => new(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent
            };

        private static MouseEventArgs CreateMouseMove()
            => new(Mouse.PrimaryDevice, 0)
            {
                RoutedEvent = UIElement.MouseMoveEvent
            };
    }
}
