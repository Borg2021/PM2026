namespace ProjectManagement.Domain.Entities;

public class ProjectIssueMeasure
{
    public long Id { get; set; }
    public long IssueId { get; set; }
    public int SortOrder { get; set; }
    public string Measure { get; set; } = "";
    public string? MeasureType { get; set; }
    public long? ResponsibleDeptId { get; set; }
    public string? ResponsibleDeptName { get; set; }
    public long? ResponsibleUserId { get; set; }
    public string? ResponsibleUserName { get; set; }
    public string? Remark { get; set; }
    public DateOnly? PlannedDate { get; set; }

    public ProjectIssue Issue { get; set; } = null!;
}
