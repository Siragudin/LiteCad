using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Resources;
using LiteCad.Services;
using LiteCad.Tools;
using LiteCad.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LiteCad.UI.Layout;

public partial class PropertiesPanel : UserControl
{
    private static readonly object InitializeComponentLock = new();
    private CadSession? _session;
    private Action? _onMoveOrthoChanged;
    private Action? _onMirrorOrthoChanged;
    private Action? _requestRedraw;
    private bool _suppressMoveOrthoEvents;
    private bool _suppressMirrorOrthoEvents;
    private bool _suppressDimensionOffsetEvents;
    private bool _suppressDimensionTextSizeEvents;
    private bool _suppressDimensionToolTextSizeEvents;
    private bool _suppressDimensionExtensionStyleEvents;
    private bool _suppressDimensionToolExtensionStyleEvents;
    private bool _suppressDimensionOrthoEvents;
    private bool _suppressFaceFillEvents;
    private bool _suppressFillToolEvents;
    private bool _suppressAxisOrthoEvents;
    private bool _suppressOffsetAxisEvents;

    public PropertiesPanel()
    {
        lock (InitializeComponentLock)
        {
            InitializeComponent();
        }
    }

    public bool IsMoveToolPanelVisible => MoveToolPanel.Visibility == Visibility.Visible;

    public bool IsMirrorToolPanelVisible => MirrorToolPanel.Visibility == Visibility.Visible;

    public bool IsDimensionToolPanelVisible => DimensionToolPanel.Visibility == Visibility.Visible;

    public bool IsDimensionSelectionPanelVisible => DimensionSelectionPanel.Visibility == Visibility.Visible;

    public bool IsMoveOrthoToggleVisible => IsMoveToolPanelVisible;

    public bool IsMirrorOrthoToggleVisible => IsMirrorToolPanelVisible;

    public bool IsOffsetToolPanelVisible => OffsetToolPanel.Visibility == Visibility.Visible;

    public bool MoveOrthoIsChecked => MoveOrthoCheckBox.IsChecked == true;

    public bool MirrorOrthoIsChecked => MirrorOrthoCheckBox.IsChecked == true;

    public void BindSession(
        CadSession session,
        Action? onMoveOrthoChanged = null,
        Action? onMirrorOrthoChanged = null,
        Action? requestRedraw = null)
    {
        _session = session;
        _onMoveOrthoChanged = onMoveOrthoChanged;
        _onMirrorOrthoChanged = onMirrorOrthoChanged;
        _requestRedraw = requestRedraw;
        _session.DisplayUnitSettings.Changed += OnDisplayUnitSettingsChanged;
    }

    private void OnDisplayUnitSettingsChanged()
    {
        if (_session is null)
        {
            return;
        }

        SyncDimensionSelection(_session);
        if (_session.ToolService.ActiveTool?.Id == ToolId.Dimension)
        {
            SyncDimensionToolTextSizeFromSession();
        }
    }

    public void SetActiveTool(ToolId toolId)
    {
        ActiveToolText.Text = ToolDisplayNames.Get(toolId);
        var isLineTool = toolId == ToolId.Line;
        var isAxisTool = toolId == ToolId.Axis;
        var isMoveTool = toolId == ToolId.Move;
        var isMirrorTool = toolId == ToolId.Mirror;
        var isOffsetTool = toolId == ToolId.Offset;
        var isDimensionTool = toolId == ToolId.Dimension;
        var isFillTool = toolId == ToolId.Fill;
        LineToolPanel.Visibility = isLineTool ? Visibility.Visible : Visibility.Collapsed;
        AxisToolPanel.Visibility = isAxisTool ? Visibility.Visible : Visibility.Collapsed;
        MoveToolPanel.Visibility = isMoveTool ? Visibility.Visible : Visibility.Collapsed;
        MirrorToolPanel.Visibility = isMirrorTool ? Visibility.Visible : Visibility.Collapsed;
        OffsetToolPanel.Visibility = isOffsetTool ? Visibility.Visible : Visibility.Collapsed;
        DimensionToolPanel.Visibility = isDimensionTool ? Visibility.Visible : Visibility.Collapsed;
        FillToolPanel.Visibility = isFillTool ? Visibility.Visible : Visibility.Collapsed;

        if (_session is not null)
        {
            SyncDimensionSelection(_session);
            SyncAxisSelection(_session);
            SyncFaceSelection(_session);
        }

        UpdateNoParametersVisibility(isLineTool, isAxisTool, isMoveTool, isMirrorTool, isOffsetTool, isDimensionTool, isFillTool);

        if (isAxisTool)
        {
            SyncAxisOrthoCheckBoxFromSession();
        }

        if (isMoveTool)
        {
            SyncMoveOrthoCheckBoxFromSession();
        }

        if (isMirrorTool)
        {
            SyncMirrorOrthoCheckBoxFromSession();
        }

        if (isOffsetTool)
        {
            SyncOffsetAxisCheckBoxFromSession();
        }

        if (isDimensionTool)
        {
            SyncDimensionToolExtensionStyleFromSession();
            SyncDimensionToolTextSizeFromSession();
            SyncDimensionOrthoCheckBoxFromSession();
        }

        if (isFillTool)
        {
            SyncFillToolPanelFromSession();
        }
    }

