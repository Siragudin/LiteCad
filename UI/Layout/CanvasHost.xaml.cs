using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using System.Windows;
using System.Windows.Input;

namespace LiteCad.UI.Layout;

public partial class CanvasHost : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty SessionProperty =
        DependencyProperty.Register(
            nameof(Session),
            typeof(CadSession),
            typeof(CanvasHost),
            new PropertyMetadata(null, OnSessionChanged));

    private bool _isPanning;
    private bool _isSpaceDown;
    private Point _lastPanScreen;

    public CadSession? Session
    {
        get => (CadSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public event EventHandler<PointEventArgs>? MouseWorldPositionChanged;

    public CanvasHost()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CanvasHost host)
        {
            host.Viewport.Session = e.NewValue as CadSession;
        }
    }

    public void InitializeTools(ToolContext toolContext)
    {
        Session?.ToolService.Initialize(toolContext);
    }

    public Size GetViewportSize() => new(Viewport.ActualWidth, Viewport.ActualHeight);

    public Point GetMousePositionOnViewport(MouseEventArgs e) => e.GetPosition(Viewport);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
        Viewport.Session ??= Session;
        Viewport.RequestRedraw();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Space && !_isSpaceDown)
        {
            _isSpaceDown = true;
            Cursor = Cursors.Hand;
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
        Session?.ToolService.ActiveTool?.OnKeyDown(e);
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            _isSpaceDown = false;
            if (!_isPanning)
            {
                Cursor = Cursors.Arrow;
            }

            e.Handled = true;
            return;
        }

        base.OnPreviewKeyUp(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isPanning)
        {
            var position = e.GetPosition(Viewport);
            Session?.Camera.PanScreen(position.X - _lastPanScreen.X, position.Y - _lastPanScreen.Y);
            _lastPanScreen = position;
            e.Handled = true;
            return;
        }

        base.OnMouseMove(e);

        var world = GetWorldPoint(e);
        MouseWorldPositionChanged?.Invoke(this, new PointEventArgs(world.X, world.Y));
        Session?.ToolService.ActiveTool?.OnMouseMove(e, world);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        Focus();

        if (Session?.ToolService.TryHandleAltRightClick(e, Keyboard.Modifiers) == true)
        {
            e.Handled = true;
            return;
        }

        if (IsPanGesture(e))
        {
            _isPanning = true;
            _lastPanScreen = e.GetPosition(Viewport);
            CaptureMouse();
            Cursor = Cursors.Hand;
            e.Handled = true;
            return;
        }

        base.OnMouseDown(e);
        Session?.ToolService.ActiveTool?.OnMouseDown(e, GetWorldPoint(e));
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_isPanning && (e.ChangedButton == MouseButton.Middle ||
            (_isSpaceDown && e.ChangedButton == MouseButton.Left)))
        {
            _isPanning = false;
            ReleaseMouseCapture();
            Cursor = _isSpaceDown ? Cursors.Hand : Cursors.Arrow;
            e.Handled = true;
            return;
        }

        base.OnMouseUp(e);
        Session?.ToolService.ActiveTool?.OnMouseUp(e, GetWorldPoint(e));
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (Session is null)
        {
            return;
        }

        var factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
        Session.Camera.ZoomAt(e.GetPosition(Viewport), factor, GetViewportSize());
        e.Handled = true;
    }

    public void RequestRedraw()
    {
        Viewport.RequestRedraw();
    }

    private PointF GetWorldPoint(MouseEventArgs e)
    {
        if (Session is null)
        {
            return PointF.Zero;
        }

        return Session.Camera.ScreenToWorld(e.GetPosition(Viewport), GetViewportSize());
    }

    private bool IsPanGesture(MouseButtonEventArgs e)
        => e.ChangedButton == MouseButton.Middle || (_isSpaceDown && e.ChangedButton == MouseButton.Left);
}

public sealed class PointEventArgs(double x, double y) : EventArgs
{
    public double X { get; } = x;

    public double Y { get; } = y;
}
