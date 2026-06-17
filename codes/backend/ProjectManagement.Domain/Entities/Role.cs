namespace ProjectManagement.Domain.Entities;

public class Role
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    public int Status { get; set; } = 1;
    /// <summary>数据范围，见 <see cref="Enums.DataScopeType"/>。</summary>
    public int DataScope { get; set; } = (int)Enums.DataScopeType.MemberOnly;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RolePermission> RolePermissions { get; set; } = new();
}
