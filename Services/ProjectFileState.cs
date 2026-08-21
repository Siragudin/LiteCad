namespace LiteCad.Services;

public sealed class ProjectFileState
{
    public string? CurrentFilePath { get; private set; }

    public bool IsDirty { get; private set; }

    public bool HasSavedPath => !string.IsNullOrWhiteSpace(CurrentFilePath);

    public string DisplayName
        => HasSavedPath
            ? ProjectFileNameHelper.GetProjectDisplayName(CurrentFilePath)
            : string.Empty;

    public void MarkDirty()
    {
        IsDirty = true;
    }

    public void MarkSaved(string filePath)
    {
        CurrentFilePath = ProjectFileNameHelper.NormalizeSitFilePath(filePath);
        IsDirty = false;
    }

    public void Reset()
    {
        CurrentFilePath = null;
        IsDirty = false;
    }
}
