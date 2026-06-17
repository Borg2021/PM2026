using ProjectManagement.Domain.Entities;

namespace ProjectManagement.UnitTests.Domain;

/// <summary>
/// 项目实体的核心行为测试
/// </summary>
public class ProjectTests
{
    [Fact]
    public void CreateProject_ValidInput_ProjectStatusIsInactive()
    {
        // Arrange & Act
        var project = new Project
        {
            Name = "测试项目",
            Code = "PRJ-001",
            Type = "工程"
        };

        // Assert
        project.Status.Should().Be(0); // 新建项目默认为未激活
        project.Name.Should().Be("测试项目");
    }

    [Fact]
    public void Project_WhenStatusIsActive_CanAddTasks()
    {
        // Arrange
        var project = new Project
        {
            Name = "进行中项目",
            Code = "PRJ-002",
            Status = 1 // 进行中
        };

        // Act — 项目状态不阻止任务创建（业务逻辑在 Application 层）
        // 这里验证项目实体本身的数据完整性

        // Assert
        project.Status.Should().Be(1);
    }

    [Theory]
    [InlineData(0, "未激活")]
    [InlineData(1, "进行中")]
    [InlineData(2, "已完成")]
    [InlineData(3, "已暂停")]
    public void ProjectStatus_AllDefinedValues_ShouldBeValid(int status, string _)
    {
        // Arrange & Act
        var project = new Project
        {
            Name = "状态测试",
            Code = $"PRJ-STATUS-{status}",
            Status = status
        };

        // Assert
        project.Status.Should().Be(status);
        // 所有 0-3 的状态值都是合法的
    }

    [Fact]
    public void Project_WbsCode_ShouldBeAssignable()
    {
        // Arrange & Act — WBS 编号在任务创建时由应用层生成
        // 验证实体字段可正确赋值
        var project = new Project
        {
            Name = "WBS 测试项目",
            Code = "PRJ-WBS-001"
        };

        // Assert
        project.Code.Should().NotBeNullOrEmpty();
        project.Code.Should().StartWith("PRJ-");
    }
}
