using LiteCad.Tools;

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
}
