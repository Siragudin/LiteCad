using LiteCad.Tools;
using System.Windows.Input;

namespace LiteCad.Services;

public sealed class ToolService
{
    private ToolContext? _context;

    public ITool? ActiveTool { get; private set; }

    public void Initialize(ToolContext context)
    {
        _context = context;
    }

    public void ActivateTool(ITool tool)
    {
        ActiveTool?.OnDeactivated();
        ActiveTool = tool;

        if (tool is ToolBase toolBase && _context is not null)
        {
            toolBase.AttachContext(_context);
        }

        ActiveTool.OnActivated();
    }

    public bool TryHandleAltRightClick(MouseButtonEventArgs e, ModifierKeys modifiers)
    {
        if (!IsAltRightClick(e, modifiers))
        {
            return false;
        }

        if (ActiveTool?.Id != ToolId.Selection)
        {
            _context?.ActivateSelectionTool();
        }

        return true;
    }

    public static bool IsAltRightClick(MouseButtonEventArgs e, ModifierKeys modifiers)
        => e.ChangedButton == MouseButton.Right
           && (modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
}
