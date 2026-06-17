using MediatR;
using Microsoft.Extensions.Logging;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Projects.Commands;

// ────────── 任务 Input ──────────

public class TaskInput
{
    public long? ParentId { get; set; }
    public string TaskNo { get; set; } = "";
    public string WbsCode { get; set; } = "";
    public string TaskName { get; set; } = "";
    public int NodeType { get; set; } = 1;
    public string? TaskCategory { get; set; }
    public int SortOrder { get; set; }
    public int Status { get; set; } = 0;
    public int Priority { get; set; } = 3;
    public DateTime? PlanStartDate { get; set; }
    public DateTime? PlanFinishDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualFinishDate { get; set; }
    public int? PlanDuration { get; set; }
    public int? ActualDuration { get; set; }
    public int? ReferenceDuration { get; set; }
    public string? PreTaskCodes { get; set; }
    public int DeliverableCnt { get; set; }
    public decimal ProgressPct { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public long? DeptId { get; set; }
    public string? DeptName { get; set; }
    public long? MilestoneId { get; set; }
    public string? Remark { get; set; }
}

// ────────── CreateTask ──────────

public record CreateProjectTaskCommand(long ProjectId, TaskInput Input, long CreatedBy) : IRequest<long>;

public class CreateProjectTaskHandler : IRequestHandler<CreateProjectTaskCommand, long>
{
    private readonly IProjectRepository _repo;
    private readonly ILogger<CreateProjectTaskHandler> _logger;
    public CreateProjectTaskHandler(IProjectRepository repo, ILogger<CreateProjectTaskHandler> logger) { _repo = repo; _logger = logger; }

    public async Task<long> Handle(CreateProjectTaskCommand req, CancellationToken ct)
    {
        var rules = ParseCodeRule(await _repo.GetSysParamValueAsync("plan_code_rule"));
        var allTasks = await _repo.GetTasksAsync(req.ProjectId);

        // 里程碑校验：不允许有子节点，参考工期为0
        if (req.Input.NodeType == 2)
        {
            req.Input.ReferenceDuration = 0;
            req.Input.PlanDuration = 0;
        }

        var parentNo = req.Input.ParentId.HasValue
            ? allTasks.FirstOrDefault(t => t.Id == req.Input.ParentId.Value)?.TaskNo
            : null;
        var siblings = allTasks.Where(t => t.ParentId == req.Input.ParentId).ToList();
        var level = parentNo == null ? 0 : parentNo.Count(c => c == '.') + 1;
        var digits = level < rules.Length ? rules[level] : 2;
        var taskNo = parentNo == null
            ? (siblings.Count + 1).ToString().PadLeft(digits, '0')
            : $"{parentNo}.{siblings.Count + 1}";

        var task = new ProjectTask
        {
            ProjectId = req.ProjectId,
            ParentId = req.Input.ParentId,
            TaskNo = taskNo,
            WbsCode = req.Input.WbsCode,
            TaskName = req.Input.TaskName,
            NodeType = req.Input.NodeType,
            TaskCategory = req.Input.TaskCategory,
            SortOrder = req.Input.SortOrder,
            Status = req.Input.Status,
            Priority = req.Input.Priority,
            PlanStartDate = req.Input.PlanStartDate,
            PlanFinishDate = req.Input.PlanFinishDate,
            ActualStartDate = req.Input.ActualStartDate,
            ActualFinishDate = req.Input.ActualFinishDate,
            PlanDuration = req.Input.PlanDuration,
            ActualDuration = req.Input.ActualDuration,
            ReferenceDuration = req.Input.ReferenceDuration,
            PreTaskCodes = req.Input.PreTaskCodes,
            DeliverableCnt = req.Input.DeliverableCnt,
            ProgressPct = req.Input.ProgressPct,
            AssigneeId = req.Input.AssigneeId,
            AssigneeName = req.Input.AssigneeName,
            DeptId = req.Input.DeptId,
            DeptName = req.Input.DeptName,
            MilestoneId = req.Input.MilestoneId,
            Remark = req.Input.Remark
        };
        var result = await _repo.AddTaskAsync(task);
        return result.Id;
    }

