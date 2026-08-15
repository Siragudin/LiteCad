using System.Windows;
using System.Windows.Controls;

namespace LiteCad.UI.Layout;

public partial class ToolBar : UserControl
{
    public event EventHandler<string>? ToolRequested;

    public ToolBar()
    {
        InitializeComponent();
    }

    private void ToolButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string toolTag })
        {
            ToolRequested?.Invoke(this, toolTag);
        }
    }
}
