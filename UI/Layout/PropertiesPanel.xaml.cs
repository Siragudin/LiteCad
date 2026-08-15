using LiteCad.Core.Document;
using LiteCad.Infrastructure;
using LiteCad.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LiteCad.UI.Layout;

public partial class PropertiesPanel : UserControl
{
    private CadSession? _session;

    public PropertiesPanel()
    {
        InitializeComponent();
    }

    public void BindSession(CadSession session)
    {
        _session = session;
    }

    public void SetActiveTool(string toolName)
    {
        ActiveToolText.Text = toolName;
        var isLineTool = toolName == "Line";
        LineToolPanel.Visibility = isLineTool ? Visibility.Visible : Visibility.Collapsed;
        NoParametersText.Visibility = isLineTool ? Visibility.Collapsed : Visibility.Visible;
    }

    public void SetSelection(string selectionInfo)
    {
        SelectionText.Text = selectionInfo;
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
}