    private int[] ParseCodeRule(string? ruleStr)
    {
        if (string.IsNullOrWhiteSpace(ruleStr)) return new[] { 3, 2, 2 };
        try { return ruleStr.Split(',').Select(s => int.Parse(s.Trim())).ToArray(); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析 plan_code_rule 失败: {RuleStr}，使用默认值 [3,2,2]", ruleStr);
            return new[] { 3, 2, 2 };
        }
    }
}

// ────────── UpdateTask ──────────

public record UpdateProjectTaskCommand(long TaskId, TaskInput Input) : IRequest;

public class UpdateProjectTaskHandler : IRequestHandler<UpdateProjectTaskCommand>
{
    private readonly IProjectRepository _repo;
    private readonly ILogger<UpdateProjectTaskHandler> _logger;
    public UpdateProjectTaskHandler(IProjectRepository repo, ILogger<UpdateProjectTaskHandler> logger) { _repo = repo; _logger = logger; }

    public async Task Handle(UpdateProjectTaskCommand req, CancellationToken ct)
    {
        var existing = await _repo.GetTaskByIdAsync(req.TaskId)
            ?? throw new InvalidOperationException("任务不存在");
        // 前置任务引用使用 taskId，编号变更不再需要同步 preTaskCodes

        // 里程碑校验：参考工期/计划工期为0
        if (req.Input.NodeType == 2)
        {
            req.Input.ReferenceDuration = 0;
            req.Input.PlanDuration = 0;
        }

        // 非未激活状态禁止修改计划开始/计划完成
        var project = await _repo.GetByIdAsync(existing.ProjectId);
        if (project?.Status != 0)
        {
            req.Input.PlanStartDate = existing.PlanStartDate;
            req.Input.PlanFinishDate = existing.PlanFinishDate;
        }

        // 直接使用前端传来的 TaskNo（前端负责维护正确的编号）
        // 不在后端重新计算，避免多任务并发保存时序号冲突
        existing.ParentId = req.Input.ParentId;
        existing.TaskNo = string.IsNullOrWhiteSpace(req.Input.TaskNo) ? existing.TaskNo : req.Input.TaskNo;
        existing.WbsCode = req.Input.WbsCode;
        existing.TaskName = req.Input.TaskName;
        existing.NodeType = req.Input.NodeType;
        existing.TaskCategory = req.Input.TaskCategory;
        existing.SortOrder = req.Input.SortOrder;
        existing.Status = req.Input.Status;
        existing.Priority = req.Input.Priority;
        existing.PlanStartDate = req.Input.PlanStartDate;
        existing.PlanFinishDate = req.Input.PlanFinishDate;
        existing.ActualStartDate = req.Input.ActualStartDate;
        existing.ActualFinishDate = req.Input.ActualFinishDate;
        existing.PlanDuration = req.Input.PlanDuration;
        existing.ActualDuration = req.Input.ActualDuration;
        existing.ReferenceDuration = req.Input.ReferenceDuration;
        existing.PreTaskCodes = req.Input.PreTaskCodes;
        existing.DeliverableCnt = req.Input.DeliverableCnt;
        existing.ProgressPct = req.Input.ProgressPct;
        // 部门信息始终从 input 取值（不依赖责任人是否为空）
        existing.DeptId = req.Input.DeptId;
        existing.DeptName = req.Input.DeptName;

        // 责任人清空时仅清空责任人
        if (string.IsNullOrWhiteSpace(req.Input.AssigneeName))
        {
            existing.AssigneeId = null;
            existing.AssigneeName = null;
        }
        else
        {
            existing.AssigneeId = req.Input.AssigneeId;
            existing.AssigneeName = req.Input.AssigneeName;
        }
        existing.MilestoneId = req.Input.MilestoneId;
        existing.Remark = req.Input.Remark;

        await _repo.UpdateTaskAsync(existing);

    }

