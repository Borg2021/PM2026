namespace ProjectManagement.Domain.Entities;

public class ProjectFile
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
