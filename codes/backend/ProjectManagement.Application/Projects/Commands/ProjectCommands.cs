using MediatR;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Projects.Commands;

// ────────── 输入 DTO（供 Command 及 Controller 共用）──────────

public class ProductInput
{
    public int SortOrder { get; set; }
    public string? ProductType { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTime? PlannedDelivery { get; set; }
    public string? Remark { get; set; }
}

public class MemberInput
{
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
}

public class MilestoneInput
{
    public int SortOrder { get; set; }
    public string MilestoneCode { get; set; } = "";
    public string MilestoneName { get; set; } = "";
    public int Status { get; set; }
    public DateTime? PlanFinishDate { get; set; }
    public DateTime? ActualFinishDate { get; set; }
    public string? NodeReference { get; set; }
    public string? Remark { get; set; }
}

// ────────── CreateProject ──────────

public record CreateProjectCommand(
    string ProjectCode,
    string ProjectName,
    string? ProjectType,
    string? ContractCode,
    string? EngineeringCenter,
    string? CategoryCode,
    string? CustomerName,
    long? RegionalManagerId,
    string? RegionalManagerName,
    string? CustomerContactPhone,
    string? CustomerContactEmail,
    long? SalesManagerId,
    string? SalesManagerName,
    long? PreSalesManagerId,
    string? PreSalesManagerName,
    string? SalesRegion,
    long? ProjectManagerId,
    string? ProjectManagerName,
    string? PmCenter,
    string? OwnerContactPhone,
    string? BusinessContactEmail,
    DateTime? PlanStartDate,
    DateTime? RequiredDelivery,
    DateTime? AcceptedDelivery,
    string? DeliveryLocation,
    string? FinalCustomer,
    string? ProjectScope,
    string? SpecialTerms,
    string? Remark,
    string? ProgressDesc,
    long CreatedBy,
    string CreatedByName,
    List<ProductInput> Products,
    List<long>? UploadedFileIds
) : IRequest<long>;

public class CreateProjectHandler : IRequestHandler<CreateProjectCommand, long>
{
    private readonly IProjectRepository _repo;
    private readonly IUserRepository _userRepo;

    public CreateProjectHandler(IProjectRepository repo, IUserRepository userRepo)
    {
        _repo = repo;
        _userRepo = userRepo;
    }

