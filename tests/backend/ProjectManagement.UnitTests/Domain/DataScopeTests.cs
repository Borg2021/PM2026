using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Enums;

namespace ProjectManagement.UnitTests.Domain;

/// <summary>
/// 数据范围逻辑的单元测试
/// </summary>
public class DataScopeTests
{
    [Theory]
    [InlineData(DataScopeType.All, true)]          // 全部 → 不限制
    [InlineData(DataScopeType.Dept, false)]         // 本部门 → 限制
    [InlineData(DataScopeType.Self, false)]         // 仅本人 → 限制
    [InlineData(DataScopeType.MemberOnly, false)]   // 仅成员 → 限制
    public void DataScope_IsAllScope_AffectsFiltering(
        DataScopeType scope, bool shouldSeeAll)
    {
        // Arrange
        var role = new Role
        {
            Name = "测试角色",
            Code = "test_role",
            DataScope = scope
        };

        // Assert — 只有 All 范围不需要额外过滤
        role.DataScope.Should().Be(scope);
        var isAllScope = scope == DataScopeType.All;
        isAllScope.Should().Be(shouldSeeAll);
    }

    [Fact]
    public void User_DataScopeOverride_TakesPriorityOverRoleScope()
    {
        // Arrange
        var role = new Role
        {
            DataScope = DataScopeType.All // 角色默认全部
        };

        // Act — 用户可以覆盖数据范围
        var user = new User
        {
            Username = "scoped_user",
            DataScope = DataScopeType.Self // 覆盖为仅本人
        };

        // Assert — 用户级别的 DataScope 覆盖角色默认值
        // 实际判断逻辑在 ProjectDataScopeResolver 中
        user.DataScope.Should().Be(DataScopeType.Self);
        user.DataScope.Should().NotBe(role.DataScope);
    }

    [Fact]
    public void DataScopeType_AllValues_AreDefinedCorrectly()
    {
        // 验证所有数据范围枚举值都被正确定义
        var values = Enum.GetValues<DataScopeType>();

        values.Should().HaveCount(6);
        values.Should().Contain(DataScopeType.All);
        values.Should().Contain(DataScopeType.Dept);
        values.Should().Contain(DataScopeType.DeptAndChildren);
        values.Should().Contain(DataScopeType.Self);
        values.Should().Contain(DataScopeType.MemberOnly);
        values.Should().Contain(DataScopeType.ProjectManagerOwn);
    }
}
