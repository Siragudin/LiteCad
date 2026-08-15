using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public abstract class ToolBase : ITool
{
    protected ToolContext? Context { get; private set; }

    public abstract string Name { get; }

    internal void AttachContext(ToolContext context) => Context = context;

    public virtual void OnActivated() { }

    public virtual void OnDeactivated() { }

    public virtual void OnMouseDown(MouseButtonEventArgs e, PointF world) { }

    public virtual void OnMouseMove(MouseEventArgs e, PointF world) { }

    public virtual void OnMouseUp(MouseButtonEventArgs e, PointF world) { }

    public virtual void OnKeyDown(KeyEventArgs e) { }

    public virtual bool TryApplyLength(double length) => false;

    public virtual void RenderOverlay(DrawingContext context, Camera camera, Size viewport) { }
}
