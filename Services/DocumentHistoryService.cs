namespace LiteCad.Services;

public sealed class DocumentHistoryService
{
    private readonly Stack<Core.Document.DocumentSnapshot> _undoStack = new();
    private readonly Stack<Core.Document.DocumentSnapshot> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public void Record(Core.Document.CadDocument document)
    {
        _undoStack.Push(Core.Document.DocumentSnapshot.Capture(document));
        _redoStack.Clear();
    }

    public bool Undo(Core.Document.CadDocument document, double tolerance)
    {
        if (_undoStack.Count == 0)
        {
            return false;
        }

        _redoStack.Push(Core.Document.DocumentSnapshot.Capture(document));
        _undoStack.Pop().Restore(document, tolerance);
        return true;
    }

    public bool Redo(Core.Document.CadDocument document, double tolerance)
    {
        if (_redoStack.Count == 0)
        {
            return false;
        }

        _undoStack.Push(Core.Document.DocumentSnapshot.Capture(document));
        _redoStack.Pop().Restore(document, tolerance);
        return true;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
