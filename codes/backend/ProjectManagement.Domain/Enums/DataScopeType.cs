namespace ProjectManagement.Domain.Enums;

/// <summary>数据范围：数值越小范围越大。</summary>
public enum DataScopeType
{
    All = 1,
    Dept = 2,
    DeptAndChildren = 3,
    Self = 4,
    MemberOnly = 5,
    ProjectManagerOwn = 6
}
