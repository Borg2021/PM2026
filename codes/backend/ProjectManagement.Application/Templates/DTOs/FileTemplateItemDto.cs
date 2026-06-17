namespace ProjectManagement.Application.Templates.DTOs;

public class FileTemplateItemDto
{
    public long Id { get; set; }
    public int SortOrder { get; set; }
    public string FileName { get; set; } = "";
    public bool Required { get; set; }
    public bool IsPublic { get; set; } = true;
    public string? ViewRoles { get; set; }
    public long? DeptId { get; set; }
    public string? DeptName { get; set; }
    public string? Remark { get; set; }
}
