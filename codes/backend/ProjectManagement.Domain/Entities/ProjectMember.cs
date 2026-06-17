namespace ProjectManagement.Domain.Entities;

public class ProjectMember
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public int SortOrder { get; set; }
    public long? RoleId { get; set; }
    public string? RoleName { get; set; }
    public long? MemberId { get; set; }
    public string? MemberName { get; set; }
    public long? DeptId { get; set; }
    public string? DeptName { get; set; }
    public long? FunctionId { get; set; }
    public string? FunctionName { get; set; }
    public string? Remark { get; set; }

    public Project? Project { get; set; }
}
