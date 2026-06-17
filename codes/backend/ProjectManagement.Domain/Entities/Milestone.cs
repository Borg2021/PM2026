namespace ProjectManagement.Domain.Entities;

public class Milestone
{
    public long Id { get; set; }
    public long TemplateId { get; set; }
    public string MilestoneCode { get; set; } = "";
    public string MilestoneName { get; set; } = "";
    public int SortOrder { get; set; }
    public string? Remark { get; set; }

    public Template Template { get; set; } = null!;
}
