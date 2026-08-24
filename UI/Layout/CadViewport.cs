using LiteCad.Infrastructure;
using LiteCad.Rendering;
using LiteCad.Tools;
using System.Windows;
using System.Windows.Media;

namespace LiteCad.UI.Layout;

public sealed class CadViewport : FrameworkElement
{
    public static readonly DependencyProperty SessionProperty =
        DependencyProperty.Register(
            nameof(Session),
            typeof(CadSession),
            typeof(CadViewport),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSessionChanged));

    private ViewportRenderPass _nextPass = ViewportRenderPass.Full;

    public CadSession? Session
    {
        get => (CadSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public CadViewport()
    {
        SnapsToDevicePixels = true;
        ClipToBounds = true;
        SizeChanged += (_, _) => RequestFullRedraw();
    }

    private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CadViewport viewport)
        {
            return;
        }

        if (e.OldValue is CadSession oldSession)
        {
            oldSession.Camera.Changed -= viewport.OnCameraChanged;
            oldSession.DisplayUnitSettings.Changed -= viewport.OnDisplayUnitsChanged;
        }

        if (e.NewValue is CadSession newSession)
        {
            newSession.Camera.Changed += viewport.OnCameraChanged;
            newSession.DisplayUnitSettings.Changed += viewport.OnDisplayUnitsChanged;
        }

        viewport.RequestFullRedraw();
    }

    private void OnCameraChanged()
        => RequestFullRedraw();

    private void OnDisplayUnitsChanged()
        => RequestFullRedraw();

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (Session is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var pass = _nextPass;
        _nextPass = ViewportRenderPass.OverlayOnly;

        var viewport = new Size(ActualWidth, ActualHeight);
        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, viewport.Width, viewport.Height)));

        try
        {
            Session.Renderer.Render(
                drawingContext,
                Session.Document,
                Session.Selection,
                Session.Camera,
                viewport,
                Session.ToolService.ActiveTool,
                Session.DisplayUnitSettings.LinearUnit,
                pass);
        }
        finally
        {
            drawingContext.Pop();
        }
    }

    public void RequestFullRedraw()
    {
        _nextPass = ViewportRenderPass.Full;
        InvalidateVisual();
    }

    public void RequestOverlayRedraw()
    {
        _nextPass = ViewportRenderPass.OverlayOnly;
        InvalidateVisual();
    }

    public void RequestRedraw()
        => RequestFullRedraw();
}
