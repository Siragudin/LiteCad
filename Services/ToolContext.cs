using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using System.Windows.Input;
using System.Windows;

namespace LiteCad.Services;

public sealed class ToolContext
{
    public ToolContext(
        CadSession session,
        Func<Size> getViewportSize,
        Func<Point, PointF> screenToWorld,
        Action requestRedraw,
        Action<string>? setStatus = null,
        Action<double?>? setLength = null,
        Action<double?>? setArea = null,
        Action<string>? setSelectionInfo = null,
        Action<bool>? setLengthInputEnabled = null,
        Action<double?>? resetLengthInput = null,
        Func<KeyEventArgs, bool>? processLengthKey = null,
        Action? recordUndo = null)
    {
        Session = session;
        GetViewportSize = getViewportSize;
        ScreenToWorld = screenToWorld;
        RequestRedraw = requestRedraw;
        SetStatus = setStatus ?? (_ => { });
        SetLength = setLength ?? (_ => { });
        SetArea = setArea ?? (_ => { });
        SetSelectionInfo = setSelectionInfo ?? (_ => { });
        SetLengthInputEnabled = setLengthInputEnabled ?? (_ => { });
        ResetLengthInput = resetLengthInput ?? (_ => { });
        ProcessLengthKey = processLengthKey ?? (_ => false);
        RecordUndo = recordUndo ?? (() => { });
    }

    public CadSession Session { get; }

    public Func<Size> GetViewportSize { get; }

    public Func<Point, PointF> ScreenToWorld { get; }

    public Action RequestRedraw { get; }

    public Action<string> SetStatus { get; }

    public Action<double?> SetLength { get; }

    public Action<double?> SetArea { get; }

    public Action<string> SetSelectionInfo { get; }

    public Action<bool> SetLengthInputEnabled { get; }

    public Action<double?> ResetLengthInput { get; }

    public Func<KeyEventArgs, bool> ProcessLengthKey { get; }

    public Action RecordUndo { get; }

    public double SnapTolerance
        => MathUtils.SnapToleranceWorld(Session.Camera.Zoom);
}
