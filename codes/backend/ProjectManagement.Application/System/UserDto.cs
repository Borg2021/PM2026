namespace ProjectManagement.Application.System;

public class UserDto
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string RealName { get; set; } = "";
    public string Role { get; set; } = "";
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public List<long> FunctionIds { get; set; } = new();
    public string? FunctionNames { get; set; }
    public List<string> RbacRoleNames { get; set; } = new();
}