    private int[] ParseCodeRule(string? ruleStr)
    {
        if (string.IsNullOrWhiteSpace(ruleStr)) return new[] { 3, 2, 2 };
        try { return ruleStr.Split(',').Select(s => int.Parse(s.Trim())).ToArray(); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析 plan_code_rule 失败: {RuleStr}，使用默认值 [3,2,2]", ruleStr);
            return new[] { 3, 2, 2 };
        }
    }

}

// ────────── DeleteTask ──────────

public record DeleteProjectTaskCommand(long TaskId) : IRequest;

public class DeleteProjectTaskHandler : IRequestHandler<DeleteProjectTaskCommand>
{
    private readonly IProjectRepository _repo;
    public DeleteProjectTaskHandler(IProjectRepository repo) => _repo = repo;
    public async Task Handle(DeleteProjectTaskCommand req, CancellationToken ct)
    {
        var task = await _repo.GetTaskByIdAsync(req.TaskId);
        var projectId = task?.ProjectId;
        await _repo.DeleteTaskAsync(req.TaskId);
        if (projectId == null) return;

        // 收集所有被删的 ID（含子任务）
        var allTasks = await _repo.GetTasksAsync(projectId.Value);
        var deletedIds = new HashSet<long> { req.TaskId };
        void CollectDescendants(long pid)
        {
            foreach (var t in allTasks.Where(t => t.ParentId == pid))
                if (deletedIds.Add(t.Id)) CollectDescendants(t.Id);
        }
        CollectDescendants(req.TaskId);

        // 从其他任务的 preTaskCodes 中移除对被删任务的引用
        foreach (var t in allTasks)
        {
            if (string.IsNullOrEmpty(t.PreTaskCodes)) continue;
            var segments = t.PreTaskCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var filtered = segments.Where(s =>
            {
                var parenIdx = s.IndexOf('(');
                var idPart = parenIdx > 0 ? s[..parenIdx].Trim() : s.Trim();
                return !(long.TryParse(idPart, out var id) && deletedIds.Contains(id));
            }).ToArray();
            var newCodes = string.Join(",", filtered);
            if (newCodes != t.PreTaskCodes)
            {
                t.PreTaskCodes = newCodes;
                await _repo.UpdateTaskAsync(t);
            }
        }
    }
}

// ────────── ChangeInput ──────────

public class ChangeInput
{
    public string? ChangeType { get; set; }
    public string? ChangeParty { get; set; }
    public string? ChangeContent { get; set; }
    public string? AttachmentUrl { get; set; }
    public long? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public DateTime? EffectEndDate { get; set; }
}

// ────────── CreateChange ──────────

public record CreateProjectChangeCommand(long ProjectId, ChangeInput Input, long CreatedBy, string CreatedByName) : IRequest<long>;

public class CreateProjectChangeHandler : IRequestHandler<CreateProjectChangeCommand, long>
{
    private readonly IProjectRepository _repo;
    public CreateProjectChangeHandler(IProjectRepository repo) => _repo = repo;

    public async Task<long> Handle(CreateProjectChangeCommand req, CancellationToken ct)
    {
        var change = new ProjectChange
        {
            ProjectId = req.ProjectId,
            ChangeType = req.Input.ChangeType,
            ChangeParty = req.Input.ChangeParty,
            ChangeContent = req.Input.ChangeContent,
            AttachmentUrl = req.Input.AttachmentUrl,
            ApproverId = req.Input.ApproverId,
            ApproverName = req.Input.ApproverName,
            EffectEndDate = req.Input.EffectEndDate,
            CreatedBy = req.CreatedBy,
            CreatedByName = req.CreatedByName,
            CreatedAt = DateTime.UtcNow
        };
        var result = await _repo.AddChangeAsync(change);
        return result.Id;
    }
}

// ────────── UpdateChange ──────────

public record UpdateProjectChangeCommand(long ChangeId, ChangeInput Input) : IRequest;

public class UpdateProjectChangeHandler : IRequestHandler<UpdateProjectChangeCommand>
{
    private readonly IProjectRepository _repo;
    public UpdateProjectChangeHandler(IProjectRepository repo) => _repo = repo;

