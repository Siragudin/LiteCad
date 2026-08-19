using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Resources;
using LiteCad.Services;
using LiteCad.Tools;
using LiteCad.UI;
using LiteCad.UI.Layout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LiteCad;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, ITool> _tools;

    public MainWindowViewModel ViewModel { get; }

    public MainWindow()
    {
        ViewModel = new MainWindowViewModel();
        DataContext = ViewModel;
        InitializeComponent();

        _tools = new Dictionary<string, ITool>
        {
            ["Selection"] = new SelectionTool(),
            ["Hand"] = new HandTool(),
            ["Line"] = new LineTool(),
            ["Arc"] = new ArcTool(),
            ["Axis"] = new AxisTool(),
            ["Wall"] = new WallTool(),
            ["Rectangle"] = new RectangleTool(),
            ["Circle"] = new CircleTool(),
            ["Sector"] = new SectorTool(),
            ["Move"] = new MoveTool(),
            ["Rotate"] = new RotateTool(),
            ["Mirror"] = new MirrorTool(),
            ["Stretch"] = new StretchTool(),
            ["Eraser"] = new EraserTool(),
            ["Copy"] = new CopyTool(),
            ["PolygonEdit"] = new PolygonEditTool()
        };

        MainCanvas.Session = ViewModel.Session;
        MainProperties.BindSession(ViewModel.Session, OnMoveOrthoChanged, OnMirrorOrthoChanged);

        var toolContext = new ToolContext(
            ViewModel.Session,
            () => MainCanvas.GetViewportSize(),
            screen => ViewModel.Session.Camera.ScreenToWorld(screen, MainCanvas.GetViewportSize()),
            e => MainCanvas.GetMousePositionOnViewport(e),
            () => MainCanvas.RequestRedraw(),
            () => MainCanvas.CaptureMouse(),
            () => MainCanvas.ReleaseMouseCapture(),
            status => MainStatusBar.SetStatus(status),
            length => MainStatusBar.SetLength(length),
            text => MainStatusBar.SetLengthText(text),
            area => MainStatusBar.SetArea(area),
            selection => MainProperties.SetSelection(selection),
            enabled => MainStatusBar.SetLengthInputEnabled(enabled),
            length => MainStatusBar.ResetLengthEditing(length),
            e => MainStatusBar.ProcessLengthKey(e),
            enabled => MainStatusBar.SetRectangleSizeInputEnabled(enabled),
            (width, height) => MainStatusBar.SetRectangleSizePreview(width, height),
            (width, height) => MainStatusBar.ResetRectangleSizeInput(width, height),
            e => MainStatusBar.ProcessRectangleSizeKey(e),
            (enabled, mode) => MainStatusBar.SetRectangleSizeInputEnabled(enabled, mode),
            (enabled, label) => MainStatusBar.SetLengthInputEnabled(enabled, label),
            () => MainStatusBar.GetDualFieldInputText(),
            (first, second) => MainStatusBar.SetDualFieldInputText(first, second),
            () => MainStatusBar.LineInputText,
            text => MainStatusBar.SetLineInputText(text),
            () => ViewModel.Session.History.Record(ViewModel.Session.Document));

        MainCanvas.InitializeTools(toolContext);
        MainCanvas.MouseWorldPositionChanged += OnMouseWorldPositionChanged;
        MainStatusBar.LengthCommitted += OnLengthCommitted;
        MainStatusBar.RectangleSizeCommitted += OnRectangleSizeCommitted;
        MainStatusBar.TryCommitLengthInput = TryCommitLengthForActiveTool;
        MainStatusBar.TryCommitRectangleSizeInput = TryCommitRectangleSizeForActiveTool;

        MainToolBar.ToolRequested += OnToolRequested;
        MainMenuBar.ToolRequested += OnToolRequested;
        MainMenuBar.EditCommandRequested += OnEditCommandRequested;
        MainMenuBar.FileCommandRequested += OnFileCommandRequested;
        LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        ActivateTool(_tools["Selection"]);
    }

    private void OnToolRequested(object? sender, string toolTag)
    {
        if (_tools.TryGetValue(toolTag, out var tool))
        {
            ActivateTool(tool);
        }
    }

    private void OnEditCommandRequested(object? sender, string command)
    {
        ExecuteEditCommand(command);
    }

    private void OnFileCommandRequested(object? sender, string command)
    {
        if (command != "New")
        {
            return;
        }

        var session = ViewModel.Session;
        session.NewDocument();
        session.Selection.Clear();
        MainProperties.SetSelection(Strings.Selection_NothingSelected);
        MainStatusBar.SetLength(null);
        MainStatusBar.SetArea(null);
        MainStatusBar.SetStatus(Strings.Status_NewDocument);
        MainCanvas.RequestRedraw();
    }

    private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (MainStatusBar.IsRectangleInputActive
            && StatusBar.IsAltToggleKey(e)
            && e.OriginalSource is not System.Windows.Controls.TextBox { Name: "WidthInput" or "HeightInput" })
        {
            MainStatusBar.ToggleRectangleSizeField();
            e.Handled = true;
            return;
        }

        if (e.OriginalSource is TextBox)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Z:
                    ExecuteEditCommand("Undo");
                    e.Handled = true;
                    return;
                case Key.Y:
                    ExecuteEditCommand("Redo");
                    e.Handled = true;
                    return;
                case Key.C:
                    ExecuteEditCommand("Copy");
                    e.Handled = true;
                    return;
                case Key.X:
                    ExecuteEditCommand("Cut");
                    e.Handled = true;
                    return;
                case Key.V:
                    ExecuteEditCommand("Paste");
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.Delete)
        {
            ExecuteEditCommand("Delete");
            e.Handled = true;
        }
    }

    private void ExecuteEditCommand(string command)
    {
        var session = ViewModel.Session;
        var success = command switch
        {
            "Undo" => session.History.Undo(session.Document, MathUtils.SnapToleranceWorld(session.Camera.Zoom)),
            "Redo" => session.History.Redo(session.Document, MathUtils.SnapToleranceWorld(session.Camera.Zoom)),
            "Cut" => session.Edit.Cut(session),
            "Copy" => TryStartInteractiveCopy(session),
            "Paste" => session.Edit.Paste(session),
            "Delete" => session.Edit.Delete(session),
            _ => false
        };

        if (!success)
        {
            MainStatusBar.SetStatus(Strings.Format(Strings.Error_CannotCommand, command.ToLowerInvariant()));
            return;
        }

        if (command == "Copy")
        {
            MainCanvas.RequestRedraw();
            return;
        }

        UpdateUiAfterEdit(command, session);
        MainCanvas.RequestRedraw();
    }

    private bool TryStartInteractiveCopy(CadSession session)
    {
        if (!CopyOperations.CanCopy(session.Selection))
        {
            return false;
        }

        if (_tools.TryGetValue("Copy", out var tool))
        {
            ActivateTool(tool);
            return true;
        }

        return false;
    }

    private void UpdateUiAfterEdit(string command, CadSession session)
    {
        switch (command)
        {
            case "Undo":
            case "Redo":
                session.Selection.Clear();
                MainProperties.SetSelection(Strings.Selection_NothingSelected);
                MainStatusBar.SetLength(null);
                MainStatusBar.SetArea(null);
                MainStatusBar.SetStatus(command == "Undo" ? Strings.Status_Undo : Strings.Status_Redo);
                break;
            case "Copy":
                MainStatusBar.SetStatus(Strings.Status_CopiedToClipboard);
                break;
            case "Paste":
                MainProperties.SetSelection(Strings.Selection_Pasted);
                MainStatusBar.SetLength(null);
                MainStatusBar.SetArea(null);
                MainStatusBar.SetStatus(Strings.Status_Pasted);
                break;
            case "Cut":
                session.Selection.Clear();
                MainProperties.SetSelection(Strings.Selection_NothingSelected);
                MainStatusBar.SetLength(null);
                MainStatusBar.SetArea(null);
                MainStatusBar.SetStatus(Strings.Status_CutToClipboard);
                break;
            case "Delete":
                session.Selection.Clear();
                MainProperties.SetSelection(Strings.Selection_NothingSelected);
                MainStatusBar.SetLength(null);
                MainStatusBar.SetArea(null);
                MainStatusBar.SetStatus(Strings.Status_Deleted);
                break;
        }
    }

    private void ActivateTool(ITool tool)
    {
        ViewModel.Session.ToolService.ActivateTool(tool);
        MainProperties.SetActiveTool(tool.Id);
        MainStatusBar.SetStatus(Strings.Format(Strings.Status_ToolActive, tool.Name));
    }

    private void OnRectangleSizeCommitted(object? sender, (string Width, string Height) sizes)
    {
        ViewModel.Session.ToolService.ActiveTool?.TryApplyRectangleSize(sizes.Width, sizes.Height);
    }

    private bool TryCommitRectangleSizeForActiveTool((string Width, string Height) sizes)
        => ViewModel.Session.ToolService.ActiveTool?.TryApplyRectangleSize(sizes.Width, sizes.Height) == true;

    private void OnLengthCommitted(object? sender, string input)
    {
        TryCommitLengthForActiveTool(input);
    }

    private bool TryCommitLengthForActiveTool(string input)
    {
        var tool = ViewModel.Session.ToolService.ActiveTool;
        if (tool is null)
        {
            return false;
        }

        if (tool.TryApplyLengthInput(input))
        {
            return true;
        }

        return TryParseSingleLength(input, out var length) && tool.TryApplyLength(length);
    }

    private static bool TryParseSingleLength(string text, out double length)
    {
        length = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim().Replace(',', '.');
        return double.TryParse(normalized, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out length)
            && length > 0;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
        => RefreshLocalizedUi();

    private void RefreshLocalizedUi()
    {
        MainStatusBar.RefreshLocalizedLabels();

        var activeTool = ViewModel.Session.ToolService.ActiveTool;
        if (activeTool is not null)
        {
            MainProperties.SetActiveTool(activeTool.Id);
            MainStatusBar.SetStatus(Strings.Format(Strings.Status_ToolActive, activeTool.Name));
        }

        MainProperties.SetSelection(SelectionUiFormatter.FormatSelectionInfo(ViewModel.Session));
    }

    private void OnMouseWorldPositionChanged(object? sender, PointEventArgs e)
    {
        MainStatusBar.SetCoordinates(new PointF(e.X, e.Y));
    }

    private void OnMoveOrthoChanged()
    {
        if (ViewModel.Session.ToolService.ActiveTool is MoveTool moveTool)
        {
            moveTool.NotifyOrthoChanged();
        }

        MainCanvas.RequestRedraw();
    }

    private void OnMirrorOrthoChanged()
    {
        if (ViewModel.Session.ToolService.ActiveTool is MirrorTool mirrorTool)
        {
            mirrorTool.NotifyOrthoChanged();
        }

        MainCanvas.RequestRedraw();
    }
}

public sealed class MainWindowViewModel
{
    public CadSession Session { get; } = new();
}
