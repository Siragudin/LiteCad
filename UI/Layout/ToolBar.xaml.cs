using LiteCad.Resources;
using LiteCad.UI;
using System.Windows;
using System.Windows.Controls;

namespace LiteCad.UI.Layout;

public partial class ToolBar : UserControl
{
    private readonly Dictionary<string, Button> _toolButtons = new(StringComparer.Ordinal);

    public event EventHandler<string>? ToolRequested;

    public ToolBar()
    {
        InitializeComponent();
        Loaded += ToolBar_OnLoaded;
    }

    public void SetActiveTool(string toolTag)
    {
        foreach (var (tag, button) in _toolButtons)
        {
            ToolBarProperties.SetIsActive(button, tag == toolTag);
        }
    }

    public void RefreshLocalizedLabels()
    {
        SetButtonTooltip("Selection", Strings.Tooltip_Tool_Selection);
        SetButtonTooltip("Hand", Strings.Tooltip_Tool_Hand);
        SetButtonTooltip("Line", Strings.Tooltip_Tool_Line);
        SetButtonTooltip("Arc", Strings.Tooltip_Tool_Arc);
        SetButtonTooltip("Rectangle", Strings.Tooltip_Tool_Rectangle);
        SetButtonTooltip("Circle", Strings.Tooltip_Tool_Circle);
        SetButtonTooltip("Sector", Strings.Tooltip_Tool_Sector);
        SetButtonTooltip("Move", Strings.Tooltip_Tool_Move);
        SetButtonTooltip("Rotate", Strings.Tooltip_Tool_Rotate);
        SetButtonTooltip("Mirror", Strings.Tooltip_Tool_Mirror);
        SetButtonTooltip("Offset", Strings.Tooltip_Tool_Offset);
        SetButtonTooltip("Stretch", Strings.Tooltip_Tool_Stretch);
        SetButtonTooltip("Extend", Strings.Tooltip_Tool_Extend);
        SetButtonTooltip("Dimension", Strings.Tooltip_Tool_Dimension);
        SetButtonTooltip("Eraser", Strings.Tooltip_Tool_Eraser);
        SetButtonTooltip("PolygonEdit", Strings.Tooltip_Tool_PolygonEdit);
    }

    private void ToolBar_OnLoaded(object sender, RoutedEventArgs e)
    {
        _toolButtons.Clear();
        foreach (var child in ToolButtonPanel.Children)
        {
            if (child is Button { Tag: string tag })
            {
                _toolButtons[tag] = (Button)child;
            }
        }
    }

    private void ToolButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string toolTag })
        {
            ToolRequested?.Invoke(this, toolTag);
        }
    }

    private void SetButtonTooltip(string tag, string tooltip)
    {
        if (_toolButtons.TryGetValue(tag, out var button))
        {
            button.ToolTip = tooltip;
        }
    }
}