    public async Task Handle(UpdateProjectChangeCommand req, CancellationToken ct)
    {
        var change = new ProjectChange
        {
            Id = req.ChangeId,
            ChangeType = req.Input.ChangeType,
            ChangeParty = req.Input.ChangeParty,
            ChangeContent = req.Input.ChangeContent,
            AttachmentUrl = req.Input.AttachmentUrl,
            ApproverId = req.Input.ApproverId,
            ApproverName = req.Input.ApproverName,
            EffectEndDate = req.Input.EffectEndDate
        };
        await _repo.UpdateChangeAsync(change);
    }
}

// ────────── DeleteChange ──────────

public record DeleteProjectChangeCommand(long ChangeId) : IRequest;

public class DeleteProjectChangeHandler : IRequestHandler<DeleteProjectChangeCommand>
{
    private readonly IProjectRepository _repo;
    public DeleteProjectChangeHandler(IProjectRepository repo) => _repo = repo;
    public async Task Handle(DeleteProjectChangeCommand req, CancellationToken ct) => await _repo.DeleteChangeAsync(req.ChangeId);
}

// ────────── Finance Inputs ──────────

public class FinanceInput
{
    public decimal? TaxContractAmount { get; set; }
    public decimal? TaxRate { get; set; }
    public string? CurrencyType { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal? CurrencyAmount { get; set; }
    public decimal? ContributionRate { get; set; }
    public decimal? InvoiceRate { get; set; }
    public string? Remark { get; set; }
}

public class PlanReceiptInput { public int SortOrder { get; set; } public decimal PlanAmount { get; set; } public string? ReceiptType { get; set; } public DateTime? PlanDate { get; set; } public string? Remark { get; set; } }
public class ReceiptInput { public int SortOrder { get; set; } public decimal ActualAmount { get; set; } public string? ReceiptType { get; set; } public DateTime? ReceiptTime { get; set; } public string? Remark { get; set; } }
public class InvoiceInput { public int SortOrder { get; set; } public decimal InvoiceAmount { get; set; } public decimal? InvoiceRate { get; set; } public DateTime? InvoiceTime { get; set; } public string? InvoiceNo { get; set; } public string? Remark { get; set; } }

// ────────── SaveFinance ──────────

public record SaveProjectFinanceCommand(long ProjectId, FinanceInput Input) : IRequest;

public class SaveProjectFinanceHandler : IRequestHandler<SaveProjectFinanceCommand>
{
    private readonly IProjectRepository _repo;
    public SaveProjectFinanceHandler(IProjectRepository repo) => _repo = repo;

    public async Task Handle(SaveProjectFinanceCommand req, CancellationToken ct)
    {
        var finance = new ProjectFinance
        {
            ProjectId = req.ProjectId,
            TaxContractAmount = req.Input.TaxContractAmount,
            TaxRate = req.Input.TaxRate,
            CurrencyType = req.Input.CurrencyType,
            PaymentMethod = req.Input.PaymentMethod,
            CurrencyAmount = req.Input.CurrencyAmount,
            ContributionRate = req.Input.ContributionRate,
            InvoiceRate = req.Input.InvoiceRate,
            Remark = req.Input.Remark
        };
        await _repo.SaveFinanceAsync(finance);
    }
}

// ────────── SavePlanReceipts ──────────

public record SavePlanReceiptsCommand(long ProjectId, List<PlanReceiptInput> Records) : IRequest;

public class SavePlanReceiptsHandler : IRequestHandler<SavePlanReceiptsCommand>
{
    private readonly IProjectRepository _repo;
    public SavePlanReceiptsHandler(IProjectRepository repo) => _repo = repo;

    public async Task Handle(SavePlanReceiptsCommand req, CancellationToken ct)
    {
        var financeId = await EnsureFinanceId(req.ProjectId);
        var records = req.Records.Select((r, i) => new ProjectPlanReceipt
        {
            ProjectFinanceId = financeId,
            SortOrder = r.SortOrder > 0 ? r.SortOrder : i + 1,
            PlanAmount = r.PlanAmount,
            ReceiptType = r.ReceiptType,
            PlanDate = r.PlanDate,
            Remark = r.Remark
        }).ToList();
        await _repo.SavePlanReceiptsAsync(financeId, records);
    }

    private async Task<long> EnsureFinanceId(long projectId)
    {
        var finance = await _repo.GetFinanceAsync(projectId);
        if (finance != null) return finance.Id;
        var newFinance = new ProjectFinance { ProjectId = projectId };
        await _repo.SaveFinanceAsync(newFinance);
        return (await _repo.GetFinanceAsync(projectId))!.Id;
    }
}

// ────────── SaveReceipts ──────────

public record SaveReceiptsCommand(long ProjectId, List<ReceiptInput> Records) : IRequest;

public class SaveReceiptsHandler : IRequestHandler<SaveReceiptsCommand>
{
    private readonly IProjectRepository _repo;
    public SaveReceiptsHandler(IProjectRepository repo) => _repo = repo;

