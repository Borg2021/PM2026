namespace ProjectManagement.Application.PlanBundles.DTOs;

public class PlanBundleDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int TemplateCount { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PlanBundleDetailDto : PlanBundleDto
{
    public List<PlanBundleItemDto> Items { get; set; } = new();
}

public class PlanBundleItemDto
{
    public long Id { get; set; }
    public long TemplateId { get; set; }
    public string TemplateCode { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public int SortOrder { get; set; }
}
