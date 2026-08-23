using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using LiteCad.Resources;
using LiteCad.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public interface ITool
{
    ToolId Id { get; }

    string Name { get; }

    void OnActivated();

    void OnDeactivated();

    void OnMouseDown(MouseButtonEventArgs e, PointF world);

    void OnMouseMove(MouseEventArgs e, PointF world);

    void OnMouseUp(MouseButtonEventArgs e, PointF world);

    void OnKeyDown(KeyEventArgs e);

    void OnTextInput(TextCompositionEventArgs e);

    bool CapturesTextInput { get; }

    bool TryApplyLength(double length);

    bool TryApplyLengthInput(string input);

    bool TryApplyRectangleSize(string width, string height);

    void RenderOverlay(DrawingContext context, Camera camera, Size viewport);
}

public abstract class ToolBase : ITool
{
    protected ToolContext? Context { get; private set; }

    public abstract ToolId Id { get; }

    public string Name => ToolDisplayNames.Get(Id);

    internal void AttachContext(ToolContext context) => Context = context;

    public virtual void OnActivated() { }

    public virtual void OnDeactivated() { }

    public virtual void OnMouseDown(MouseButtonEventArgs e, PointF world) { }

    public virtual void OnMouseMove(MouseEventArgs e, PointF world) { }

    public virtual void OnMouseUp(MouseButtonEventArgs e, PointF world) { }

    public virtual void OnKeyDown(KeyEventArgs e) { }

    public virtual void OnTextInput(TextCompositionEventArgs e) { }

    public virtual bool CapturesTextInput => false;

    public virtual bool TryApplyLength(double length) => false;

    public virtual bool TryApplyLengthInput(string input) => false;

    public virtual bool TryApplyRectangleSize(string width, string height) => false;

    public virtual void RenderOverlay(DrawingContext context, Camera camera, Size viewport) { }
}
