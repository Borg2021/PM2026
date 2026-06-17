namespace ProjectManagement.Application.Templates.DTOs;

public class MilestoneDto
{
    public long Id { get; set; }
    public string MilestoneCode { get; set; } = "";
    public string MilestoneName { get; set; } = "";
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}
