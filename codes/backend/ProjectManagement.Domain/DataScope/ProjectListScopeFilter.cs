using ProjectManagement.Domain.Enums;

namespace ProjectManagement.Domain.DataScope;

public class ProjectListScopeFilter
{
    public bool Bypass { get; init; }
    public bool WorkbenchSelfOnly { get; init; }
    public DataScopeType Scope { get; init; } = DataScopeType.MemberOnly;
    public long UserId { get; init; }
    public long? UserDeptId { get; init; }
    public string? UserDeptName { get; init; }
    public IReadOnlyList<long> AllowedDeptIds { get; init; } = Array.Empty<long>();
    /// <summary>用户担任负责人的部门及其所有子孙部门 ID。用于部门负责人可见性。</summary>
    public IReadOnlyList<long> LeaderDeptIds { get; init; } = Array.Empty<long>();
    /// <summary>普通用户不显示暂停的项目</summary>
    public bool HideSuspended { get; init; }
}
