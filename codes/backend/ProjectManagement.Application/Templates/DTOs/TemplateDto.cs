namespace ProjectManagement.Application.Templates.DTOs;

public class TemplateDto
{
    public long Id { get; set; }
    public string TemplateCode { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public int TemplateType { get; set; }
    public string TemplateTypeName { get; set; } = "";
    public string? Description { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Status { get; set; }
}
