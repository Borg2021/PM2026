namespace ProjectManagement.Application.Templates.DTOs;

public class TemplateDetailDto
{
    public long Id { get; set; }
    public string TemplateCode { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public int TemplateType { get; set; }
    public string TemplateTypeName { get; set; } = "";
    public string? Description { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PlanNodeDto> PlanNodes { get; set; } = new();
    public List<MilestoneDto> Milestones { get; set; } = new();
    public List<MemberDto> Members { get; set; } = new();
    public List<FileTemplateItemDto> FileItems { get; set; } = new();
}
