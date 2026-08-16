using LiteCad.Core.Geometry;
using System.Windows;
using System.Windows.Input;

namespace LiteCad.Tools;

public sealed class HandTool : ToolBase
{
    private bool _isPanning;
    private Point _lastScreen;

    public override string Name => "Hand";

    public override void OnDeactivated()
    {
        EndPan();
    }

    public override void OnMouseDown(MouseButtonEventArgs e, PointF world)
    {
        if (Context is null || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _isPanning = true;
        _lastScreen = Context.GetMousePositionOnViewport(e);
        Context.CaptureMouse();
        e.Handled = true;
    }

    public override void OnMouseMove(MouseEventArgs e, PointF world)
    {
        if (Context is null || !_isPanning)
        {
            return;
        }

        var current = Context.GetMousePositionOnViewport(e);
        var deltaX = current.X - _lastScreen.X;
        var deltaY = current.Y - _lastScreen.Y;
        Context.Session.Camera.PanScreen(deltaX, deltaY);
        _lastScreen = current;
        Context.RequestRedraw();
        e.Handled = true;
    }

    public override void OnMouseUp(MouseButtonEventArgs e, PointF world)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        EndPan();
        e.Handled = true;
    }

    private void EndPan()
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        Context?.ReleaseMouseCapture();
    }
}
