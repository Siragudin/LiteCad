using LiteCad.Core.Geometry;
using LiteCad.Rendering;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.Tools;

public interface ITool
{
    string Name { get; }

    void OnActivated();

    void OnDeactivated();

    void OnMouseDown(MouseButtonEventArgs e, PointF world);

    void OnMouseMove(MouseEventArgs e, PointF world);

    void OnMouseUp(MouseButtonEventArgs e, PointF world);

    void OnKeyDown(KeyEventArgs e);

    bool TryApplyLength(double length);

    bool TryApplyLengthInput(string input);

    bool TryApplyRectangleSize(string width, string height);

    void RenderOverlay(DrawingContext context, Camera camera, Size viewport);
}
