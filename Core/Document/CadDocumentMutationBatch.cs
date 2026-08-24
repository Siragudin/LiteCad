namespace LiteCad.Core.Document;

public sealed class CadDocumentMutationBatch : IDisposable
{
    private readonly CadDocument _document;
    private bool _disposed;

    internal CadDocumentMutationBatch(CadDocument document)
    {
        _document = document;
        _document.BeginMutationBatchCore();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _document.EndMutationBatchCore();
    }
}
