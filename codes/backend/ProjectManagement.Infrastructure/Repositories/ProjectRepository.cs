using Microsoft.EntityFrameworkCore;
using ProjectManagement.Domain.DataScope;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Infrastructure.Data;

namespace ProjectManagement.Infrastructure.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _db;

    public ProjectRepository(AppDbContext db) => _db = db;

    public async Task<(List<Project> Items, int Total)> GetListAsync(
        string? projectCode, string? projectName, string? projectType,
        int? status, string? projectManagerName, long? memberId, long? assigneeId, int pageIndex, int pageSize,
        ProjectListScopeFilter? scopeFilter = null)
    {
        var query = _db.Projects.AsQueryable();
        query = ProjectDataScopeFilter.Apply(query, _db, scopeFilter);

        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(p => p.ProjectCode.Contains(projectCode));
        if (!string.IsNullOrWhiteSpace(projectName))
            query = query.Where(p => p.ProjectName.Contains(projectName));
        if (!string.IsNullOrWhiteSpace(projectType))
            query = query.Where(p => p.ProjectType != null && p.ProjectType.Contains(projectType));
        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(projectManagerName))
            query = query.Where(p => p.ProjectManagerName != null && p.ProjectManagerName.Contains(projectManagerName));
        if (memberId.HasValue && assigneeId.HasValue)
            query = query.Where(p => p.Members.Any(m => m.MemberId == memberId.Value) || p.Tasks.Any(t => t.AssigneeId == assigneeId.Value));
        else if (memberId.HasValue)
            query = query.Where(p => p.Members.Any(m => m.MemberId == memberId.Value));
        else if (assigneeId.HasValue)
            query = query.Where(p => p.Tasks.Any(t => t.AssigneeId == assigneeId.Value));

        // 非系统管理员过滤未激活(0)，但项目经理可看到自己负责的未激活项目
        //（绕过 scopeFilter.Bypass，确保工作台路径始终生效）
        // 暂停(3)由 scopeFilter.HideSuspended 控制（项目经理可看到自己的暂停项目）
        if (scopeFilter != null)
        {
            var isAdminOrProjectAdmin = await _db.UserRoles.AnyAsync(ur =>
                ur.UserId == scopeFilter.UserId && (ur.Role.Code == "admin" || ur.Role.Code == "project_admin"));
            if (!isAdminOrProjectAdmin)
                query = query.Where(p => p.Status != 0 || p.ProjectManagerId == scopeFilter.UserId);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.ProjectCode)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Project?> GetByIdAsync(long id)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id);
        if (project == null) return null;

        await _db.Entry(project).Collection(p => p.Products).LoadAsync();
        await _db.Entry(project).Collection(p => p.Members).LoadAsync();
        await _db.Entry(project).Collection(p => p.Milestones).LoadAsync();

        return project;
    }

    public async Task<Project> AddAsync(Project project)
    {
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return project;
    }

    public async Task UpdateAsync(Project project)
    {
        _db.Projects.Update(project);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ProjectCodeExistsAsync(string code, long? excludeId = null)
    {
        var query = _db.Projects.Where(p => p.ProjectCode == code);
        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task SaveProductsAsync(long projectId, List<ProjectProduct> products)
    {
        var existing = await _db.ProjectProducts.Where(p => p.ProjectId == projectId).ToListAsync();
        _db.ProjectProducts.RemoveRange(existing);
        foreach (var p in products) { p.Id = 0; }
        _db.ProjectProducts.AddRange(products);
        await _db.SaveChangesAsync();
    }

    public async Task SaveMembersAsync(long projectId, List<ProjectMember> members)
    {
        var existing = await _db.ProjectMembers.Where(m => m.ProjectId == projectId).ToListAsync();
        _db.ProjectMembers.RemoveRange(existing);
        foreach (var m in members) { m.Id = 0; }
        _db.ProjectMembers.AddRange(members);
        await _db.SaveChangesAsync();
    }

    public async Task SaveMilestonesAsync(long projectId, List<ProjectMilestone> milestones)
    {
        var existing = await _db.ProjectMilestones.Where(m => m.ProjectId == projectId).ToListAsync();
        _db.ProjectMilestones.RemoveRange(existing);
        foreach (var m in milestones) { m.Id = 0; }
        _db.ProjectMilestones.AddRange(milestones);
        await _db.SaveChangesAsync();
    }

    // ────── 任务计划 ──────

    public async Task<List<ProjectTask>> GetTasksAsync(long projectId)
        => await _db.ProjectTasks.Where(t => t.ProjectId == projectId).OrderBy(t => t.TaskNo).ToListAsync();

    public async Task<Dictionary<long, (decimal? Progress, decimal? PlannedProgress, DateTime? PlanStart, DateTime? PlanFinish, int? PlanDuration)>> GetFirstTaskProgressMapAsync(List<long> projectIds)
    {
        var tasks = await _db.ProjectTasks
            .Where(t => projectIds.Contains(t.ProjectId))
            .ToListAsync();
        var today = DateTime.Today;

        return tasks
            .GroupBy(t => t.ProjectId)
            .ToDictionary(g => g.Key, g =>
            {
                var valid = g.Where(t => t.PlanDuration.HasValue && t.PlanDuration.Value > 0).ToList();
                decimal? progress;
                if (valid.Count == 0)
                    progress = g.First().ProgressPct;
                else
                {
                    var totalWeight = valid.Sum(t => t.PlanDuration!.Value);
                    var weightedSum = valid.Sum(t => t.PlanDuration!.Value * t.ProgressPct);
                    progress = totalWeight > 0 ? Math.Round(weightedSum / totalWeight, 1) : g.First().ProgressPct;
                }

                // Planned progress: weighted average of (elapsed / planDuration) per root task
                decimal? plannedProgress = null;
                var plannedValid = g.Where(t => t.PlanDuration.HasValue && t.PlanDuration.Value > 0 && t.PlanStartDate.HasValue).ToList();
                if (plannedValid.Count > 0)
                {
                    decimal ppWeightSum = 0;
                    decimal ppTotalWeight = 0;
                    foreach (var t in plannedValid)
                    {
                        var elapsed = Math.Max(0, (today - t.PlanStartDate!.Value).Days);
                        var pct = Math.Min(100, Math.Round((decimal)elapsed / t.PlanDuration!.Value * 100, 1));
                        ppWeightSum += t.PlanDuration!.Value;
                        ppTotalWeight += t.PlanDuration!.Value * pct;
                    }
                    plannedProgress = ppWeightSum > 0 ? Math.Round(ppTotalWeight / ppWeightSum, 1) : null;
                }

                // PlanStart: earliest if all set, else null
                DateTime? planStart = null;
                var allTasks = g.ToList();
                if (allTasks.All(t => t.PlanStartDate.HasValue))
                    planStart = allTasks.Min(t => t.PlanStartDate!.Value);

                // PlanFinish: latest if all set, else null
                DateTime? planFinish = null;
                if (allTasks.All(t => t.PlanFinishDate.HasValue))
                    planFinish = allTasks.Max(t => t.PlanFinishDate!.Value);

                var first = allTasks.OrderBy(t => t.SortOrder).First();
                return (progress, plannedProgress, planStart, planFinish, first.PlanDuration);
            });
    }

    public async Task<string?> GetSysParamValueAsync(string key)
    {
        var param = await _db.SysParams.FirstOrDefaultAsync(p => p.ParamKey == key);
        return param?.ParamValue;
    }

    public async Task<ProjectTask?> GetTaskByIdAsync(long taskId)
        => await _db.ProjectTasks.FindAsync(taskId);

    public async Task<ProjectTask> AddTaskAsync(ProjectTask task)
    {
        _db.ProjectTasks.Add(task);
        await _db.SaveChangesAsync();
        return task;
    }

    public async Task UpdateTaskAsync(ProjectTask task)
    {
        _db.ProjectTasks.Update(task);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteTaskAsync(long taskId)
    {
        var ids = new List<long> { taskId };
        await CollectChildIds(taskId, ids);
        var tasks = await _db.ProjectTasks.Where(t => ids.Contains(t.Id)).ToListAsync();
        _db.ProjectTasks.RemoveRange(tasks);
        await _db.SaveChangesAsync();
    }

    private async Task CollectChildIds(long parentId, List<long> ids)
    {
        var children = await _db.ProjectTasks.Where(t => t.ParentId == parentId).ToListAsync();
        foreach (var child in children)
        {
            ids.Add(child.Id);
            await CollectChildIds(child.Id, ids);
        }
    }

    // ────── 变更记录 ──────

    public async Task<List<ProjectChange>> GetChangesAsync(long projectId)
        => await _db.ProjectChanges.Where(c => c.ProjectId == projectId).OrderBy(c => c.CreatedAt).ToListAsync();

    public async Task<ProjectChange> AddChangeAsync(ProjectChange change)
    {
        _db.ProjectChanges.Add(change);
        await _db.SaveChangesAsync();
        return change;
    }

    public async Task UpdateChangeAsync(ProjectChange change)
    {
        _db.ProjectChanges.Update(change);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteChangeAsync(long changeId)
    {
        var c = await _db.ProjectChanges.FindAsync(changeId);
        if (c != null) { _db.ProjectChanges.Remove(c); await _db.SaveChangesAsync(); }
    }

    // ────── 财务信息 ──────

    public async Task<ProjectFinance?> GetFinanceAsync(long projectId)
    {
        var finance = await _db.ProjectFinances.FirstOrDefaultAsync(f => f.ProjectId == projectId);
        if (finance == null) return null;
        await _db.Entry(finance).Collection(f => f.PlanReceipts).LoadAsync();
        await _db.Entry(finance).Collection(f => f.Receipts).LoadAsync();
        await _db.Entry(finance).Collection(f => f.Invoices).LoadAsync();
        return finance;
    }

    public async Task SaveFinanceAsync(ProjectFinance finance)
    {
        var existing = await _db.ProjectFinances.FirstOrDefaultAsync(f => f.ProjectId == finance.ProjectId);
        if (existing == null)
        {
            _db.ProjectFinances.Add(finance);
        }
        else
        {
            existing.TaxContractAmount = finance.TaxContractAmount;
            existing.TaxRate = finance.TaxRate;
            existing.CurrencyType = finance.CurrencyType;
            existing.PaymentMethod = finance.PaymentMethod;
            existing.CurrencyAmount = finance.CurrencyAmount;
            existing.ContributionRate = finance.ContributionRate;
            existing.InvoiceRate = finance.InvoiceRate;
            existing.Remark = finance.Remark;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    private async Task<long> EnsureFinanceAsync(long projectId)
    {
        var finance = await _db.ProjectFinances.FirstOrDefaultAsync(f => f.ProjectId == projectId);
        if (finance == null)
        {
            finance = new ProjectFinance { ProjectId = projectId };
            _db.ProjectFinances.Add(finance);
            await _db.SaveChangesAsync();
        }
        return finance.Id;
    }

    public async Task SavePlanReceiptsAsync(long financeId, List<ProjectPlanReceipt> records)
    {
        var existing = await _db.ProjectPlanReceipts.Where(r => r.ProjectFinanceId == financeId).ToListAsync();
        _db.ProjectPlanReceipts.RemoveRange(existing);
        foreach (var r in records) { r.Id = 0; r.ProjectFinanceId = financeId; }
        _db.ProjectPlanReceipts.AddRange(records);
        await _db.SaveChangesAsync();
    }

    public async Task SaveReceiptsAsync(long financeId, List<ProjectReceipt> records)
    {
        var existing = await _db.ProjectReceipts.Where(r => r.ProjectFinanceId == financeId).ToListAsync();
        _db.ProjectReceipts.RemoveRange(existing);
        foreach (var r in records) { r.Id = 0; r.ProjectFinanceId = financeId; }
        _db.ProjectReceipts.AddRange(records);
        await _db.SaveChangesAsync();
    }

    public async Task SaveInvoicesAsync(long financeId, List<ProjectInvoice> records)
    {
        var existing = await _db.ProjectInvoices.Where(i => i.ProjectFinanceId == financeId).ToListAsync();
        _db.ProjectInvoices.RemoveRange(existing);
        foreach (var i in records) { i.Id = 0; i.ProjectFinanceId = financeId; }
        _db.ProjectInvoices.AddRange(records);
        await _db.SaveChangesAsync();
    }
}