    public async Task<long> Handle(CreateProjectCommand req, CancellationToken ct)
    {
        if (await _repo.ProjectCodeExistsAsync(req.ProjectCode))
            throw new InvalidOperationException("项目编号已存在");

        var project = new Project
        {
            ProjectCode = req.ProjectCode,
            ProjectName = req.ProjectName,
            ProjectType = req.ProjectType,
            ContractCode = req.ContractCode,
            Status = 0,
            EngineeringCenter = req.EngineeringCenter,
            CategoryCode = req.CategoryCode,
            CustomerName = req.CustomerName,
            RegionalManagerId = req.RegionalManagerId,
            RegionalManagerName = req.RegionalManagerName,
            CustomerContactPhone = req.CustomerContactPhone,
            CustomerContactEmail = req.CustomerContactEmail,
            SalesManagerId = req.SalesManagerId,
            SalesManagerName = req.SalesManagerName,
            PreSalesManagerId = req.PreSalesManagerId,
            PreSalesManagerName = req.PreSalesManagerName,
            SalesRegion = req.SalesRegion,
            ProjectManagerId = req.ProjectManagerId,
            ProjectManagerName = req.ProjectManagerName,
            PmCenter = req.PmCenter,
            OwnerContactPhone = req.OwnerContactPhone,
            BusinessContactEmail = req.BusinessContactEmail,
            PlanStartDate = req.PlanStartDate,
            RequiredDelivery = req.RequiredDelivery,
            AcceptedDelivery = req.AcceptedDelivery,
            DeliveryLocation = req.DeliveryLocation,
            FinalCustomer = req.FinalCustomer,
            ProjectScope = req.ProjectScope,
            SpecialTerms = req.SpecialTerms,
            Remark = req.Remark,
            ProgressDesc = req.ProgressDesc,
            CreatedBy = req.CreatedBy,
            CreatedByName = req.CreatedByName,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _repo.AddAsync(project);

        // 创建人自动加入项目成员
        var creator = await _userRepo.GetByIdAsync(req.CreatedBy);
        if (creator != null)
        {
            await _repo.SaveMembersAsync(result.Id, new List<ProjectMember>
            {
                new ProjectMember
                {
                    ProjectId = result.Id,
                    SortOrder = 1,
                    MemberId = creator.Id,
                    MemberName = creator.RealName,
                    DeptId = creator.DepartmentId,
                    DeptName = creator.Department?.Name
                }
            });
        }

        if (req.Products.Count > 0)
        {
            var products = req.Products.Select((p, i) => new ProjectProduct
            {
                ProjectId = result.Id,
                SortOrder = p.SortOrder > 0 ? p.SortOrder : i + 1,
                ProductType = p.ProductType,
                Quantity = p.Quantity,
                PlannedDelivery = p.PlannedDelivery,
                Remark = p.Remark
            }).ToList();
            await _repo.SaveProductsAsync(result.Id, products);
        }

        return result.Id;
    }
}

// ────────── UpdateProject ──────────

public record UpdateProjectCommand(
    long Id,
    string ProjectName,
    string? ProjectType,
    string? ContractCode,
    string? EngineeringCenter,
    string? CategoryCode,
    string? CustomerName,
    long? RegionalManagerId,
    string? RegionalManagerName,
    string? CustomerContactPhone,
    string? CustomerContactEmail,
    long? SalesManagerId,
    string? SalesManagerName,
    long? PreSalesManagerId,
    string? PreSalesManagerName,
    string? SalesRegion,
    long? ProjectManagerId,
    string? ProjectManagerName,
    string? PmCenter,
    string? OwnerContactPhone,
    string? BusinessContactEmail,
    DateTime? PlanStartDate,
    DateTime? RequiredDelivery,
    DateTime? AcceptedDelivery,
    DateTime? ActualFinishDate,
    string? DeliveryLocation,
    string? FinalCustomer,
    string? ProjectScope,
    string? SpecialTerms,
    string? Remark,
    string? QualityStrategy,
    string? ProjectDelivery,
    string? ReportContent,
    string? RiskStatus,
    DateTime? CurrentPhaseDate,
    string? NextStatus,
    string? ProgressDesc,
    long UpdatedBy,
    List<ProductInput> Products
) : IRequest;

public class UpdateProjectHandler : IRequestHandler<UpdateProjectCommand>
{
    private readonly IProjectRepository _repo;

    public UpdateProjectHandler(IProjectRepository repo) => _repo = repo;

