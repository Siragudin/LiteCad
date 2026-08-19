using LiteCad.Core.Document;
using LiteCad.Infrastructure;
using LiteCad.Resources;
using LiteCad.Services;
using LiteCad.Tools;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LiteCad.UI.Layout;

public partial class PropertiesPanel : UserControl
{
    private static readonly object InitializeComponentLock = new();
    private CadSession? _session;
    private Action? _onMoveOrthoChanged;
    private Action? _onMirrorOrthoChanged;
    private bool _suppressMoveOrthoEvents;
    private bool _suppressMirrorOrthoEvents;

    public PropertiesPanel()
    {
        lock (InitializeComponentLock)
        {
            InitializeComponent();
        }
    }

    public bool IsMoveToolPanelVisible => MoveToolPanel.Visibility == Visibility.Visible;

    public bool IsMirrorToolPanelVisible => MirrorToolPanel.Visibility == Visibility.Visible;

    public bool IsMoveOrthoToggleVisible => IsMoveToolPanelVisible;

    public bool IsMirrorOrthoToggleVisible => IsMirrorToolPanelVisible;

    public bool MoveOrthoIsChecked => MoveOrthoCheckBox.IsChecked == true;

    public bool MirrorOrthoIsChecked => MirrorOrthoCheckBox.IsChecked == true;

    public void BindSession(
        CadSession session,
        Action? onMoveOrthoChanged = null,
        Action? onMirrorOrthoChanged = null)
    {
        _session = session;
        _onMoveOrthoChanged = onMoveOrthoChanged;
        _onMirrorOrthoChanged = onMirrorOrthoChanged;
    }

    public void SetActiveTool(ToolId toolId)
    {
        ActiveToolText.Text = ToolDisplayNames.Get(toolId);
        var isLineTool = toolId == ToolId.Line;
        var isMoveTool = toolId == ToolId.Move;
        var isMirrorTool = toolId == ToolId.Mirror;
        LineToolPanel.Visibility = isLineTool ? Visibility.Visible : Visibility.Collapsed;
        MoveToolPanel.Visibility = isMoveTool ? Visibility.Visible : Visibility.Collapsed;
        MirrorToolPanel.Visibility = isMirrorTool ? Visibility.Visible : Visibility.Collapsed;
        NoParametersText.Visibility = isLineTool || isMoveTool || isMirrorTool
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (isMoveTool)
        {
            SyncMoveOrthoCheckBoxFromSession();
        }

        if (isMirrorTool)
        {
            SyncMirrorOrthoCheckBoxFromSession();
        }
    }

    public void SetSelection(string selectionInfo)
    {
        SelectionText.Text = selectionInfo;
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
}