    public void SetSelection(string selectionInfo)
    {
        SelectionText.Text = selectionInfo;
        if (_session is not null)
        {
            SyncDimensionSelection(_session);
            SyncAxisSelection(_session);
            SyncFaceSelection(_session);
        }
    }

    public void SyncAxisSelection(CadSession session)
    {
        var isAxisToolActive = session.ToolService.ActiveTool?.Id == ToolId.Axis;
        var showAxisSelectionPanel = !isAxisToolActive
            && session.Selection.SelectedAxisIds.Count == 1
            && session.Selection.SelectedEdgeIds.Count == 0
            && session.Selection.SelectedPolygonIds.Count == 0
            && session.Selection.SelectedVertexIds.Count == 0
            && session.Selection.SelectedDimensionIds.Count == 0;

        AxisSelectionPanel.Visibility = showAxisSelectionPanel
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!showAxisSelectionPanel)
        {
            UpdateNoParametersVisibility(
                session.ToolService.ActiveTool?.Id == ToolId.Line,
                session.ToolService.ActiveTool?.Id == ToolId.Axis,
                session.ToolService.ActiveTool?.Id == ToolId.Move,
                session.ToolService.ActiveTool?.Id == ToolId.Mirror,
                session.ToolService.ActiveTool?.Id == ToolId.Dimension,
                session.ToolService.ActiveTool?.Id == ToolId.Fill);
            return;
        }

        var axis = session.Document.Axes.First(item => session.Selection.SelectedAxisIds.Contains(item.Id));
        var unit = session.DisplayUnitSettings.LinearUnit;
        AxisStartText.Text = $"{UnitDisplayFormatter.FormatCoordinate(axis.Start.X, unit)}, {UnitDisplayFormatter.FormatCoordinate(axis.Start.Y, unit)}";
        AxisEndText.Text = $"{UnitDisplayFormatter.FormatCoordinate(axis.End.X, unit)}, {UnitDisplayFormatter.FormatCoordinate(axis.End.Y, unit)}";
        AxisLengthText.Text = UnitDisplayFormatter.FormatLinear(MathUtils.Distance(axis.Start, axis.End), unit);