    public async Task Handle(SaveReceiptsCommand req, CancellationToken ct)
    {
        var finance = await _repo.GetFinanceAsync(req.ProjectId);
        if (finance == null) { await _repo.SaveFinanceAsync(new ProjectFinance { ProjectId = req.ProjectId }); finance = await _repo.GetFinanceAsync(req.ProjectId); }
        var records = req.Records.Select((r, i) => new ProjectReceipt
        {
            ProjectFinanceId = finance!.Id,
            SortOrder = r.SortOrder > 0 ? r.SortOrder : i + 1,
            ActualAmount = r.ActualAmount,
            ReceiptType = r.ReceiptType,
            ReceiptTime = r.ReceiptTime,
            Remark = r.Remark
        }).ToList();
        await _repo.SaveReceiptsAsync(finance!.Id, records);
    }
}

// ────────── SaveInvoices ──────────

public record SaveInvoicesCommand(long ProjectId, List<InvoiceInput> Records) : IRequest;

public class SaveInvoicesHandler : IRequestHandler<SaveInvoicesCommand>
{
    private readonly IProjectRepository _repo;
    public SaveInvoicesHandler(IProjectRepository repo) => _repo = repo;

    public async Task Handle(SaveInvoicesCommand req, CancellationToken ct)
    {
        var finance = await _repo.GetFinanceAsync(req.ProjectId);
        if (finance == null) { await _repo.SaveFinanceAsync(new ProjectFinance { ProjectId = req.ProjectId }); finance = await _repo.GetFinanceAsync(req.ProjectId); }
        var records = req.Records.Select((r, i) => new ProjectInvoice
        {
            ProjectFinanceId = finance!.Id,
            SortOrder = r.SortOrder > 0 ? r.SortOrder : i + 1,
            InvoiceAmount = r.InvoiceAmount,
            InvoiceRate = r.InvoiceRate,
            InvoiceTime = r.InvoiceTime,
            InvoiceNo = r.InvoiceNo,
            Remark = r.Remark
        }).ToList();
        await _repo.SaveInvoicesAsync(finance!.Id, records);
    }
}

// ────────── CreateTasksFromTemplate ──────────

public record CreateTasksFromTemplateCommand(long ProjectId, long TemplateId) : IRequest<int>;

public class CreateTasksFromTemplateHandler : IRequestHandler<CreateTasksFromTemplateCommand, int>
{
    private readonly ITemplateRepository _templateRepo;
    private readonly IProjectRepository _projectRepo;

    public CreateTasksFromTemplateHandler(ITemplateRepository templateRepo, IProjectRepository projectRepo)
    {
        _templateRepo = templateRepo;
        _projectRepo = projectRepo;
    }

    public async Task<int> Handle(CreateTasksFromTemplateCommand req, CancellationToken ct)
    {
        var template = await _templateRepo.GetByIdAsync(req.TemplateId)
            ?? throw new InvalidOperationException("模板不存在");

        // 清空现有任务
        var existingTasks = await _projectRepo.GetTasksAsync(req.ProjectId);
        foreach (var task in existingTasks.Where(t => t.ParentId == null).ToList())
        {
            await _projectRepo.DeleteTaskAsync(task.Id);
        }

        var allNodes = template.PlanNodes.ToList();
        if (allNodes.Count == 0) return 0;

        // 加载项目成员列表，构建按部门查找表（用于节点无责任人时自动匹配）
        var project = await _projectRepo.GetByIdAsync(req.ProjectId);
        var membersByDeptId = (project?.Members ?? new())
            .Where(m => m.DeptId.HasValue)
            .GroupBy(m => m.DeptId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.SortOrder).First());
        var membersByDeptName = (project?.Members ?? new())
            .Where(m => !string.IsNullOrWhiteSpace(m.DeptName))
            .GroupBy(m => m.DeptName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.SortOrder).First(), StringComparer.OrdinalIgnoreCase);

