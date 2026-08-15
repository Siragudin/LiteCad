using System.Windows;
using System.Windows.Controls;

namespace LiteCad.UI.Layout;

public partial class MenuBar : UserControl
{
    public event EventHandler<string>? ToolRequested;

    public event EventHandler<string>? EditCommandRequested;

    public event EventHandler<string>? FileCommandRequested;

    public MenuBar()
    {
        InitializeComponent();
    }

    private void ToolMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string toolTag })
        {
            ToolRequested?.Invoke(this, toolTag);
        }
    }

    private void EditMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string command })
        {
            EditCommandRequested?.Invoke(this, command);
        }
    }

    private void FileMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string command })
        {
            FileCommandRequested?.Invoke(this, command);
        }
    }
}
