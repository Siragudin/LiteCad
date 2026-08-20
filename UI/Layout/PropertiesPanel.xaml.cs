using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Dimensions;
using LiteCad.Infrastructure;
using LiteCad.Resources;
using LiteCad.Services;
using LiteCad.Tools;
using System.Globalization;
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
    private bool _suppressDimensionExtensionStyleEvents;
    private bool _suppressDimensionToolExtensionStyleEvents;
    private bool _suppressDimensionOrthoEvents;

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
    }

    public void SetActiveTool(ToolId toolId)
    {
        ActiveToolText.Text = ToolDisplayNames.Get(toolId);
        var isLineTool = toolId == ToolId.Line;
        var isMoveTool = toolId == ToolId.Move;
        var isMirrorTool = toolId == ToolId.Mirror;
        var isDimensionTool = toolId == ToolId.Dimension;
        LineToolPanel.Visibility = isLineTool ? Visibility.Visible : Visibility.Collapsed;
        MoveToolPanel.Visibility = isMoveTool ? Visibility.Visible : Visibility.Collapsed;
        MirrorToolPanel.Visibility = isMirrorTool ? Visibility.Visible : Visibility.Collapsed;
        DimensionToolPanel.Visibility = isDimensionTool ? Visibility.Visible : Visibility.Collapsed;

        if (_session is not null)
        {
            SyncDimensionSelection(_session);
        }

        UpdateNoParametersVisibility(isLineTool, isMoveTool, isMirrorTool, isDimensionTool);

        if (isMoveTool)
        {
            SyncMoveOrthoCheckBoxFromSession();
        }

        if (isMirrorTool)
        {
            SyncMirrorOrthoCheckBoxFromSession();
        }

        if (isDimensionTool)
        {
            SyncDimensionToolExtensionStyleFromSession();
            SyncDimensionOrthoCheckBoxFromSession();
        }
    }

    public void SetSelection(string selectionInfo)
    {
        SelectionText.Text = selectionInfo;
        if (_session is not null)
        {
            SyncDimensionSelection(_session);
        }
    }

    public void SyncDimensionSelection(CadSession session)
    {
        var isDimensionToolActive = session.ToolService.ActiveTool?.Id == ToolId.Dimension;
        var showDimensionSelectionPanel = !isDimensionToolActive
            && session.Selection.SelectedDimensionIds.Count == 1
            && session.Selection.SelectedEdgeIds.Count == 0
            && session.Selection.SelectedPolygonIds.Count == 0
            && session.Selection.SelectedVertexIds.Count == 0;

        DimensionSelectionPanel.Visibility = showDimensionSelectionPanel
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (showDimensionSelectionPanel)
        {
            var dimension = session.Document.Dimensions.First(item =>
                session.Selection.SelectedDimensionIds.Contains(item.Id));
            _suppressDimensionOffsetEvents = true;
            DimensionOffsetTextBox.Text = dimension.Offset.ToString("F2", CultureInfo.InvariantCulture);
            _suppressDimensionOffsetEvents = false;

            _suppressDimensionExtensionStyleEvents = true;
            SelectDimensionExtensionStyle(DimensionExtensionStyleCombo, dimension.ExtensionStyle);
            _suppressDimensionExtensionStyleEvents = false;
        }

        var activeTool = session.ToolService.ActiveTool?.Id;
        UpdateNoParametersVisibility(
            activeTool == ToolId.Line,
            activeTool == ToolId.Move,
            activeTool == ToolId.Mirror,
            activeTool == ToolId.Dimension);
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

    private void MoveOrthoCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_session is null || _suppressMoveOrthoEvents)
        {
            return;
        }

        ApplyMoveOrthoEnabled(MoveOrthoCheckBox.IsChecked == true);
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

        if (!TryParseOffset(DimensionOffsetTextBox.Text, out var offset))
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

    private static bool TryParseOffset(string text, out double offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim().Replace(',', '.');
        return double.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out offset);
    }

    private void UpdateNoParametersVisibility(
        bool isLineTool,
        bool isMoveTool,
        bool isMirrorTool,
        bool isDimensionTool = false)
    {
        NoParametersText.Visibility = isLineTool
            || isMoveTool
            || isMirrorTool
            || isDimensionTool
            || DimensionSelectionPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
