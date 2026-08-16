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
        Func<MouseEventArgs, Point> getMousePositionOnViewport,
        Action requestRedraw,
        Action captureMouse,
        Action releaseMouseCapture,
        Action<string>? setStatus = null,
        Action<double?>? setLength = null,
        Action<string?>? setLengthText = null,
        Action<double?>? setArea = null,
        Action<string>? setSelectionInfo = null,
        Action<bool>? setLengthInputEnabled = null,
        Action<double?>? resetLengthInput = null,
        Func<KeyEventArgs, bool>? processLengthKey = null,
        Action<bool>? setRectangleSizeInputEnabled = null,
        Action<double?, double?>? setRectangleSizePreview = null,
        Action<double?, double?>? resetRectangleSizeInput = null,
        Func<KeyEventArgs, bool>? processRectangleSizeKey = null,
        Action<bool, DualFieldLabelMode>? setDualFieldInputEnabled = null,
        Action<bool, string>? setLineInputModeEnabled = null,
        Func<(string First, string Second)>? getDualFieldInputText = null,
        Action<string, string>? setDualFieldInputText = null,
        Func<string>? getLineInputText = null,
        Action<string>? setLineInputText = null,
        Action? recordUndo = null)
    {
        Session = session;
        GetViewportSize = getViewportSize;
        ScreenToWorld = screenToWorld;
        GetMousePositionOnViewport = getMousePositionOnViewport;
        RequestRedraw = requestRedraw;
        CaptureMouse = captureMouse;
        ReleaseMouseCapture = releaseMouseCapture;
        SetStatus = setStatus ?? (_ => { });
        SetLength = setLength ?? (_ => { });
        SetLengthText = setLengthText ?? (_ => { });
        SetArea = setArea ?? (_ => { });
        SetSelectionInfo = setSelectionInfo ?? (_ => { });
        SetLengthInputEnabled = setLengthInputEnabled ?? (_ => { });
        ResetLengthInput = resetLengthInput ?? (_ => { });
        ProcessLengthKey = processLengthKey ?? (_ => false);
        SetRectangleSizeInputEnabled = setRectangleSizeInputEnabled ?? (_ => { });
        SetRectangleSizePreview = setRectangleSizePreview ?? ((_, _) => { });
        ResetRectangleSizeInput = resetRectangleSizeInput ?? ((_, _) => { });
        ProcessRectangleSizeKey = processRectangleSizeKey ?? (_ => false);
        SetDualFieldInputEnabled = setDualFieldInputEnabled ?? ((_, _) => { });
        SetLineInputModeEnabled = setLineInputModeEnabled ?? ((_, _) => { });
        GetDualFieldInputText = getDualFieldInputText ?? (() => (string.Empty, string.Empty));
        SetDualFieldInputText = setDualFieldInputText ?? ((_, _) => { });
        GetLineInputText = getLineInputText ?? (() => string.Empty);
        SetLineInputText = setLineInputText ?? (_ => { });
        RecordUndo = recordUndo ?? (() => { });
    }

    public CadSession Session { get; }

    public Func<Size> GetViewportSize { get; }

    public Func<Point, PointF> ScreenToWorld { get; }

    public Func<MouseEventArgs, Point> GetMousePositionOnViewport { get; }

    public Action RequestRedraw { get; }

    public Action CaptureMouse { get; }

    public Action ReleaseMouseCapture { get; }

    public Action<string> SetStatus { get; }

    public Action<double?> SetLength { get; }

    public Action<string?> SetLengthText { get; }

    public Action<double?> SetArea { get; }

    public Action<string> SetSelectionInfo { get; }

    public Action<bool> SetLengthInputEnabled { get; }

    public Action<double?> ResetLengthInput { get; }

    public Func<KeyEventArgs, bool> ProcessLengthKey { get; }

    public Action<bool> SetRectangleSizeInputEnabled { get; }

    public Action<double?, double?> SetRectangleSizePreview { get; }

    public Action<double?, double?> ResetRectangleSizeInput { get; }

    public Func<KeyEventArgs, bool> ProcessRectangleSizeKey { get; }

    public Action<bool, DualFieldLabelMode> SetDualFieldInputEnabled { get; }

    public Action<bool, string> SetLineInputModeEnabled { get; }

    public Func<(string First, string Second)> GetDualFieldInputText { get; }

    public Action<string, string> SetDualFieldInputText { get; }

    public Func<string> GetLineInputText { get; }

    public Action<string> SetLineInputText { get; }

    public Action RecordUndo { get; }

    public double SnapTolerance
        => MathUtils.SnapToleranceWorld(Session.Camera.Zoom);
}