        ProjectMember? FindAutoMember(long? deptId, string? deptName)
        {
            if (deptId.HasValue && membersByDeptId.TryGetValue(deptId.Value, out var m))
                return m;
            if (!string.IsNullOrWhiteSpace(deptName) && membersByDeptName.TryGetValue(deptName, out var m2))
                return m2;
            return null;
        }

        var nodeIds = allNodes.Select(n => n.Id).ToList();
        var dependencies = await _templateRepo.GetDependenciesByNodeIdsAsync(nodeIds);
        var depsByNodeId = dependencies.ToLookup(d => d.PlanNodeId);
        var nodeMap = allNodes.ToDictionary(n => n.Id);

        // DFS 扁平化（父节点在前，保证子节点创建时父节点 ID 已就绪）
        var flat = FlattenTree(allNodes, null);

        // 第一轮：创建所有任务
        var codeToTask = new Dictionary<string, (long Id, string TaskNo)>();
        foreach (var node in flat)
        {
            // 节点无显式责任人时，从项目成员中按责任部门自动匹配（取第一个）
            var autoMember = node.AssigneeId.HasValue ? null : FindAutoMember(node.DeptId, node.DeptName);

            var task = new ProjectTask
            {
                ProjectId = req.ProjectId,
                TaskNo = node.NodeCode,
                TaskName = node.NodeName,
                NodeType = node.NodeType,
                SortOrder = node.SortOrder,
                Status = 0,
                Priority = 3,
                PlanDuration = node.NodeType == 2 ? 0 : node.StdDuration,
                ReferenceDuration = node.NodeType == 2 ? 0 : node.StdDuration,
                DeliverableCnt = node.DeliverableCnt,
                ProgressPct = 0,
                AssigneeId = autoMember?.MemberId ?? node.AssigneeId,
                AssigneeName = !string.IsNullOrWhiteSpace(node.AssigneeName) ? node.AssigneeName : autoMember?.MemberName,
                DeptId = node.DeptId,
                DeptName = node.DeptName,
                Remark = node.Remark,
                TaskCategory = node.TaskCategory
            };
            var saved = await _projectRepo.AddTaskAsync(task);
            codeToTask[node.NodeCode] = (saved.Id, saved.TaskNo);
        }

        // 第二轮：更新 ParentId 和 PreTaskCodes
        foreach (var node in flat)
        {
            if (!codeToTask.TryGetValue(node.NodeCode, out var taskInfo)) continue;
            var task = await _projectRepo.GetTaskByIdAsync(taskInfo.Id);
            if (task == null) continue;

            var updated = false;

            // ParentId
            if (node.ParentId.HasValue && nodeMap.TryGetValue(node.ParentId.Value, out var parentNode))
            {
                if (codeToTask.TryGetValue(parentNode.NodeCode, out var parentInfo))
                {
                    task.ParentId = parentInfo.Id;
                    updated = true;
                }
            }

            // PreTaskCodes
            var nodeDeps = depsByNodeId[node.Id];
            var predParts = new List<string>();
            foreach (var dep in nodeDeps)
            {
                if (nodeMap.TryGetValue(dep.PredecessorId, out var predNode))
                {
                    if (codeToTask.TryGetValue(predNode.NodeCode, out var predInfo))
                    {
                        var sign = dep.LagDays > 0 ? "+" : "";
                        var lag = dep.LagDays != 0 ? $"{sign}{dep.LagDays}" : "";
                        predParts.Add($"{predInfo.Id}({dep.DependencyType}{lag})");
                    }
                }
            }
            if (predParts.Count > 0)
            {
                task.PreTaskCodes = string.Join(",", predParts);
                updated = true;
            }

            if (updated)
                await _projectRepo.UpdateTaskAsync(task);
        }

        return flat.Count;
    }

    private static List<PlanNode> FlattenTree(IEnumerable<PlanNode> allNodes, long? parentId)
    {
        var result = new List<PlanNode>();
        var children = allNodes.Where(n => n.ParentId == parentId).OrderBy(n => n.SortOrder).ToList();
        foreach (var child in children)
        {
            result.Add(child);
            result.AddRange(FlattenTree(allNodes, child.Id));
        }
        return result;
    }
}
