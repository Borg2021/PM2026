namespace ProjectManagement.Domain.Entities;

public class ProjectFileVersionFile
{
    public long Id { get; set; }
    public long ProjectFileVersionId { get; set; }
    public string FilePath { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public long FileSize { get; set; }
    public string? FileExt { get; set; }

    public ProjectFileVersion Version { get; set; } = null!;
}
