namespace ProjectManagement.Domain.Entities;

public class ProjectFileItem
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public long? TemplateItemId { get; set; }
    public bool IsPublic { get; set; } = true;
    public string? ViewRoles { get; set; }
    public int SortOrder { get; set; }
    public string FileName { get; set; } = "";
    public bool Required { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public long? DeptId { get; set; }
    public string? DeptName { get; set; }
    public DateTime? PlanFinishDate { get; set; }
    public long? LatestVersionId { get; set; }
    public string? Remark { get; set; }

    public Project Project { get; set; } = null!;
    public ProjectFileVersion? LatestVersion { get; set; }
    public List<ProjectFileVersion> Versions { get; set; } = new();
}
