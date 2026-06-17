namespace ProjectManagement.Domain.Entities;

public class Permission
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public long? ParentId { get; set; }
    public int Type { get; set; } = 1;
    public int SortOrder { get; set; } = 0;
    public string? Icon { get; set; }
    public string? Path { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Permission? Parent { get; set; }
    public List<Permission> Children { get; set; } = new();
}