    public async Task Handle(UpdateProjectCommand req, CancellationToken ct)
    {
        var project = await _repo.GetByIdAsync(req.Id)
            ?? throw new InvalidOperationException("项目不存在");

        project.ProjectName = req.ProjectName;
        project.ProjectType = req.ProjectType;
        project.ContractCode = req.ContractCode;
        project.EngineeringCenter = req.EngineeringCenter;
        project.CategoryCode = req.CategoryCode;
        project.CustomerName = req.CustomerName;
        project.RegionalManagerId = req.RegionalManagerId;
        project.RegionalManagerName = req.RegionalManagerName;
        project.CustomerContactPhone = req.CustomerContactPhone;
        project.CustomerContactEmail = req.CustomerContactEmail;
        project.SalesManagerId = req.SalesManagerId;
        project.SalesManagerName = req.SalesManagerName;
        project.PreSalesManagerId = req.PreSalesManagerId;
        project.PreSalesManagerName = req.PreSalesManagerName;
        project.SalesRegion = req.SalesRegion;
        project.ProjectManagerId = req.ProjectManagerId;
        project.ProjectManagerName = req.ProjectManagerName;
        project.PmCenter = req.PmCenter;
        project.OwnerContactPhone = req.OwnerContactPhone;
        project.BusinessContactEmail = req.BusinessContactEmail;
        project.PlanStartDate = req.PlanStartDate;
        project.RequiredDelivery = req.RequiredDelivery;
        project.AcceptedDelivery = req.AcceptedDelivery;
        project.ActualFinishDate = req.ActualFinishDate;
        project.DeliveryLocation = req.DeliveryLocation;
        project.FinalCustomer = req.FinalCustomer;
        project.ProjectScope = req.ProjectScope;
        project.SpecialTerms = req.SpecialTerms;
        project.Remark = req.Remark;
        project.QualityStrategy = req.QualityStrategy;
        project.ProjectDelivery = req.ProjectDelivery;
        project.ReportContent = req.ReportContent;
        project.RiskStatus = req.RiskStatus;
        project.CurrentPhaseDate = req.CurrentPhaseDate;
        project.NextStatus = req.NextStatus;
        project.ProgressDesc = req.ProgressDesc;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = req.UpdatedBy;

        await _repo.UpdateAsync(project);

        var products = req.Products.Select((p, i) => new ProjectProduct
        {
            ProjectId = req.Id,
            SortOrder = p.SortOrder > 0 ? p.SortOrder : i + 1,
            ProductType = p.ProductType,
            Quantity = p.Quantity,
            PlannedDelivery = p.PlannedDelivery,
            Remark = p.Remark
        }).ToList();
        await _repo.SaveProductsAsync(req.Id, products);
    }
}

// ────────── ChangeStatus ──────────

public record ChangeProjectStatusCommand(long Id, int NewStatus, long UpdatedBy) : IRequest;

public class ChangeProjectStatusHandler : IRequestHandler<ChangeProjectStatusCommand>
{
    private readonly IProjectRepository _repo;

    public ChangeProjectStatusHandler(IProjectRepository repo) => _repo = repo;

    public async Task Handle(ChangeProjectStatusCommand req, CancellationToken ct)
    {
        var project = await _repo.GetByIdAsync(req.Id)
            ?? throw new InvalidOperationException("项目不存在");

        project.Status = req.NewStatus;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = req.UpdatedBy;

        await _repo.UpdateAsync(project);
    }
}

// ────────── SaveMembers ──────────

public record SaveProjectMembersCommand(long ProjectId, List<MemberInput> Members) : IRequest;

public class SaveProjectMembersHandler : IRequestHandler<SaveProjectMembersCommand>
{
    private readonly IProjectRepository _repo;

    public SaveProjectMembersHandler(IProjectRepository repo) => _repo = repo;

    public async Task Handle(SaveProjectMembersCommand req, CancellationToken ct)
    {
        var members = req.Members.Select((m, i) => new ProjectMember
        {
            ProjectId = req.ProjectId,
            SortOrder = m.SortOrder > 0 ? m.SortOrder : i + 1,
            RoleId = m.RoleId,
            RoleName = m.RoleName,
            MemberId = m.MemberId,
            MemberName = m.MemberName,
            DeptId = m.DeptId,
            DeptName = m.DeptName,
            FunctionId = m.FunctionId,
            FunctionName = m.FunctionName,
            Remark = m.Remark
        }).ToList();

        await _repo.SaveMembersAsync(req.ProjectId, members);
    }
}

// ────────── CopyProject ──────────

public record CopyProjectCommand(
    long SourceProjectId,
    string NewProjectCode,
    long CreatedBy,
    string CreatedByName
) : IRequest<long>;

public class CopyProjectHandler : IRequestHandler<CopyProjectCommand, long>
{
    private readonly IProjectRepository _repo;

