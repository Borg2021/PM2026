using ProjectManagement.Domain.DataScope;
using ProjectManagement.Domain.Entities;

namespace ProjectManagement.Domain.Interfaces;

public interface IProjectRepository
{
    Task<(List<Project> Items, int Total)> GetListAsync(
        string? projectCode, string? projectName, string? projectType,
        int? status, string? projectManagerName, long? memberId, long? assigneeId, int pageIndex, int pageSize,
        ProjectListScopeFilter? scopeFilter = null);

    Task<Project?> GetByIdAsync(long id);
    Task<Project> AddAsync(Project project);
    Task UpdateAsync(Project project);
    Task<bool> ProjectCodeExistsAsync(string code, long? excludeId = null);

    Task SaveProductsAsync(long projectId, List<ProjectProduct> products);
    Task SaveMembersAsync(long projectId, List<ProjectMember> members);
    Task SaveMilestonesAsync(long projectId, List<ProjectMilestone> milestones);

    // 任务计划
    Task<List<ProjectTask>> GetTasksAsync(long projectId);
    Task<Dictionary<long, (decimal? Progress, decimal? PlannedProgress, DateTime? PlanStart, DateTime? PlanFinish, int? PlanDuration)>> GetFirstTaskProgressMapAsync(List<long> projectIds);
    Task<ProjectTask?> GetTaskByIdAsync(long taskId);
    Task<ProjectTask> AddTaskAsync(ProjectTask task);
    Task UpdateTaskAsync(ProjectTask task);
    Task DeleteTaskAsync(long taskId);
    Task<string?> GetSysParamValueAsync(string key);

    // 变更记录
    Task<List<ProjectChange>> GetChangesAsync(long projectId);
    Task<ProjectChange> AddChangeAsync(ProjectChange change);
    Task UpdateChangeAsync(ProjectChange change);
    Task DeleteChangeAsync(long changeId);

    // 财务信息
    Task<ProjectFinance?> GetFinanceAsync(long projectId);
    Task SaveFinanceAsync(ProjectFinance finance);
    Task SavePlanReceiptsAsync(long financeId, List<ProjectPlanReceipt> records);
    Task SaveReceiptsAsync(long financeId, List<ProjectReceipt> records);
    Task SaveInvoicesAsync(long financeId, List<ProjectInvoice> records);
}
