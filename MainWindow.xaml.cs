using LiteCad.Core.Geometry;
using LiteCad.Infrastructure;
using LiteCad.Rendering.Pdf;
using LiteCad.Resources;
using LiteCad.Services;
using LiteCad.Tools;
using LiteCad.UI;
using LiteCad.UI.Layout;
using LiteCad.UI.Pdf;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LiteCad;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, ITool> _tools;
    private readonly ProjectStorage _projectStorage = new();

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
            ["Offset"] = new OffsetTool(),
            ["Stretch"] = new StretchTool(),
            ["Extend"] = new ExtendTool(),
            ["Dimension"] = new DimensionTool(),
            ["Leader"] = new LeaderTool(),
            ["Eraser"] = new EraserTool(),
            ["Copy"] = new CopyTool(),
            ["Fill"] = new FillTool(),
            ["PolygonEdit"] = new PolygonEditTool()
        };

        MainCanvas.Session = ViewModel.Session;
        MainProperties.BindSession(
            ViewModel.Session,
            OnMoveOrthoChanged,
            OnMirrorOrthoChanged,
            () => MainCanvas.RequestRedraw());

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
            selection =>
            {
                MainProperties.SetSelection(selection);
                MainProperties.SyncDimensionSelection(ViewModel.Session);
                MainProperties.SyncAxisSelection(ViewModel.Session);
                MainProperties.SyncFaceSelection(ViewModel.Session);
            },
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
            () =>
            {
                ViewModel.Session.History.Record(ViewModel.Session.Document);
                ViewModel.Session.ProjectFile.MarkDirty();
            },
            () => ActivateTool(_tools["Selection"]));

        MainCanvas.InitializeTools(toolContext);
        MainCanvas.MouseWorldPositionChanged += OnMouseWorldPositionChanged;
        MainStatusBar.LengthCommitted += OnLengthCommitted;
        MainStatusBar.RectangleSizeCommitted += OnRectangleSizeCommitted;
        MainStatusBar.TryCommitLengthInput = TryCommitLengthForActiveTool;
        MainStatusBar.TryCommitRectangleSizeInput = TryCommitRectangleSizeForActiveTool;
        MainStatusBar.BindSession(ViewModel.Session, OnDisplayUnitChanged);

        MainToolBar.ToolRequested += OnToolRequested;
        MainMenuBar.ToolRequested += OnToolRequested;
        MainMenuBar.EditCommandRequested += OnEditCommandRequested;
        MainMenuBar.FileCommandRequested += OnFileCommandRequested;
        LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        ThemeManager.Instance.ThemeChanged += OnThemeChanged;
        _projectStorage.EnsureProjectsDirectoryExists();
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
        switch (command)
        {
            case "New":
                CreateNewProject();
                break;
            case "Open":
                OpenProject();
                break;
            case "Save":
                SaveProject(saveAs: false);
                break;
            case "SaveAs":
                SaveProject(saveAs: true);
                break;
            case "DeleteProject":
                DeleteCurrentProject();
                break;
        }
    }

    private void CreateNewProject()
    {
        var session = ViewModel.Session;
        if (session.ProjectFile.IsDirty
            && MessageBox.Show(
                Strings.Dialog_UnsavedChanges_Message,
                Strings.Dialog_UnsavedChanges_Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        ResetToNewDocument(session, Strings.Status_NewDocument);
    }

    private void OpenProject()
    {
        var session = ViewModel.Session;
        if (session.ProjectFile.IsDirty
            && MessageBox.Show(
                Strings.Dialog_UnsavedChanges_Message,
                Strings.Dialog_UnsavedChanges_Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _projectStorage.EnsureProjectsDirectoryExists();
        var dialog = new OpenFileDialog
        {
            Title = Strings.Dialog_OpenProject_Title,
            Filter = Strings.Dialog_ProjectFilter,
            InitialDirectory = _projectStorage.ProjectsDirectory,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        LoadProjectFromPath(session, dialog.FileName);
    }

    private void SaveProject(bool saveAs)
    {
        var session = ViewModel.Session;
        if (!saveAs && session.ProjectFile.HasSavedPath)
        {
            _projectStorage.SaveProject(
                session.Document,
                session.DisplayUnitSettings.LinearUnit,
                session.ProjectFile.CurrentFilePath!,
                session.Renderer);
            session.ProjectFile.MarkSaved(session.ProjectFile.CurrentFilePath!);
            MainStatusBar.SetStatus(Strings.Status_ProjectSaved);
            return;
        }

        _projectStorage.EnsureProjectsDirectoryExists();
        var dialog = new SaveFileDialog
        {
            Title = saveAs ? Strings.Dialog_SaveProjectAs_Title : Strings.Dialog_SaveProject_Title,
            Filter = saveAs ? Strings.Dialog_SaveAsFilter : Strings.Dialog_ProjectFilter,
            InitialDirectory = _projectStorage.ProjectsDirectory,
            AddExtension = true,
            DefaultExt = ProjectFileNameHelper.SitExtension.TrimStart('.'),
            FileName = session.ProjectFile.HasSavedPath
                ? ProjectFileNameHelper.GetProjectDisplayName(session.ProjectFile.CurrentFilePath)
                : Strings.Label_DefaultProjectName
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (saveAs && PdfFileNameHelper.IsPdfPath(dialog.FileName))
        {
            ShowPdfPreviewAndSave(session, dialog.FileName);
            return;
        }

        var targetPath = ProjectFileNameHelper.NormalizeSitFilePath(dialog.FileName);
        if (File.Exists(targetPath)
            && MessageBox.Show(
                Strings.Dialog_OverwriteProject_Message,
                Strings.Dialog_OverwriteProject_Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _projectStorage.SaveProject(
            session.Document,
            session.DisplayUnitSettings.LinearUnit,
            targetPath,
            session.Renderer);
        session.ProjectFile.MarkSaved(targetPath);
        MainStatusBar.SetStatus(Strings.Status_ProjectSaved);
    }

    private void ShowPdfPreviewAndSave(CadSession session, string fileName)
    {
        if (PdfPreviewWindow.TryShowAndSave(
                session.Document,
                session.DisplayUnitSettings.LinearUnit,
                session.Renderer,
                fileName,
                this))
        {
            MainStatusBar.SetStatus(Strings.Status_PdfExported);
        }
    }

    private void ExportPdf(CadSession session, string fileName)
    {
        var targetPath = PdfFileNameHelper.NormalizePdfFilePath(fileName);
        if (File.Exists(targetPath)
            && MessageBox.Show(
                Strings.Dialog_OverwritePdf_Message,
                Strings.Dialog_OverwritePdf_Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            PdfExporter.Export(
                session.Document,
                session.DisplayUnitSettings.LinearUnit,
                session.Renderer,
                targetPath);
            MainStatusBar.SetStatus(Strings.Status_PdfExported);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format(CultureInfo.CurrentCulture, Strings.Dialog_PdfExportFailed_Message, ex.Message),
                Strings.Dialog_PdfExportFailed_Title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void DeleteCurrentProject()
    {
        var session = ViewModel.Session;
        if (!session.ProjectFile.HasSavedPath)
        {
            return;
        }

        if (MessageBox.Show(
                Strings.Dialog_DeleteProject_Message,
                Strings.Dialog_DeleteProject_Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var deletedPath = session.ProjectFile.CurrentFilePath!;
        _projectStorage.DeleteProject(deletedPath);
        ResetToNewDocument(session, Strings.Status_ProjectDeleted);
    }

    private void LoadProjectFromPath(CadSession session, string filePath)
    {
        try
        {
            var dto = _projectStorage.LoadProject(filePath, out _);
            session.LoadProject(dto, filePath);
            FitCameraAfterProjectOpen();
            MainProperties.SetSelection(Strings.Selection_NothingSelected);
            MainProperties.SyncDimensionSelection(session);
            MainProperties.SyncAxisSelection(session);
            MainProperties.SyncFaceSelection(session);
            MainStatusBar.SetLength(null);
            MainStatusBar.SetArea(null);
            MainStatusBar.SetStatus(Strings.Status_ProjectOpened);
            ActivateTool(_tools["Selection"]);
            MainCanvas.RequestRedraw();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                Strings.Dialog_OpenProject_Title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void FitCameraAfterProjectOpen()
    {
        var session = ViewModel.Session;
        var viewport = MainCanvas.GetViewportSize();
        if (viewport.Width >= 1 && viewport.Height >= 1)
        {
            session.FitCameraToDocument(viewport);
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            session.FitCameraToDocument(MainCanvas.GetViewportSize());
            MainCanvas.RequestRedraw();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ResetToNewDocument(CadSession session, string statusMessage)
    {
        session.NewDocument();
        session.Selection.Clear();
        MainProperties.SetSelection(Strings.Selection_NothingSelected);
        MainProperties.SyncDimensionSelection(session);
            MainProperties.SyncAxisSelection(session);
        MainProperties.SyncFaceSelection(session);
        MainStatusBar.SetLength(null);
        MainStatusBar.SetArea(null);
        MainStatusBar.SetStatus(statusMessage);
        ActivateTool(_tools["Selection"]);
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
                MainProperties.SyncDimensionSelection(session);
            MainProperties.SyncAxisSelection(session);
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
                MainProperties.SyncDimensionSelection(session);
            MainProperties.SyncAxisSelection(session);
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
        MainToolBar.SetActiveTool(tool.Id.ToString());
        MainStatusBar.SetStatus(Strings.Format(Strings.Status_ToolActive, tool.Name));
    }

    private void OnRectangleSizeCommitted(object? sender, (string Width, string Height) sizes)
    {
        TryCommitRectangleSizeForActiveTool(sizes);
    }

    private bool TryCommitLengthForActiveTool(string input)
    {
        var tool = ViewModel.Session.ToolService.ActiveTool;
        if (tool is null)
        {
            return false;
        }

        return LinearInputCommit.TryCommitLength(
            tool,
            input,
            ViewModel.Session.DisplayUnitSettings.LinearUnit,
            MainStatusBar.LineInputLabelMode);
    }

    private bool TryCommitRectangleSizeForActiveTool((string Width, string Height) sizes)
    {
        var tool = ViewModel.Session.ToolService.ActiveTool;
        if (tool is null)
        {
            return false;
        }

        return LinearInputCommit.TryCommitRectangleSize(
            tool,
            sizes.Width,
            sizes.Height,
            ViewModel.Session.DisplayUnitSettings.LinearUnit,
            MainStatusBar.DualFieldLabels);
    }

    private void OnLengthCommitted(object? sender, string input)
    {
        TryCommitLengthForActiveTool(input);
    }

    private void OnDisplayUnitChanged()
    {
        MainCanvas.RequestRedraw();
        MainProperties.SetSelection(SelectionUiFormatter.FormatSelectionInfo(ViewModel.Session));
        MainProperties.SyncDimensionSelection(ViewModel.Session);
                MainProperties.SyncAxisSelection(ViewModel.Session);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
        => RefreshLocalizedUi();

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        MainCanvas.RequestRedraw();
        MainStatusBar.RefreshThemeStyles();
    }

    private void RefreshLocalizedUi()
    {
        MainStatusBar.RefreshLocalizedLabels();
        MainToolBar.RefreshLocalizedLabels();

        var activeTool = ViewModel.Session.ToolService.ActiveTool;
        if (activeTool is not null)
        {
            MainProperties.SetActiveTool(activeTool.Id);
            MainToolBar.SetActiveTool(activeTool.Id.ToString());
            MainStatusBar.SetStatus(Strings.Format(Strings.Status_ToolActive, activeTool.Name));
        }

        MainProperties.SetSelection(SelectionUiFormatter.FormatSelectionInfo(ViewModel.Session));
        MainProperties.SyncDimensionSelection(ViewModel.Session);
                MainProperties.SyncAxisSelection(ViewModel.Session);
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
