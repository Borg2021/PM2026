namespace ProjectManagement.Domain.Entities;

public class ProjectMilestone
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public int SortOrder { get; set; }
    public string MilestoneCode { get; set; } = "";
    public string MilestoneName { get; set; } = "";

    /// <summary>状态：0=未完成, 1=已完成</summary>
    public int Status { get; set; } = 0;

    public DateTime? PlanFinishDate { get; set; }
    public DateTime? ActualFinishDate { get; set; }
    public string? NodeReference { get; set; }
    public string? Remark { get; set; }

    public Project? Project { get; set; }
}
