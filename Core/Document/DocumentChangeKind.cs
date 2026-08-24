namespace LiteCad.Core.Document;

[Flags]
public enum DocumentChangeKind
{
    None = 0,
    Topology = 1 << 0,
    Annotations = 1 << 1,
    Axes = 1 << 2,
    FaceFill = 1 << 3,
    FullReplace = Topology | Annotations | Axes | FaceFill
}

public sealed class DocumentChangedEventArgs : EventArgs
{
    public DocumentChangedEventArgs(long revision, DocumentChangeKind kind)
    {
        Revision = revision;
        Kind = kind;
    }

    public long Revision { get; }

    public DocumentChangeKind Kind { get; }
}
