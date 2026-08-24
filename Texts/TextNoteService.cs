using LiteCad.Core.Document;
using LiteCad.Core.Geometry;
using LiteCad.Core.Selection;

namespace LiteCad.Texts;

public static class TextNoteService
{
    public static TextNote Create(
        CadDocument document,
        TextNoteKind kind,
        PointF origin,
        PointF? arrowTip,
        string text,
        double textSize)
    {
        var note = new TextNote(Guid.NewGuid(), kind, origin, arrowTip, text, textSize);
        document.Texts.Add(note);
        document.NotifyChanged(DocumentChangeKind.Annotations);
        return note;
    }

    public static int DeleteSelected(CadDocument document, Selection selection)
    {
        if (selection.SelectedTextIds.Count == 0)
        {
            return 0;
        }

        var removed = document.Texts.RemoveAll(item => selection.SelectedTextIds.Contains(item.Id));
        selection.SelectedTextIds.Clear();
        if (removed > 0)
        {
            document.NotifyChanged(DocumentChangeKind.Annotations);
        }

        return removed;
    }
}
