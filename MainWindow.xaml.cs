using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Services;
using LiteCad.Tools;
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
            ["Axis"] = new AxisTool(),
            ["Wall"] = new WallTool(),
            ["Rectangle"] = new RectangleTool(),
            ["Polyline"] = new PolylineTool(),
            ["Move"] = new MoveTool(),
            ["Copy"] = new CopyTool(),
            ["Delete"] = new DeleteTool(),
            ["PolygonEdit"] = new PolygonEditTool()
        };

        MainCanvas.Session = ViewModel.Session;
        MainProperties.BindSession(ViewModel.Session);

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
            () => ViewModel.Session.History.Record(ViewModel.Session.Document));

        MainCanvas.InitializeTools(toolContext);
        MainCanvas.MouseWorldPositionChanged += OnMouseWorldPositionChanged;
        MainStatusBar.LengthCommitted += OnLengthCommitted;
        MainStatusBar.RectangleSizeCommitted += OnRectangleSizeCommitted;

        MainToolBar.ToolRequested += OnToolRequested;
        MainMenuBar.ToolRequested += OnToolRequested;
        MainMenuBar.EditCommandRequested += OnEditCommandRequested;
        MainMenuBar.FileCommandRequested += OnFileCommandRequested;
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
        MainProperties.SetSelection("Nothing selected");
        MainStatusBar.SetLength(null);
        MainStatusBar.SetArea(null);
        MainStatusBar.SetStatus("New document");
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
            "Copy" => session.Edit.Copy(session),
            "Paste" => session.Edit.Paste(session),
            "Delete" => session.Edit.Delete(session),
            _ => false
        };

        if (!success)
        {
            MainStatusBar.SetStatus($"Cannot {command.ToLower()}");
            return;
        }

        UpdateUiAfterEdit(command, session);
        MainCanvas.RequestRedraw();
    }

    private void UpdateUiAfterEdit(string command, CadSession session)
    {
        switch (command)
        {
            case "Undo":
            case "Redo":
                session.Selection.Clear();
                MainProperties.SetSelection("Nothing selected");
                MainStatusBar.SetLength(null);
                MainStatusBar.SetArea(null);
                MainStatusBar.SetStatus(command);
                break;
            case "Copy":
                MainStatusBar.SetStatus("Copied to clipboard");
                break;
            case "Paste":
                MainProperties.SetSelection("Pasted");
                MainStatusBar.SetLength(null);
                MainStatusBar.SetArea(null);
                MainStatusBar.SetStatus("Pasted");
                break;
            case "Cut":
                session.Selection.Clear();
                MainProperties.SetSelection("Nothing selected");
                MainStatusBar.SetLength(null);
                MainStatusBar.SetArea(null);
                MainStatusBar.SetStatus("Cut to clipboard");
                break;
            case "Delete":
                session.Selection.Clear();
                MainProperties.SetSelection("Nothing selected");
                MainStatusBar.SetLength(null);
                MainStatusBar.SetArea(null);
                MainStatusBar.SetStatus("Deleted");
                break;
        }
    }

    private void ActivateTool(ITool tool)
    {
        ViewModel.Session.ToolService.ActivateTool(tool);
        MainProperties.SetActiveTool(tool.Name);
        MainStatusBar.SetStatus($"{tool.Name} tool active");
    }

    private void OnRectangleSizeCommitted(object? sender, (string Width, string Height) sizes)
    {
        ViewModel.Session.ToolService.ActiveTool?.TryApplyRectangleSize(sizes.Width, sizes.Height);
    }

    private void OnLengthCommitted(object? sender, string input)
    {
        var tool = ViewModel.Session.ToolService.ActiveTool;
        if (tool is null)
        {
            return;
        }

        if (tool.TryApplyLengthInput(input))
        {
            return;
        }

        if (TryParseSingleLength(input, out var length))
        {
            tool.TryApplyLength(length);
        }
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

    private void OnMouseWorldPositionChanged(object? sender, PointEventArgs e)
    {
        MainStatusBar.SetCoordinates(new PointF(e.X, e.Y));
    }
}

public sealed class MainWindowViewModel
{
    public CadSession Session { get; } = new();
}
