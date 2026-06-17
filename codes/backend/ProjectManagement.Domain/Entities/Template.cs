namespace ProjectManagement.Domain.Entities;

public class Template
{
    public long Id { get; set; }
    public string TemplateCode { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public int TemplateType { get; set; }
    public string? Description { get; set; }
    public int Status { get; set; } = 1;
    public long? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }

    public List<PlanNode> PlanNodes { get; set; } = new();
    public List<Milestone> Milestones { get; set; } = new();
    public List<TemplateMember> Members { get; set; } = new();
    public List<FileTemplateItem> FileItems { get; set; } = new();
}
