namespace ProjectManagement.Application.Templates.DTOs;

public class MemberDto
{
    public long Id { get; set; }
    public int SortOrder { get; set; }
    public long? RoleId { get; set; }
    public string? RoleName { get; set; }
    public long? MemberId { get; set; }
    public string? MemberName { get; set; }
    public long? DeptId { get; set; }
    public string? DeptName { get; set; }
    public string? Remark { get; set; }
}