        UpdateNoParametersVisibility(false, false, false, false, false, false);
    }

    public void SyncFaceSelection(CadSession session)
    {
        var isFillToolActive = session.ToolService.ActiveTool?.Id == ToolId.Fill;
        var showFaceSelectionPanel = !isFillToolActive
            && session.Selection.SelectedPolygonIds.Count == 1
            && session.Selection.SelectedEdgeIds.Count == 0
            && session.Selection.SelectedVertexIds.Count == 0
            && session.Selection.SelectedDimensionIds.Count == 0
            && session.Selection.SelectedAxisIds.Count == 0;

        Polygon? selectedFace = null;
        if (showFaceSelectionPanel)
        {
            selectedFace = session.Document.Polygons.FirstOrDefault(polygon =>
                session.Selection.SelectedPolygonIds.Contains(polygon.Id)
                && polygon.Type == PolygonType.Face);
            showFaceSelectionPanel = selectedFace is not null;
        }

        FaceSelectionPanel.Visibility = showFaceSelectionPanel
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (showFaceSelectionPanel && selectedFace is not null)
        {
            var fill = FaceFillService.GetFill(session.Document, selectedFace);
            _suppressFaceFillEvents = true;
            SelectFaceFillColor(fill.FillColor);
            SelectFaceFillPattern(fill.FillPattern);
            _suppressFaceFillEvents = false;
        }

        var activeTool = session.ToolService.ActiveTool?.Id;
        UpdateNoParametersVisibility(
            activeTool == ToolId.Line,
            activeTool == ToolId.Axis,
            activeTool == ToolId.Move,
            activeTool == ToolId.Mirror,
            activeTool == ToolId.Dimension,
            activeTool == ToolId.Fill);
    }

    public void SyncFillToolPanelFromSession()
    {
        if (_session is null)
        {
            return;
        }

        _suppressFillToolEvents = true;
        SelectFillToolColor(_session.FillToolOptions.FillColor);
        SelectFillToolPattern(_session.FillToolOptions.FillPattern);
        _suppressFillToolEvents = false;
    }

    public void SyncDimensionSelection(CadSession session)
    {
        var isDimensionToolActive = session.ToolService.ActiveTool?.Id == ToolId.Dimension;
        var showDimensionSelectionPanel = !isDimensionToolActive
            && session.Selection.SelectedDimensionIds.Count == 1
            && session.Selection.SelectedEdgeIds.Count == 0
            && session.Selection.SelectedPolygonIds.Count == 0
            && session.Selection.SelectedVertexIds.Count == 0
            && session.Selection.SelectedAxisIds.Count == 0;

        DimensionSelectionPanel.Visibility = showDimensionSelectionPanel
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (showDimensionSelectionPanel)
        {
            var dimension = session.Document.Dimensions.First(item =>
                session.Selection.SelectedDimensionIds.Contains(item.Id));
            _suppressDimensionOffsetEvents = true;
            DimensionOffsetTextBox.Text = UnitDisplayFormatter.FormatLinear(
                dimension.Offset,
                session.DisplayUnitSettings.LinearUnit);
            _suppressDimensionOffsetEvents = false;

            _suppressDimensionTextSizeEvents = true;
            DimensionTextSizeTextBox.Text = FormatDimensionTextSize(
                dimension.TextSize,
                session.DisplayUnitSettings.LinearUnit);
            _suppressDimensionTextSizeEvents = false;

            _suppressDimensionExtensionStyleEvents = true;
            SelectDimensionExtensionStyle(DimensionExtensionStyleCombo, dimension.ExtensionStyle);
            _suppressDimensionExtensionStyleEvents = false;
        }

        var activeTool = session.ToolService.ActiveTool?.Id;
        UpdateNoParametersVisibility(
            activeTool == ToolId.Line,
            activeTool == ToolId.Axis,
            activeTool == ToolId.Move,
            activeTool == ToolId.Mirror,
            activeTool == ToolId.Dimension,
            activeTool == ToolId.Fill);
    }

    public void SetMoveOrthoChecked(bool enabled)
    {
        _suppressMoveOrthoEvents = true;
        MoveOrthoCheckBox.IsChecked = enabled;
        _suppressMoveOrthoEvents = false;
        ApplyMoveOrthoEnabled(enabled);
    }

    public void SetMirrorOrthoChecked(bool enabled)
    {
        _suppressMirrorOrthoEvents = true;
        MirrorOrthoCheckBox.IsChecked = enabled;
        _suppressMirrorOrthoEvents = false;
        ApplyMirrorOrthoEnabled(enabled);
    }

    public void SetDimensionToolExtensionStyle(DimensionExtensionStyle extensionStyle)
    {
        if (_session is not null)
        {
            _session.DimensionToolOptions.ExtensionStyle = extensionStyle;
        }

        _suppressDimensionToolExtensionStyleEvents = true;
        SelectDimensionExtensionStyle(DimensionToolExtensionStyleCombo, extensionStyle);
        _suppressDimensionToolExtensionStyleEvents = false;
    }

    public void SetDimensionToolTextSize(double textSize)
    {
        if (_session is not null)
        {
            _session.DimensionToolOptions.TextSize = Dimension.NormalizeTextSize(textSize);
        }

        SyncDimensionToolTextSizeFromSession();
    }

    public string DimensionTextSizeDisplayText => DimensionTextSizeTextBox.Text;

    public void SetSelectedDimensionTextSize(string text)
    {
        DimensionTextSizeTextBox.Text = text;
    }

    public void CommitSelectedDimensionTextSizeForTests()
        => CommitDimensionTextSize();

    public void SetDimensionOrthoChecked(bool enabled)
    {
        _suppressDimensionOrthoEvents = true;
        DimensionOrthoCheckBox.IsChecked = enabled;
        _suppressDimensionOrthoEvents = false;
        ApplyDimensionOrthoEnabled(enabled);
    }

    public bool DimensionOrthoIsChecked => DimensionOrthoCheckBox.IsChecked == true;

    private void LineColorCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_session is null || LineColorCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        _session.LineToolOptions.Color = item.Tag switch
        {
            "Gray" => Color.FromRgb(0x66, 0x66, 0x66),
            "Blue" => Colors.Blue,
            "Red" => Colors.Red,
            "Green" => Colors.Green,
            _ => Color.FromRgb(0x22, 0x22, 0x22)
        };
    }

    private void LineThicknessCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_session is null || LineThicknessCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        if (double.TryParse(item.Tag?.ToString(), out var thickness))
        {
            _session.LineToolOptions.Thickness = thickness;
        }
    }

    private void LineTypeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_session is null || LineTypeCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        _session.LineToolOptions.LineType = item.Tag?.ToString() switch
        {
            "Dashed" => EdgeLineType.Dashed,
            "Dotted" => EdgeLineType.Dotted,
            _ => EdgeLineType.Solid
        };
    }

    private void OrthoCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        _session.LineToolOptions.OrthoEnabled = OrthoCheckBox.IsChecked == true;
    }

    private void AxisOrthoCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_session is null || _suppressAxisOrthoEvents)
        {
            return;
        }

        _session.AxisToolOptions.OrthoEnabled = AxisOrthoCheckBox.IsChecked == true;
    }

    private void SyncAxisOrthoCheckBoxFromSession()
    {
        if (_session is null)
        {
            return;
        }

        _suppressAxisOrthoEvents = true;
        AxisOrthoCheckBox.IsChecked = _session.AxisToolOptions.OrthoEnabled;
        _suppressAxisOrthoEvents = false;
    }

    private void MoveOrthoCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_session is null || _suppressMoveOrthoEvents)
        {
            return;
        }

        ApplyMoveOrthoEnabled(MoveOrthoCheckBox.IsChecked == true);
    }

    private void OffsetAxisCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_session is null || _suppressOffsetAxisEvents)
        {
            return;
        }

        _session.OffsetToolOptions.IsAxisOffset = OffsetAxisCheckBox.IsChecked == true;
        if (_session.ToolService.ActiveTool is OffsetTool offsetTool)
        {
            offsetTool.CancelPendingOperation();
        }

        _requestRedraw?.Invoke();
    }

    private void SyncOffsetAxisCheckBoxFromSession()
    {
        if (_session is null)
        {
            return;
        }

        _suppressOffsetAxisEvents = true;
        OffsetAxisCheckBox.IsChecked = _session.OffsetToolOptions.IsAxisOffset;
        _suppressOffsetAxisEvents = false;
    }

    private void MirrorOrthoCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_session is null || _suppressMirrorOrthoEvents)
        {
            return;
        }

        ApplyMirrorOrthoEnabled(MirrorOrthoCheckBox.IsChecked == true);
    }

    private void ApplyMoveOrthoEnabled(bool enabled)
    {
        if (_session is null)
        {
            return;
        }

        _session.LineToolOptions.OrthoEnabled = enabled;
        _onMoveOrthoChanged?.Invoke();
    }

    private void ApplyMirrorOrthoEnabled(bool enabled)
    {
        if (_session is null)
        {
            return;
        }

        _session.MirrorToolOptions.OrthoEnabled = enabled;
        _onMirrorOrthoChanged?.Invoke();
    }

    private void SyncMoveOrthoCheckBoxFromSession()
    {
        if (_session is null)
        {
            return;
        }

        _suppressMoveOrthoEvents = true;
        MoveOrthoCheckBox.IsChecked = _session.LineToolOptions.OrthoEnabled;
        _suppressMoveOrthoEvents = false;
    }

    private void SyncMirrorOrthoCheckBoxFromSession()
    {
        if (_session is null)
        {
            return;
        }

        _suppressMirrorOrthoEvents = true;
        MirrorOrthoCheckBox.IsChecked = _session.MirrorToolOptions.OrthoEnabled;
        _suppressMirrorOrthoEvents = false;
    }

    private void DimensionOffsetTextBox_OnCommit(object sender, RoutedEventArgs e)
        => CommitDimensionOffset();

    private void DimensionOffsetTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitDimensionOffset();
            e.Handled = true;
        }
    }

    private void CommitDimensionOffset()
    {
        if (_session is null || _suppressDimensionOffsetEvents)
        {
            return;
        }

        if (_session.Selection.SelectedDimensionIds.Count != 1)
        {
            return;
        }

        if (!LinearInputParser.TryParse(
                DimensionOffsetTextBox.Text,
                _session.DisplayUnitSettings.LinearUnit,
                allowNegative: true,
                allowEmpty: false,
                out var offset))
        {
            return;
        }

        var dimensionId = _session.Selection.SelectedDimensionIds.First();
        var dimension = _session.Document.Dimensions.FirstOrDefault(item => item.Id == dimensionId);
        if (dimension is null)
        {
            return;
        }

        if (Math.Abs(dimension.Offset - offset) <= TopologyTolerance.ForMutation)
        {
            return;
        }

        _session.History.Record(_session.Document);
        if (!DimensionService.TrySetOffset(
                _session.Document,
                dimensionId,
                offset,
                TopologyTolerance.ForMutation))
        {
            return;
        }

        _requestRedraw?.Invoke();
    }

    private void DimensionTextSizeTextBox_OnCommit(object sender, RoutedEventArgs e)
        => CommitDimensionTextSize();

    private void DimensionTextSizeTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitDimensionTextSize();
            e.Handled = true;
        }
    }

    private void CommitDimensionTextSize()
    {
        if (_session is null || _suppressDimensionTextSizeEvents)
        {
            return;
        }

        if (_session.Selection.SelectedDimensionIds.Count != 1)
        {
            return;
        }

        if (!TryParseDimensionTextSize(
                DimensionTextSizeTextBox.Text,
                _session.DisplayUnitSettings.LinearUnit,
                out var textSize))
        {
            return;
        }

        var dimensionId = _session.Selection.SelectedDimensionIds.First();
        var dimension = _session.Document.Dimensions.FirstOrDefault(item => item.Id == dimensionId);
        if (dimension is null)
        {
            return;
        }

        if (Math.Abs(dimension.TextSize - textSize) <= TopologyTolerance.ForMutation)
        {
            return;
        }

        _session.History.Record(_session.Document);
        if (!DimensionService.TrySetTextSize(_session.Document, dimensionId, textSize))
        {
            return;
        }

        _requestRedraw?.Invoke();
    }

    private void DimensionToolTextSizeTextBox_OnCommit(object sender, RoutedEventArgs e)
        => CommitDimensionToolTextSize();

    private void DimensionToolTextSizeTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitDimensionToolTextSize();
            e.Handled = true;
        }
    }

    private void CommitDimensionToolTextSize()
    {
        if (_session is null || _suppressDimensionToolTextSizeEvents)
        {
            return;
        }

        if (!TryParseDimensionTextSize(
                DimensionToolTextSizeTextBox.Text,
                _session.DisplayUnitSettings.LinearUnit,
                out var textSize))
        {
            return;
        }

        if (Math.Abs(_session.DimensionToolOptions.TextSize - textSize) <= TopologyTolerance.ForMutation)
        {
            return;
        }

        _session.DimensionToolOptions.TextSize = Dimension.NormalizeTextSize(textSize);
        _requestRedraw?.Invoke();
    }

    private void SyncDimensionToolTextSizeFromSession()
    {
        if (_session is null)
        {
            return;
        }

        _suppressDimensionToolTextSizeEvents = true;
        DimensionToolTextSizeTextBox.Text = FormatDimensionTextSize(
            _session.DimensionToolOptions.TextSize,
            _session.DisplayUnitSettings.LinearUnit);
        _suppressDimensionToolTextSizeEvents = false;
    }

    private static bool TryParseDimensionTextSize(
        string? text,
        LinearDisplayUnit unit,
        out double textSize)
    {
        textSize = 0;
        if (string.IsNullOrWhiteSpace(text)
            || !LinearInputParser.TryParse(text, unit, allowNegative: false, allowEmpty: false, out textSize))
        {
            return false;
        }

        return double.IsFinite(textSize);
    }

    private static string FormatDimensionTextSize(double textSize, LinearDisplayUnit unit)
        => UnitDisplayFormatter.FormatLinear(Dimension.NormalizeTextSize(textSize), unit);

    private void DimensionToolExtensionStyleCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        => ApplyDimensionToolExtensionStyle();

    private void ApplyDimensionToolExtensionStyle()
    {
        if (_session is null || _suppressDimensionToolExtensionStyleEvents)
        {
            return;
        }

        if (DimensionToolExtensionStyleCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        _session.DimensionToolOptions.ExtensionStyle = item.Tag?.ToString() switch
        {
            "Short" => DimensionExtensionStyle.Short,
            _ => DimensionExtensionStyle.Full
        };
    }

    private void SyncDimensionToolExtensionStyleFromSession()
    {
        if (_session is null)
        {
            return;
        }

        _suppressDimensionToolExtensionStyleEvents = true;
        SelectDimensionExtensionStyle(
            DimensionToolExtensionStyleCombo,
            _session.DimensionToolOptions.ExtensionStyle);
        _suppressDimensionToolExtensionStyleEvents = false;
    }

    private void DimensionOrthoCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_session is null || _suppressDimensionOrthoEvents)
        {
            return;
        }

        ApplyDimensionOrthoEnabled(DimensionOrthoCheckBox.IsChecked == true);
    }

    private void ApplyDimensionOrthoEnabled(bool enabled)
    {
        if (_session is null)
        {
            return;
        }

        _session.DimensionToolOptions.OrthoEnabled = enabled;
    }

    private void SyncDimensionOrthoCheckBoxFromSession()
    {
        if (_session is null)
        {
            return;
        }

        _suppressDimensionOrthoEvents = true;
        DimensionOrthoCheckBox.IsChecked = _session.DimensionToolOptions.OrthoEnabled;
        _suppressDimensionOrthoEvents = false;
    }

    private void DimensionExtensionStyleCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        => CommitDimensionExtensionStyle();

    private void CommitDimensionExtensionStyle()
    {
        if (_session is null || _suppressDimensionExtensionStyleEvents)
        {
            return;
        }

        if (_session.Selection.SelectedDimensionIds.Count != 1)
        {
            return;
        }

        if (DimensionExtensionStyleCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var extensionStyle = item.Tag?.ToString() switch
        {
            "Short" => DimensionExtensionStyle.Short,
            _ => DimensionExtensionStyle.Full
        };

        var dimensionId = _session.Selection.SelectedDimensionIds.First();
        var dimension = _session.Document.Dimensions.FirstOrDefault(item => item.Id == dimensionId);
        if (dimension is null || dimension.ExtensionStyle == extensionStyle)
        {
            return;
        }

        _session.History.Record(_session.Document);
        if (!DimensionService.TrySetExtensionStyle(_session.Document, dimensionId, extensionStyle))
        {
            return;
        }

        _requestRedraw?.Invoke();
    }

    private void SelectDimensionExtensionStyle(ComboBox comboBox, DimensionExtensionStyle extensionStyle)
    {
        foreach (ComboBoxItem item in comboBox.Items)
        {
            var isMatch = item.Tag?.ToString() switch
            {
                "Short" => extensionStyle == DimensionExtensionStyle.Short,
                _ => extensionStyle == DimensionExtensionStyle.Full
            };

            if (isMatch)
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private void UpdateNoParametersVisibility(
        bool isLineTool,
        bool isAxisTool,
        bool isMoveTool,
        bool isMirrorTool,
        bool isOffsetTool = false,
        bool isDimensionTool = false,
        bool isFillTool = false)
    {
        NoParametersText.Visibility = isLineTool
            || isAxisTool
            || isMoveTool
            || isMirrorTool
            || isOffsetTool
            || isDimensionTool
            || isFillTool
            || DimensionSelectionPanel.Visibility == Visibility.Visible
            || AxisSelectionPanel.Visibility == Visibility.Visible
            || FaceSelectionPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void FillToolColorCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_session is null || _suppressFillToolEvents || FillToolColorCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        _session.FillToolOptions.FillColor = FaceFillService.ParsePaletteTag(item.Tag?.ToString());
    }

    private void FillToolPatternCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_session is null || _suppressFillToolEvents || FillToolPatternCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        _session.FillToolOptions.FillPattern = FaceFillService.ParsePatternTag(item.Tag?.ToString());
    }

    private void SelectFillToolColor(Color color)
    {
        var tag = FaceFillService.GetPaletteTag(color) ?? "LightGray";
        foreach (ComboBoxItem item in FillToolColorCombo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                FillToolColorCombo.SelectedItem = item;
                return;
            }
        }
    }

    private void SelectFillToolPattern(FaceFillPattern fillPattern)
    {
        var tag = FaceFillService.GetPatternTag(fillPattern);
        foreach (ComboBoxItem item in FillToolPatternCombo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                FillToolPatternCombo.SelectedItem = item;
                return;
            }
        }
    }

    private void FaceFillColorCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        => CommitFaceFillColor();

    private void FaceFillPatternCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        => CommitFaceFillPattern();

    private void CommitFaceFillColor()
    {
        if (_session is null || _suppressFaceFillEvents || FaceFillColorCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var polygon = GetSelectedFace(_session);
        if (polygon is null)
        {
            return;
        }

        var fillColor = FaceFillService.ParsePaletteTag(item.Tag?.ToString());
        var currentFill = FaceFillService.GetFill(_session.Document, polygon);
        if (currentFill.FillColor == fillColor)
        {
            return;
        }

        _session.History.Record(_session.Document);
        if (!FaceFillService.TrySetFillColor(_session.Document, polygon, fillColor))
        {
            return;
        }

        _requestRedraw?.Invoke();
    }

    private void CommitFaceFillPattern()
    {
        if (_session is null || _suppressFaceFillEvents || FaceFillPatternCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var polygon = GetSelectedFace(_session);
        if (polygon is null)
        {
            return;
        }

        var fillPattern = FaceFillService.ParsePatternTag(item.Tag?.ToString());

        var currentFill = FaceFillService.GetFill(_session.Document, polygon);
        if (currentFill.FillPattern == fillPattern)
        {
            return;
        }

        _session.History.Record(_session.Document);
        if (!FaceFillService.TrySetFillPattern(_session.Document, polygon, fillPattern))
        {
            return;
        }

        _requestRedraw?.Invoke();
    }

    private static Polygon? GetSelectedFace(CadSession session)
    {
        if (session.Selection.SelectedPolygonIds.Count != 1)
        {
            return null;
        }

        return session.Document.Polygons.FirstOrDefault(polygon =>
            session.Selection.SelectedPolygonIds.Contains(polygon.Id)
            && polygon.Type == PolygonType.Face);
    }

    private void SelectFaceFillColor(Color color)
    {
        var tag = FaceFillService.GetPaletteTag(color) ?? "LightGray";
        foreach (ComboBoxItem item in FaceFillColorCombo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                FaceFillColorCombo.SelectedItem = item;
                return;
            }
        }
    }

    private void SelectFaceFillPattern(FaceFillPattern fillPattern)
    {
        var tag = FaceFillService.GetPatternTag(fillPattern);
        foreach (ComboBoxItem item in FaceFillPatternCombo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                FaceFillPatternCombo.SelectedItem = item;
                return;
            }
        }
    }
}
