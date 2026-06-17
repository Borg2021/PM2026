namespace ProjectManagement.Domain.Entities;

public class User
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string RealName { get; set; } = "";
    public string Role { get; set; } = "user";
    public int Status { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public List<UserFunction> UserFunctions { get; set; } = new();
    /// <summary>用户级数据范围覆盖；NULL 表示跟随角色。</summary>
    public int? DataScope { get; set; }
    public List<UserRole> UserRoles { get; set; } = new();
}
