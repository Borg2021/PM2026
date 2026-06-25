namespace ProjectManagement.Domain.Entities;

public class ProjectFileVersion
{
    public long Id { get; set; }
    public long ProjectFileItemId { get; set; }
    public int VersionNumber { get; set; }
    public long UploadedBy { get; set; }
    public string UploadedByName { get; set; } = "";
    public DateTime UploadedAt { get; set; }
    public string? Remark { get; set; }

    public ProjectFileItem ProjectFileItem { get; set; } = null!;
    public List<ProjectFileVersionFile> Files { get; set; } = new();
}