    public CopyProjectHandler(IProjectRepository repo) => _repo = repo;

    public async Task<long> Handle(CopyProjectCommand req, CancellationToken ct)
    {
        var source = await _repo.GetByIdAsync(req.SourceProjectId)
            ?? throw new InvalidOperationException("源项目不存在");

        var tasks = await _repo.GetTasksAsync(req.SourceProjectId);
        var changes = await _repo.GetChangesAsync(req.SourceProjectId);
        var finance = await _repo.GetFinanceAsync(req.SourceProjectId);

        // 创建新项目
        var project = new Project
        {
            ProjectCode = req.NewProjectCode,
            ProjectName = source.ProjectName + " - 副本",
            ProjectType = source.ProjectType,
            ContractCode = source.ContractCode,
            Status = 0,
            EngineeringCenter = source.EngineeringCenter,
            CategoryCode = source.CategoryCode,
            CustomerName = source.CustomerName,
            RegionalManagerId = source.RegionalManagerId,
            RegionalManagerName = source.RegionalManagerName,
            CustomerContactPhone = source.CustomerContactPhone,
            CustomerContactEmail = source.CustomerContactEmail,
            SalesManagerId = source.SalesManagerId,
            SalesManagerName = source.SalesManagerName,
            PreSalesManagerId = source.PreSalesManagerId,
            PreSalesManagerName = source.PreSalesManagerName,
            SalesRegion = source.SalesRegion,
            ProjectManagerId = source.ProjectManagerId,
            ProjectManagerName = source.ProjectManagerName,
            PmCenter = source.PmCenter,
            OwnerContactPhone = source.OwnerContactPhone,
            BusinessContactEmail = source.BusinessContactEmail,
            PlanStartDate = source.PlanStartDate,
            RequiredDelivery = source.RequiredDelivery,
            AcceptedDelivery = source.AcceptedDelivery,
            DeliveryLocation = source.DeliveryLocation,
            FinalCustomer = source.FinalCustomer,
            ProjectScope = source.ProjectScope,
            SpecialTerms = source.SpecialTerms,
            Remark = source.Remark,
            ProgressDesc = source.ProgressDesc,
            CreatedBy = req.CreatedBy,
            CreatedByName = req.CreatedByName,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _repo.AddAsync(project);
        var newId = result.Id;

        // 复制成员
        if (source.Members.Count > 0)
        {
            var members = source.Members.Select(m => new ProjectMember
            {
                ProjectId = newId,
                SortOrder = m.SortOrder,
                RoleId = m.RoleId,
                RoleName = m.RoleName,
                MemberId = m.MemberId,
                MemberName = m.MemberName,
                DeptId = m.DeptId,
                DeptName = m.DeptName,
                FunctionId = m.FunctionId,
                FunctionName = m.FunctionName,
                Remark = m.Remark
            }).ToList();
            await _repo.SaveMembersAsync(newId, members);
        }

        // 复制产品
        if (source.Products.Count > 0)
        {
            var products = source.Products.Select(p => new ProjectProduct
            {
                ProjectId = newId,
                SortOrder = p.SortOrder,
                ProductType = p.ProductType,
                Quantity = p.Quantity,
                PlannedDelivery = p.PlannedDelivery,
                Remark = p.Remark
            }).ToList();
            await _repo.SaveProductsAsync(newId, products);
        }

        // 复制里程碑（SaveMilestonesAsync 后 EF Core 会回填 Id）
        var milestoneIdMap = new Dictionary<long, long>();
        if (source.Milestones.Count > 0)
        {
            var milestones = source.Milestones.Select(m => new ProjectMilestone
            {
                ProjectId = newId,
                SortOrder = m.SortOrder,
                MilestoneCode = m.MilestoneCode,
                MilestoneName = m.MilestoneName,
                Status = m.Status,
                PlanFinishDate = m.PlanFinishDate,
                ActualFinishDate = m.ActualFinishDate,
                NodeReference = m.NodeReference,
                Remark = m.Remark
            }).ToList();
            await _repo.SaveMilestonesAsync(newId, milestones);
            for (int i = 0; i < source.Milestones.Count; i++)
                milestoneIdMap[source.Milestones[i].Id] = milestones[i].Id;
        }

        // 复制任务（DFS 保证父节点先于子节点创建）
        var taskIdMap = new Dictionary<long, long>();
        if (tasks.Count > 0)
        {
            var childrenMap = tasks
                .Where(t => t.ParentId.HasValue)
                .GroupBy(t => t.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.SortOrder).ThenBy(t => t.TaskNo).ToList());

            var flatOrder = new List<ProjectTask>();
            void Dfs(ProjectTask task)
            {
                flatOrder.Add(task);
                if (childrenMap.TryGetValue(task.Id, out var children))
                    foreach (var child in children) Dfs(child);
            }
            foreach (var root in tasks.Where(t => !t.ParentId.HasValue).OrderBy(t => t.SortOrder).ThenBy(t => t.TaskNo))
                Dfs(root);

            foreach (var task in flatOrder)
            {
                var newTask = new ProjectTask
                {
                    ProjectId = newId,
                    TaskNo = task.TaskNo,
                    WbsCode = task.WbsCode,
                    TaskName = task.TaskName,
                    NodeType = task.NodeType,
                    TaskCategory = task.TaskCategory,
                    SortOrder = task.SortOrder,
                    Status = 0,
                    Priority = task.Priority,
                    PlanStartDate = task.PlanStartDate,
                    PlanFinishDate = task.PlanFinishDate,
                    ReferenceDuration = task.ReferenceDuration,
                    PreTaskCodes = task.PreTaskCodes,
                    DeliverableCnt = task.DeliverableCnt,
                    ProgressPct = 0,
                    AssigneeId = task.AssigneeId,
                    AssigneeName = task.AssigneeName,
                    DeptId = task.DeptId,
                    DeptName = task.DeptName,
                    MilestoneId = task.MilestoneId.HasValue && milestoneIdMap.TryGetValue(task.MilestoneId.Value, out var newMsId) ? newMsId : task.MilestoneId,
                    Remark = task.Remark,
                    ParentId = task.ParentId.HasValue && taskIdMap.TryGetValue(task.ParentId.Value, out var newPid) ? newPid : null
                };
                var saved = await _repo.AddTaskAsync(newTask);
                taskIdMap[task.Id] = saved.Id;
            }
        }

        // 第二轮：重映射 PreTaskCodes 中的旧 ID → 新 ID
        if (taskIdMap.Count > 0)
        {
            var clonedTasks = await _repo.GetTasksAsync(newId);
            foreach (var t in clonedTasks)
            {
                if (string.IsNullOrWhiteSpace(t.PreTaskCodes)) continue;
                var remapped = RemapPreTaskCodes(t.PreTaskCodes, taskIdMap);
                if (remapped != t.PreTaskCodes)
                {
                    t.PreTaskCodes = remapped;
                    await _repo.UpdateTaskAsync(t);
                }
            }
        }

        // 复制变更记录
        if (changes.Count > 0)
        {
            foreach (var c in changes)
            {
                await _repo.AddChangeAsync(new ProjectChange
                {
                    ProjectId = newId,
                    SortOrder = c.SortOrder,
                    ChangeType = c.ChangeType,
                    ChangeParty = c.ChangeParty,
                    ChangeContent = c.ChangeContent,
                    AttachmentUrl = c.AttachmentUrl,
                    ApproverId = c.ApproverId,
                    ApproverName = c.ApproverName,
                    EffectEndDate = c.EffectEndDate,
                    CreatedBy = req.CreatedBy,
                    CreatedByName = req.CreatedByName,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // 复制财务信息
        if (finance != null)
        {
            var newFinance = new ProjectFinance
            {
                ProjectId = newId,
                TaxContractAmount = finance.TaxContractAmount,
                TaxRate = finance.TaxRate,
                CurrencyType = finance.CurrencyType,
                PaymentMethod = finance.PaymentMethod,
                CurrencyAmount = finance.CurrencyAmount,
                ContributionRate = finance.ContributionRate,
                InvoiceRate = finance.InvoiceRate,
                Remark = finance.Remark
            };
            await _repo.SaveFinanceAsync(newFinance);

            if (finance.PlanReceipts.Count > 0)
            {
                var records = finance.PlanReceipts.Select(r => new ProjectPlanReceipt
                {
                    ProjectFinanceId = newFinance.Id,
                    SortOrder = r.SortOrder,
                    PlanAmount = r.PlanAmount,
                    ReceiptType = r.ReceiptType,
                    PlanDate = r.PlanDate,
                    Remark = r.Remark
                }).ToList();
                await _repo.SavePlanReceiptsAsync(newFinance.Id, records);
            }
            if (finance.Receipts.Count > 0)
            {
                var records = finance.Receipts.Select(r => new ProjectReceipt
                {
                    ProjectFinanceId = newFinance.Id,
                    SortOrder = r.SortOrder,
                    ActualAmount = r.ActualAmount,
                    ReceiptType = r.ReceiptType,
                    ReceiptTime = r.ReceiptTime,
                    Remark = r.Remark
                }).ToList();
                await _repo.SaveReceiptsAsync(newFinance.Id, records);
            }
            if (finance.Invoices.Count > 0)
            {
                var records = finance.Invoices.Select(i => new ProjectInvoice
                {
                    ProjectFinanceId = newFinance.Id,
                    SortOrder = i.SortOrder,
                    InvoiceAmount = i.InvoiceAmount,
                    InvoiceRate = i.InvoiceRate,
                    InvoiceTime = i.InvoiceTime,
                    InvoiceNo = i.InvoiceNo,
                    Remark = i.Remark
                }).ToList();
                await _repo.SaveInvoicesAsync(newFinance.Id, records);
            }
        }

        return newId;
    }

    private static string? RemapPreTaskCodes(string? preTaskCodes, Dictionary<long, long> idMap)
    {
        if (string.IsNullOrWhiteSpace(preTaskCodes)) return preTaskCodes;
        var parts = preTaskCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var parenIdx = parts[i].IndexOf('(');
            if (parenIdx <= 0) continue;
            var idStr = parts[i][..parenIdx];
            if (long.TryParse(idStr, out var oldId) && idMap.TryGetValue(oldId, out var newId))
                parts[i] = $"{newId}{parts[i][parenIdx..]}";
        }
        return string.Join(",", parts);
    }
}

// ────────── SaveMilestones ──────────

public record SaveProjectMilestonesCommand(long ProjectId, List<MilestoneInput> Milestones) : IRequest;

public class SaveProjectMilestonesHandler : IRequestHandler<SaveProjectMilestonesCommand>
{
    private readonly IProjectRepository _repo;

    public SaveProjectMilestonesHandler(IProjectRepository repo) => _repo = repo;

    public async Task Handle(SaveProjectMilestonesCommand req, CancellationToken ct)
    {
        var milestones = req.Milestones.Select((m, i) => new ProjectMilestone
        {
            ProjectId = req.ProjectId,
            SortOrder = m.SortOrder > 0 ? m.SortOrder : i + 1,
            MilestoneCode = m.MilestoneCode,
            MilestoneName = m.MilestoneName,
            Status = m.Status,
            PlanFinishDate = m.PlanFinishDate,
            ActualFinishDate = m.ActualFinishDate,
            NodeReference = m.NodeReference,
            Remark = m.Remark
        }).ToList();

        await _repo.SaveMilestonesAsync(req.ProjectId, milestones);
    }
}
