using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectManagement.API.Auth;
using ProjectManagement.Application.Projects.Commands;
using ProjectManagement.Application.Projects.Queries;
using ProjectManagement.Domain.DataScope;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Infrastructure.Data;

namespace ProjectManagement.API.Controllers;

    [ApiController]
[Route("api/v1/projects")]
[Authorize]
public class ProjectController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IProjectRepository _repo;
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ProjectController> _logger;

    public ProjectController(IMediator mediator, IProjectRepository repo, AppDbContext db, IWebHostEnvironment env, ILogger<ProjectController> logger) { _mediator = mediator; _repo = repo; _db = db; _env = env; _logger = logger; }

    // ────────── 列表 ──────────

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? projectCode,
        [FromQuery] string? projectName,
        [FromQuery] string? projectType,
        [FromQuery] int? status,
        [FromQuery] string? projectManagerName,
        [FromQuery] long? memberId,
        [FromQuery] long? assigneeId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        var (userId, _) = GetUserInfo();
        var codes = await ProjectPermissionHelper.GetUserPermissionCodesAsync(_db, userId);
        ProjectListScopeFilter? scopeFilter = null;
        if (!ProjectPermissionHelper.CanViewProjectList(codes))
        {
            if (!ProjectPermissionHelper.IsSelfScopedListQuery(userId, memberId, assigneeId))
                return Forbid();
            scopeFilter = new ProjectListScopeFilter { WorkbenchSelfOnly = true, UserId = userId };
            memberId = userId;
            assigneeId = userId;
        }
        else
        {
            if (memberId.HasValue && memberId != userId) memberId = null;
            if (assigneeId.HasValue && assigneeId != userId) assigneeId = null;
            scopeFilter = await ProjectDataScopeResolver.BuildListFilterAsync(_db, userId);
        }

        var query = new GetProjectListQuery(projectCode, projectName, projectType, status, projectManagerName, memberId, assigneeId, pageIndex, pageSize, scopeFilter);
        var result = await _mediator.Send(query);
        return Ok(new { code = 0, message = "success", data = result });
    }

    // ────────── 详情 ──────────

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetDetail(long id)
    {
        var (userId, _) = GetUserInfo();
        var codes = await ProjectPermissionHelper.GetUserPermissionCodesAsync(_db, userId);
        if (!await ProjectPermissionHelper.CanAccessProjectAsync(_db, id, userId, codes))
            return Forbid();

        var result = await _mediator.Send(new GetProjectDetailQuery(id));
        if (result == null) return Ok(new { code = 404, message = "项目不存在" });

        // 字段级权限脱敏：无权限的字段置 null（拥有 project:field:basic 则全部可见）
        bool HasFieldPerm(string code) => codes.Contains(code) || codes.Contains("project:field:basic");
        if (!HasFieldPerm("project:field:contract-code")) result.ContractCode = null;
        if (!HasFieldPerm("project:field:category-code")) result.CategoryCode = null;
        if (!HasFieldPerm("project:field:customer-name")) result.CustomerName = null;
        if (!HasFieldPerm("project:field:regional-manager")) result.RegionalManagerName = null;
        if (!HasFieldPerm("project:field:final-customer")) result.FinalCustomer = null;
        if (!HasFieldPerm("project:field:pm-center")) result.PmCenter = null;
        if (!HasFieldPerm("project:field:delivery-location")) result.DeliveryLocation = null;
        if (!HasFieldPerm("project:field:project-type")) result.ProjectType = null;
        if (!HasFieldPerm("project:field:engineering-center")) result.EngineeringCenter = null;
        if (!HasFieldPerm("project:field:pre-sales")) result.PreSalesManagerName = null;
        if (!HasFieldPerm("project:field:project-manager")) result.ProjectManagerName = null;
        if (!HasFieldPerm("project:field:sales-manager")) result.SalesManagerName = null;
        if (!HasFieldPerm("project:field:required-delivery")) result.RequiredDelivery = null;
        if (!HasFieldPerm("project:field:accepted-delivery")) result.AcceptedDelivery = null;
        if (!HasFieldPerm("project:field:special-terms")) result.SpecialTerms = null;
        if (!HasFieldPerm("project:field:remark")) result.Remark = null;

        result.CanManageStatus = await ProjectDataScopeResolver.CanChangeProjectStatusAsync(_db, id, userId);
        result.CanDeactivate = await ProjectDataScopeResolver.CanDeactivateProjectAsync(_db, id, userId);

        return Ok(new { code = 0, message = "success", data = result });
    }

    // ────────── 新建 ──────────

    [HttpPost]
    [RequirePermission("project:create")]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest req)
    {
        var (userId, realName) = GetUserInfo();
        var command = new CreateProjectCommand(
            req.ProjectCode, req.ProjectName, req.ProjectType, req.ContractCode,
            req.EngineeringCenter, req.CategoryCode, req.CustomerName,
            req.RegionalManagerId, req.RegionalManagerName,
            req.CustomerContactPhone, req.CustomerContactEmail,
            req.SalesManagerId, req.SalesManagerName,
            req.PreSalesManagerId, req.PreSalesManagerName, req.SalesRegion,
            req.ProjectManagerId, req.ProjectManagerName, req.PmCenter,
            req.OwnerContactPhone, req.BusinessContactEmail,
            req.PlanStartDate, req.RequiredDelivery, req.AcceptedDelivery,
            req.DeliveryLocation, req.FinalCustomer, req.ProjectScope, req.SpecialTerms, req.Remark, req.ProgressDesc,
            userId, realName, req.Products, req.UploadedFileIds);
        var id = await _mediator.Send(command);
        if (req.UploadedFileIds?.Count > 0)
        {
            var files = await _db.ProjectFiles.Where(f => req.UploadedFileIds.Contains(f.Id)).ToListAsync();
            foreach (var f in files)
            {
                f.ProjectId = id;
                var oldPath = f.FilePath;
                var newDir = $"Uploads/project_{id}";
                var newPath = $"{newDir}/{Path.GetFileName(f.FilePath)}";
                Directory.CreateDirectory(Path.Combine(_env.ContentRootPath, newDir));
                if (System.IO.File.Exists(Path.Combine(_env.ContentRootPath, oldPath)))
                {
                    System.IO.File.Move(Path.Combine(_env.ContentRootPath, oldPath), Path.Combine(_env.ContentRootPath, newPath));
                }
                f.FilePath = newPath;
            }
            await _db.SaveChangesAsync();
        }
        await LogOp(id, "创建项目", $"创建项目「{req.ProjectName}」");
        return Ok(new { code = 0, message = "success", data = new { id } });
    }

    // ────────── 复制项目 ──────────

    [HttpPost("{id:long}/copy")]
    [RequirePermission("project:create")]
    public async Task<IActionResult> Copy(long id, [FromBody] CopyProjectRequest? req)
    {
        var (userId, realName) = GetUserInfo();
        var source = await _repo.GetByIdAsync(id);
        if (source == null) return Ok(new { code = 404, message = "源项目不存在" });

        var newCode = req?.NewProjectCode;
        if (string.IsNullOrWhiteSpace(newCode))
        {
            newCode = source.ProjectCode + "-副本";
            int suffix = 1;
            while (await _repo.ProjectCodeExistsAsync(newCode))
                newCode = $"{source.ProjectCode}-副本{suffix++}";
        }

        var newId = await _mediator.Send(new CopyProjectCommand(id, newCode, userId, realName));
        await LogOp(newId, "复制项目", $"从项目「{source.ProjectName}」(#{id}) 复制");
        return Ok(new { code = 0, message = "success", data = new { id = newId } });
    }

    // ────────── 更新 ──────────

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateProjectRequest req)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        var (userId, _) = GetUserInfo();
        var old = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (old?.Status != 0)
            return Ok(new { code = 400, message = "只有未激活的项目可以修改基本信息" });
        var command = new UpdateProjectCommand(
            id, req.ProjectName, req.ProjectType, req.ContractCode,
            req.EngineeringCenter, req.CategoryCode, req.CustomerName,
            req.RegionalManagerId, req.RegionalManagerName,
            req.CustomerContactPhone, req.CustomerContactEmail,
            req.SalesManagerId, req.SalesManagerName,
            req.PreSalesManagerId, req.PreSalesManagerName, req.SalesRegion,
            req.ProjectManagerId, req.ProjectManagerName, req.PmCenter,
            req.OwnerContactPhone, req.BusinessContactEmail,
            req.PlanStartDate, req.RequiredDelivery, req.AcceptedDelivery,
            req.ActualFinishDate, req.DeliveryLocation, req.FinalCustomer,
            req.ProjectScope, req.SpecialTerms, req.Remark,
            req.QualityStrategy, req.ProjectDelivery, req.ReportContent,
            req.RiskStatus, req.CurrentPhaseDate, req.NextStatus, req.ProgressDesc,
            userId, req.Products);
        await _mediator.Send(command);
        var projectTypeNames = await _db.DictItems.Where(d => d.DictType == "project_type").ToDictionaryAsync(d => d.DictCode, d => d.DictLabel);
        var diffs = new List<string>();
        Compare(diffs, "项目名称", old?.ProjectName, req.ProjectName);
        Compare(diffs, "项目类型",
            old?.ProjectType != null && projectTypeNames.TryGetValue(old.ProjectType, out var oldPt) ? oldPt : old?.ProjectType,
            req.ProjectType != null && projectTypeNames.TryGetValue(req.ProjectType, out var newPt) ? newPt : req.ProjectType);
        Compare(diffs, "合同编号", old?.ContractCode, req.ContractCode);
        Compare(diffs, "责任部门", old?.EngineeringCenter, req.EngineeringCenter);
        Compare(diffs, "客户名称", old?.CustomerName, req.CustomerName);
        Compare(diffs, "客户联系电话", old?.CustomerContactPhone, req.CustomerContactPhone);
        Compare(diffs, "客户联系邮箱", old?.CustomerContactEmail, req.CustomerContactEmail);
        Compare(diffs, "项目经理", old?.ProjectManagerName, req.ProjectManagerName);
        Compare(diffs, "销售负责人", old?.SalesManagerName, req.SalesManagerName);
        Compare(diffs, "售前联系人", old?.PreSalesManagerName, req.PreSalesManagerName);
        CompareDate(diffs, "计划开始", old?.PlanStartDate, req.PlanStartDate);
        CompareDate(diffs, "要求交期", old?.RequiredDelivery, req.RequiredDelivery);
        CompareDate(diffs, "承诺交期", old?.AcceptedDelivery, req.AcceptedDelivery);
        CompareDate(diffs, "实际完成", old?.ActualFinishDate, req.ActualFinishDate);
        Compare(diffs, "交付地点", old?.DeliveryLocation, req.DeliveryLocation);
        Compare(diffs, "最终业主", old?.FinalCustomer, req.FinalCustomer);
        Compare(diffs, "业主联系电话", old?.OwnerContactPhone, req.OwnerContactPhone);
        Compare(diffs, "业务联系邮箱", old?.BusinessContactEmail, req.BusinessContactEmail);
        Compare(diffs, "项目范围", old?.ProjectScope, req.ProjectScope);
        Compare(diffs, "备注", old?.Remark, req.Remark);
        Compare(diffs, "风险状态", old?.RiskStatus, req.RiskStatus);
        Compare(diffs, "进展描述", old?.ProgressDesc, req.ProgressDesc);
        if (diffs.Count > 0) await LogOp(id, "更新项目基本信息", string.Join("；", diffs));
        return Ok(new { code = 0, message = "success" });
    }

    // ────────── 删除项目 ──────────

    [HttpDelete("{id:long}")]
    [RequirePermission("project:delete")]
    public async Task<IActionResult> Delete(long id)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        var project = await _repo.GetByIdAsync(id);
        if (project == null) return Ok(new { code = 404, message = "项目不存在" });

        // 级联删除项目下的所有问题和措施
        var issueIds = await _db.ProjectIssues.Where(i => i.ProjectId == id).Select(i => i.Id).ToListAsync();
        if (issueIds.Count > 0)
        {
            var measures = await _db.ProjectIssueMeasures.Where(m => issueIds.Contains(m.IssueId)).ToListAsync();
            _db.ProjectIssueMeasures.RemoveRange(measures);
            var issues = await _db.ProjectIssues.Where(i => i.ProjectId == id).ToListAsync();
            _db.ProjectIssues.RemoveRange(issues);
        }

        _db.Projects.Remove(project);
        await LogOp(id, "删除项目", $"删除项目「{project.ProjectName}」");
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    // ────────── 状态操作 ──────────

    [HttpPost("{id:long}/activate")]
    public async Task<IActionResult> Activate(long id)
    {
        var (userId, _) = GetUserInfo();
        if (!await ProjectDataScopeResolver.CanChangeProjectStatusAsync(_db, id, userId))
            return Forbid();
        var oldStatus = (await _repo.GetByIdAsync(id))?.Status ?? 0;
        await _mediator.Send(new ChangeProjectStatusCommand(id, 1, userId));
        await LogOp(id, "激活项目", $"将状态从「{StatusLabel(oldStatus)}」改为「进行中」");
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPost("{id:long}/complete")]
    public async Task<IActionResult> Complete(long id)
    {
        var (userId, _) = GetUserInfo();
        if (!await ProjectDataScopeResolver.CanChangeProjectStatusAsync(_db, id, userId))
            return Forbid();

        // 检查是否可强制完成：is_project_force_finish=1 跳过任务检查
        var forceParam = await _db.SysParams.FirstOrDefaultAsync(p => p.ParamKey == "is_project_force_finish");
        if (forceParam == null || forceParam.ParamValue != "1")
        {
            var hasUnfinished = await _db.ProjectTasks.AnyAsync(t => t.ProjectId == id && t.Status != 2);
            if (hasUnfinished)
                return Ok(new { code = 400, message = "该项目有未完成的任务，不能标记为完成" });
        }

        var oldStatus = (await _repo.GetByIdAsync(id))?.Status ?? 0;
        await _mediator.Send(new ChangeProjectStatusCommand(id, 2, userId));
        await LogOp(id, "完成项目", $"将状态从「{StatusLabel(oldStatus)}」改为「已完成」");
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPost("{id:long}/suspend")]
    public async Task<IActionResult> Suspend(long id)
    {
        var (userId, _) = GetUserInfo();
        if (!await ProjectDataScopeResolver.CanChangeProjectStatusAsync(_db, id, userId))
            return Forbid();
        var oldStatus = (await _repo.GetByIdAsync(id))?.Status ?? 0;
        await _mediator.Send(new ChangeProjectStatusCommand(id, 3, userId));
        await LogOp(id, "暂停项目", $"将状态从「{StatusLabel(oldStatus)}」改为「暂停」");
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPost("{id:long}/resume")]
    public async Task<IActionResult> Resume(long id)
    {
        var (userId, _) = GetUserInfo();
        var project = await _repo.GetByIdAsync(id);
        if (project == null) return Ok(new { code = 404, message = "项目不存在" });
        if (project.Status != 3) return Ok(new { code = 400, message = "只有暂停的项目可以恢复" });
        if (!await ProjectDataScopeResolver.CanChangeProjectStatusAsync(_db, id, userId))
            return Forbid();
        await _mediator.Send(new ChangeProjectStatusCommand(id, 1, userId));
        await LogOp(id, "取消暂停", $"将状态从「暂停」改为「进行中」");
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPost("{id:long}/deactivate")]
    public async Task<IActionResult> Deactivate(long id)
    {
        var (userId, _) = GetUserInfo();
        var project = await _repo.GetByIdAsync(id);
        if (project == null) return Ok(new { code = 404, message = "项目不存在" });
        if (project.Status != 1 && project.Status != 2) return Ok(new { code = 400, message = "只有进行中或已完成的项目可以反激活" });

        // 仅系统管理员、项目管理员角色可取消激活（项目经理不可）
        if (!await ProjectDataScopeResolver.CanDeactivateProjectAsync(_db, id, userId))
            return Forbid();

        var oldStatus = project.Status;
        await _mediator.Send(new ChangeProjectStatusCommand(id, 0, userId));
        await LogOp(id, "取消激活项目", $"将状态从「{StatusLabel(oldStatus)}」改为「未激活」");
        return Ok(new { code = 0, message = "success" });
    }

    // ────────── 成员 ──────────

    [HttpPut("{id:long}/members")]
    public async Task<IActionResult> SaveMembers(long id, [FromBody] SaveProjectMembersRequest req)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        await _mediator.Send(new SaveProjectMembersCommand(id, req.Members));
        await LogOp(id, "更新项目成员", $"更新了 {req.Members.Count} 名成员");
        return Ok(new { code = 0, message = "success" });
    }

    // ────────── 里程碑 ──────────

    [HttpPut("{id:long}/milestones")]
    public async Task<IActionResult> SaveMilestones(long id, [FromBody] SaveProjectMilestonesRequest req)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        await _mediator.Send(new SaveProjectMilestonesCommand(id, req.Milestones));
        await LogOp(id, "更新里程碑", $"更新了 {req.Milestones.Count} 个里程碑");
        return Ok(new { code = 0, message = "success" });
    }

    // ────────── 任务计划 ──────────

    [HttpGet("{id:long}/tasks")]
    public async Task<IActionResult> GetTasks(long id)
    {
        var denied = await ForbidIfCannotAccessAsync(id);
        if (denied != null) return denied;
        var tasks = await _repo.GetTasksAsync(id);
        return Ok(new { code = 0, message = "success", data = tasks });
    }

    /// <summary>跨项目任务列表：返回当前用户可访问的所有项目的任务（含项目编号、名称），支持多维度筛选。</summary>
    [HttpGet("tasks")]
    public async Task<IActionResult> GetAllTasks(
        [FromQuery] long? projectId,
        [FromQuery] string? planStartDateFrom,
        [FromQuery] string? planStartDateTo,
        [FromQuery] string? planFinishDateFrom,
        [FromQuery] string? planFinishDateTo,
        [FromQuery] int? status,
        [FromQuery] long? assigneeId)
    {
        var (userId, _) = GetUserInfo();
        var scopeFilter = await ProjectDataScopeResolver.BuildListFilterAsync(_db, userId);

        var projectQuery = _db.Projects.AsQueryable();
        projectQuery = ProjectDataScopeFilter.Apply(projectQuery, _db, scopeFilter);
        projectQuery = projectQuery.Where(p => p.Status == 1);
        if (projectId.HasValue)
            projectQuery = projectQuery.Where(p => p.Id == projectId.Value);

        var allowedProjectIds = await projectQuery.Select(p => p.Id).ToListAsync();
        if (allowedProjectIds.Count == 0)
            return Ok(new { code = 0, message = "success", data = Array.Empty<object>() });

        // 所有用户（含管理员）仅能查看自己所在项目成员列表中的任务
        var memberProjectIds = await _db.ProjectMembers
            .Where(m => m.MemberId == userId)
            .Select(m => m.ProjectId).Distinct().ToListAsync();
        allowedProjectIds = allowedProjectIds.Intersect(memberProjectIds).ToList();

        var taskQuery = _db.ProjectTasks
            .Where(t => allowedProjectIds.Contains(t.ProjectId));

        if (status.HasValue)
            taskQuery = taskQuery.Where(t => t.Status == status.Value);
        if (assigneeId.HasValue)
            taskQuery = taskQuery.Where(t => t.AssigneeId == assigneeId.Value);
        if (DateTime.TryParse(planStartDateFrom, out var psdFrom))
            taskQuery = taskQuery.Where(t => t.PlanStartDate >= psdFrom);
        if (DateTime.TryParse(planStartDateTo, out var psdTo))
            taskQuery = taskQuery.Where(t => t.PlanStartDate <= psdTo);
        if (DateTime.TryParse(planFinishDateFrom, out var pfdFrom))
            taskQuery = taskQuery.Where(t => t.PlanFinishDate >= pfdFrom);
        if (DateTime.TryParse(planFinishDateTo, out var pfdTo))
            taskQuery = taskQuery.Where(t => t.PlanFinishDate <= pfdTo);

        var tasks = await taskQuery
            .Join(_db.Projects, t => t.ProjectId, p => p.Id, (t, p) => new { t, p })
            .OrderBy(x => x.p.ProjectCode)
            .ThenBy(x => x.t.TaskNo)
            .Select(x => new
            {
                x.t.Id,
                x.t.TaskNo,
                x.t.TaskName,
                x.t.NodeType,
                x.t.TaskCategory,
                x.t.SortOrder,
                x.t.Status,
                x.t.Priority,
                x.t.ProgressPct,
                x.t.PlanStartDate,
                x.t.PlanFinishDate,
                x.t.ActualStartDate,
                x.t.ActualFinishDate,
                x.t.PlanDuration,
                x.t.ActualDuration,
                x.t.ReferenceDuration,
                x.t.PreTaskCodes,
                x.t.WbsCode,
                x.t.AssigneeId,
                x.t.AssigneeName,
                x.t.DeptId,
                x.t.DeptName,
                x.t.DeliverableCnt,
                x.t.Remark,
                x.t.ProjectId,
                ProjectCode = x.p.ProjectCode,
                ProjectName = x.p.ProjectName,
                ProjectStatus = x.p.Status
            })
            .ToListAsync();

        return Ok(new { code = 0, message = "success", data = tasks });
    }

    [HttpGet("tasks/my")]
    public async Task<IActionResult> GetMyTasks()
    {
        var (userId, _) = GetUserInfo();
        var today = DateTime.Today;
        var tasks = await _db.ProjectTasks
            .Where(t => t.AssigneeId == userId && t.AssigneeName != null && t.AssigneeName != "")
            .Where(t => t.Project != null && t.Project.Status == 1)
            .Where(t => t.Project.Members.Any(m => m.MemberId == userId))
            .OrderBy(t => t.Project != null ? t.Project.ProjectCode : "")
            .ThenBy(t => t.TaskNo)
            .Select(t => new
            {
                t.Id,
                t.TaskNo,
                t.TaskName,
                t.Status,
                t.ProgressPct,
                t.PlanStartDate,
                t.PlanFinishDate,
                t.ActualStartDate,
                t.ActualFinishDate,
                t.PlanDuration,
                t.ActualDuration,
                t.ProjectId,
                ProjectCode = t.Project != null ? t.Project.ProjectCode : "",
                ProjectName = t.Project != null ? t.Project.ProjectName : "",
                ProjectStatus = t.Project != null ? t.Project.Status : (int?)null,
                IsOverdue = t.Status != 2 && t.PlanFinishDate.HasValue && t.PlanFinishDate.Value < today
            })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = tasks });
    }

    /// <summary>当前用户待处理任务数量（未开始+进行中），给侧边栏菜单角标使用</summary>
    [HttpGet("tasks/my/pending-count")]
    public async Task<IActionResult> GetMyPendingTaskCount()
    {
        var (userId, _) = GetUserInfo();
        var count = await _db.ProjectTasks
            .Where(t => t.AssigneeId == userId)
            .Where(t => t.Status == 0 || t.Status == 1)
            .Where(t => t.Project != null && t.Project.Status == 1)
            .Where(t => t.Project.Members.Any(m => m.MemberId == userId))
            .CountAsync();
        return Ok(new { code = 0, message = "success", data = new { count } });
    }

    [HttpPost("{id:long}/tasks")]
    public async Task<IActionResult> CreateTask(long id, [FromBody] TaskInput input)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        var (userId, _) = GetUserInfo();
        input.SortOrder = input.SortOrder == 0 ? (await _repo.GetTasksAsync(id)).Count + 1 : input.SortOrder;
        var taskId = await _mediator.Send(new CreateProjectTaskCommand(id, input, userId));
        await LogOp(id, "新增任务", $"新增任务「{input.TaskName}」");
        return Ok(new { code = 0, message = "success", data = new { id = taskId } });
    }

    [HttpPost("{id:long}/tasks/from-template")]
    public async Task<IActionResult> CreateTasksFromTemplate(long id, [FromBody] CreateTasksFromTemplateRequest req)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        var count = await _mediator.Send(new CreateTasksFromTemplateCommand(id, req.TemplateId));
        await LogOp(id, "从模板创建任务", $"从模板创建了 {count} 条任务");
        return Ok(new { code = 0, message = "success", data = new { count } });
    }

    [HttpPut("{id:long}/tasks/{taskId:long}")]
    public async Task<IActionResult> UpdateTask(long id, long taskId, [FromBody] TaskInput input)
    {
        var (userId, _) = GetUserInfo();
        if (!await ProjectDataScopeResolver.CanUpdateProjectTaskAsync(_db, id, taskId, userId))
            return Forbid();
        input.TaskNo = input.TaskNo ?? "";
        var old = await _db.ProjectTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId);
        await _mediator.Send(new UpdateProjectTaskCommand(taskId, input));
        var deptNames = await _db.Departments.ToDictionaryAsync(d => d.Id, d => d.Name);
        var taskCatNames = await _db.DictItems.Where(d => d.DictType == "task_category").ToDictionaryAsync(d => d.DictCode, d => d.DictLabel);
        var diffs = new List<string>();
        Compare(diffs, "任务名称", old?.TaskName, input.TaskName);
        Compare(diffs, "工序号", old?.WbsCode, input.WbsCode);
        Compare(diffs, "任务类别",
            old?.TaskCategory != null && taskCatNames.TryGetValue(old.TaskCategory, out var oldCat) ? oldCat : old?.TaskCategory,
            input.TaskCategory != null && taskCatNames.TryGetValue(input.TaskCategory, out var newCat) ? newCat : input.TaskCategory);
        CompareInt(diffs, "节点类型", old?.NodeType, input.NodeType, v => v == 1 ? "任务" : "里程碑");
        CompareInt(diffs, "优先级", old?.Priority, input.Priority, v => new[] { "", "最高", "高", "中", "低" }[v]);
        CompareInt(diffs, "状态", old?.Status, input.Status, v => new[] { "未开始", "进行中", "已完成", "已延误" }[v]);
        CompareDate(diffs, "计划开始", old?.PlanStartDate, input.PlanStartDate);
        CompareDate(diffs, "计划完成", old?.PlanFinishDate, input.PlanFinishDate);
        CompareDate(diffs, "实际开始", old?.ActualStartDate, input.ActualStartDate);
        CompareDate(diffs, "实际完成", old?.ActualFinishDate, input.ActualFinishDate);
        CompareLong(diffs, "责任部门", old?.DeptId, input.DeptId, v => deptNames.TryGetValue(v, out var n) ? n : v.ToString());
        Compare(diffs, "责任人", old?.AssigneeName, input.AssigneeName);
        CompareInt(diffs, "参考工期", old?.ReferenceDuration, input.ReferenceDuration, v => v.ToString());
        CompareInt(diffs, "进度", old?.ProgressPct, input.ProgressPct, v => $"{v:0.##}%");
        Compare(diffs, "前置任务", old?.PreTaskCodes, input.PreTaskCodes);
        Compare(diffs, "备注", old?.Remark, input.Remark);
        if (diffs.Count > 0) await LogOp(id, "编辑任务", $"【序号】{old?.TaskNo} 【任务名称】「{old?.TaskName}」；{string.Join("；", diffs)}");
        return Ok(new { code = 0, message = "success" });
    }

    [HttpDelete("{id:long}/tasks/{taskId:long}")]
    public async Task<IActionResult> DeleteTask(long id, long taskId)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        var task = await _db.ProjectTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId);
        await _mediator.Send(new DeleteProjectTaskCommand(taskId));
        var taskCatNames = await _db.DictItems.Where(d => d.DictType == "task_category").ToDictionaryAsync(d => d.DictCode, d => d.DictLabel);
        var parts = new List<string>();
        if (task != null)
        {
            parts.Add($"【任务名称】「{task.TaskName}」");
            if (!string.IsNullOrEmpty(task.WbsCode)) parts.Add($"【工序号】{task.WbsCode}");
            if (!string.IsNullOrEmpty(task.TaskCategory))
            {
                var catLabel = taskCatNames.TryGetValue(task.TaskCategory, out var lbl) ? lbl : task.TaskCategory;
                parts.Add($"【任务类别】{catLabel}");
            }
            if (!string.IsNullOrEmpty(task.AssigneeName)) parts.Add($"【责任人】{task.AssigneeName}");
            if (!string.IsNullOrEmpty(task.DeptName)) parts.Add($"【责任部门】{task.DeptName}");
            if (task.ProgressPct > 0) parts.Add($"【进度】{task.ProgressPct}%");
        }
        await LogOp(id, "删除任务", parts.Count > 0 ? string.Join("，", parts) : "删除任务");
        return Ok(new { code = 0, message = "success" });
    }

    // ────────── 变更记录 ──────────

    [HttpGet("{id:long}/changes")]
    public async Task<IActionResult> GetChanges(long id)
    {
        var changes = await _repo.GetChangesAsync(id);
        return Ok(new { code = 0, message = "success", data = changes });
    }

    [HttpPost("{id:long}/changes")]
    public async Task<IActionResult> CreateChange(long id, [FromBody] ChangeInput input)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        var (userId, realName) = GetUserInfo();
        var changeId = await _mediator.Send(new CreateProjectChangeCommand(id, input, userId, realName));
        await LogOp(id, "新增变更记录", $"新增变更「{input.ChangeType}」");
        return Ok(new { code = 0, message = "success", data = new { id = changeId } });
    }

    [HttpPut("{id:long}/changes/{changeId:long}")]
    public async Task<IActionResult> UpdateChange(long id, long changeId, [FromBody] ChangeInput input)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        await _mediator.Send(new UpdateProjectChangeCommand(changeId, input));
        await LogOp(id, "编辑变更记录", "编辑变更记录");
        return Ok(new { code = 0, message = "success" });
    }

    [HttpDelete("{id:long}/changes/{changeId:long}")]
    public async Task<IActionResult> DeleteChange(long id, long changeId)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        await _mediator.Send(new DeleteProjectChangeCommand(changeId));
        await LogOp(id, "删除变更记录", "删除变更记录");
        return Ok(new { code = 0, message = "success" });
    }

    // ────────── 财务信息 ──────────

    [HttpGet("{id:long}/finance")]
    public async Task<IActionResult> GetFinance(long id)
    {
        var finance = await _repo.GetFinanceAsync(id);
        return Ok(new { code = 0, message = "success", data = finance });
    }

    [HttpPut("{id:long}/finance")]
    public async Task<IActionResult> SaveFinance(long id, [FromBody] FinanceInput input)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        await _mediator.Send(new SaveProjectFinanceCommand(id, input));
        await LogOp(id, "更新财务信息", "更新合同财务概况");
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPut("{id:long}/finance/plan-receipts")]
    public async Task<IActionResult> SavePlanReceipts(long id, [FromBody] SavePlanReceiptsRequest req)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        await _mediator.Send(new SavePlanReceiptsCommand(id, req.Records));
        await LogOp(id, "更新计划收款", $"更新了 {req.Records.Count} 条计划收款记录");
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPut("{id:long}/finance/receipts")]
    public async Task<IActionResult> SaveReceipts(long id, [FromBody] SaveReceiptsRequest req)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        await _mediator.Send(new SaveReceiptsCommand(id, req.Records));
        await LogOp(id, "更新收款记录", $"更新了 {req.Records.Count} 条收款记录");
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPut("{id:long}/finance/invoices")]
    public async Task<IActionResult> SaveInvoices(long id, [FromBody] SaveInvoicesRequest req)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        await _mediator.Send(new SaveInvoicesCommand(id, req.Records));
        await LogOp(id, "更新开票记录", $"更新了 {req.Records.Count} 条开票记录");
        return Ok(new { code = 0, message = "success" });
    }

    // ────────── 文件上传 ──────────

    [HttpPost("~/api/v1/files/upload")]
    public async Task<IActionResult> TempUpload(List<IFormFile> files, CancellationToken cancellationToken)
    {
        var uploadDir = Path.Combine(_env.ContentRootPath, "Uploads", "temp");
        Directory.CreateDirectory(uploadDir);

        var results = new List<object>();
        foreach (var file in files)
        {
            var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadDir, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            var pf = new ProjectFile
            {
                ProjectId = 0,
                FileName = file.FileName,
                FilePath = $"Uploads/temp/{fileName}",
                FileSize = file.Length
            };
            _db.ProjectFiles.Add(pf);
            await _db.SaveChangesAsync();

            results.Add(new { pf.Id, pf.FileName, pf.FileSize, pf.CreatedAt });
        }

        return Ok(new { code = 0, message = "success", data = results.Count == 1 ? results[0] : results });
    }

    [HttpPost("{id:long}/files")]
    public async Task<IActionResult> UploadFile(long id, List<IFormFile> files, CancellationToken cancellationToken)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        var project = await _repo.GetByIdAsync(id);
        if (project == null) return Ok(new { code = 404, message = "项目不存在" });

        var uploadDir = Path.Combine(_env.ContentRootPath, "Uploads", $"project_{id}");
        Directory.CreateDirectory(uploadDir);

        var results = new List<object>();
        foreach (var file in files)
        {
            var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadDir, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            var pf = new ProjectFile
            {
                ProjectId = id,
                FileName = file.FileName,
                FilePath = $"Uploads/project_{id}/{fileName}",
                FileSize = file.Length
            };
            _db.ProjectFiles.Add(pf);
            await _db.SaveChangesAsync();

            results.Add(new { pf.Id, pf.FileName, pf.FileSize, pf.CreatedAt });
        }

        return Ok(new { code = 0, message = "success", data = results });
    }

    [HttpGet("{id:long}/files")]
    public async Task<IActionResult> GetFiles(long id)
    {
        var files = await _db.ProjectFiles
            .Where(f => f.ProjectId == id)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new { f.Id, f.FileName, f.FileSize, f.CreatedAt, f.FilePath })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = files });
    }

    [HttpDelete("{id:long}/files/{fileId:long}")]
    public async Task<IActionResult> DeleteFile(long id, long fileId)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;
        var file = await _db.ProjectFiles.FindAsync(fileId);
        if (file == null) return Ok(new { code = 404, message = "文件不存在" });

        var fullPath = Path.Combine(_env.ContentRootPath, file.FilePath);
        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);

        _db.ProjectFiles.Remove(file);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    // ────────── 文件资料管理 ──────────

    [HttpGet("{id:long}/file-items")]
    public async Task<IActionResult> GetFileItems(long id)
    {
        var denied = await ForbidIfCannotAccessAsync(id);
        if (denied != null) return denied;
        var (userId, _) = GetUserInfo();

        // ForbidIfCannotAccessAsync 已校验：admin / 项目成员 / 任务负责人 / 数据范围
        var codes = await ProjectPermissionHelper.GetUserPermissionCodesAsync(_db, userId);
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (project == null) return Ok(new { code = 404, message = "项目不存在" });
        var memberIds = await _db.ProjectMembers.Where(m => m.ProjectId == id).Select(m => m.MemberId).ToListAsync();

        // 检查用户是否有查看文件权限
        var canViewPublic = codes.Contains("project:file:view:public");
        var canViewNonPublic = codes.Contains("project:file:view:nonpublic");
        if (!canViewPublic && !canViewNonPublic)
            return Ok(new { code = 0, message = "success", data = Array.Empty<object>() });

        var today = DateTime.Today;
        var items = await _db.ProjectFileItems
            .Where(f => f.ProjectId == id)
            .OrderBy(f => f.SortOrder)
            .Select(f => new
            {
                f.Id, f.TemplateItemId, f.SortOrder, f.FileName, f.Required,
                f.IsPublic, f.ViewRoles, f.AssigneeId, f.AssigneeName,
                f.DeptId, f.DeptName, f.PlanFinishDate, f.LatestVersionId, f.Remark,
                VersionCount = f.Versions.Count,
                LatestVersion = f.LatestVersion != null
                    ? new
                    {
                        f.LatestVersion.Id,
                        f.LatestVersion.VersionNumber,
                        f.LatestVersion.UploadedByName,
                        f.LatestVersion.UploadedAt,
                        Files = f.LatestVersion.Files
                            .OrderBy(vf => vf.Id)
                            .Select(vf => new { vf.Id, vf.OriginalFileName, vf.FileSize, vf.FileExt })
                            .ToList()
                    }
                    : null
            })
            .ToListAsync();

        var result = items.Select(f =>
        {
            // 文件可见性校验
            bool visible = CanViewFile(f.IsPublic, f.ViewRoles, f.AssigneeId, project!, userId, memberIds, canViewPublic, canViewNonPublic);

            // 计算 PlanFinishStatus
            string? planFinishStatus = null;
            if (f.PlanFinishDate.HasValue && f.LatestVersionId.HasValue)
            {
                var latestUploadedAt = _db.ProjectFileVersions
                    .Where(v => v.Id == f.LatestVersionId.Value)
                    .Select(v => (DateTime?)v.UploadedAt)
                    .FirstOrDefault();
                if (!latestUploadedAt.HasValue || latestUploadedAt.Value < f.PlanFinishDate.Value)
                {
                    if (today > f.PlanFinishDate.Value) planFinishStatus = "overdue";
                    else if ((f.PlanFinishDate.Value - today).TotalDays <= 7) planFinishStatus = "expiring";
                    else planFinishStatus = "normal";
                }
            }

            return new
            {
                f.Id, f.TemplateItemId, f.SortOrder, f.FileName, f.Required,
                f.IsPublic, f.ViewRoles, f.AssigneeId, f.AssigneeName,
                f.DeptId, f.DeptName, f.PlanFinishDate,
                PlanFinishStatus = planFinishStatus,
                HasUpload = f.LatestVersionId.HasValue,
                LatestVersion = visible ? f.LatestVersion : null,
                VersionCount = f.VersionCount,
                f.Remark
            };
        });

        return Ok(new { code = 0, message = "success", data = result });
    }

    [HttpPut("{id:long}/file-items")]
    public async Task<IActionResult> SaveFileItems(long id, [FromBody] SaveProjectFileItemsRequest req)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;

        var existingItems = await _db.ProjectFileItems
            .Where(f => f.ProjectId == id)
            .ToListAsync();
        var existingDict = existingItems.ToDictionary(e => e.Id);
        var incomingIds = req.Items.Where(i => i.Id.HasValue).Select(i => i.Id!.Value).ToHashSet();

        // 删除前端已移除的行
        var toDelete = existingItems.Where(e => !incomingIds.Contains(e.Id)).ToList();
        if (toDelete.Count > 0)
        {
            // 先断开 LatestVersionId 循环引用，再删
            foreach (var item in toDelete)
                item.LatestVersionId = null;
            await _db.SaveChangesAsync();
            _db.ProjectFileItems.RemoveRange(toDelete);
            await _db.SaveChangesAsync();
        }

        // 更新或新增
        int sort = 1;
        foreach (var item in req.Items)
        {
            string? viewRolesJson = null;
            if (item.ViewRoles != null && item.ViewRoles.Count > 0)
                viewRolesJson = System.Text.Json.JsonSerializer.Serialize(item.ViewRoles);

            if (item.Id.HasValue && existingDict.TryGetValue(item.Id.Value, out var existing))
            {
                // 更新已有项（保留版本记录）
                existing.SortOrder = sort++;
                existing.FileName = item.FileName;
                existing.Required = item.Required;
                existing.IsPublic = item.IsPublic;
                existing.ViewRoles = viewRolesJson;
                existing.AssigneeId = item.AssigneeId;
                existing.AssigneeName = item.AssigneeName;
                existing.DeptId = item.DeptId;
                existing.DeptName = item.DeptName;
                existing.PlanFinishDate = item.PlanFinishDate;
                existing.Remark = item.Remark;
            }
            else
            {
                // 新增项
                var entity = new ProjectFileItem
                {
                    ProjectId = id,
                    TemplateItemId = item.TemplateItemId,
                    SortOrder = sort++,
                    FileName = item.FileName,
                    Required = item.Required,
                    IsPublic = item.IsPublic,
                    ViewRoles = viewRolesJson,
                    AssigneeId = item.AssigneeId,
                    AssigneeName = item.AssigneeName,
                    DeptId = item.DeptId,
                    DeptName = item.DeptName,
                    PlanFinishDate = item.PlanFinishDate,
                    Remark = item.Remark
                };
                _db.ProjectFileItems.Add(entity);
            }
        }
        await _db.SaveChangesAsync();
        await LogOp(id, "更新文件清单", "保存文件资料清单");
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPost("{id:long}/file-items/{itemId:long}/upload")]
    public async Task<IActionResult> UploadFileItem(long id, long itemId, List<IFormFile> files, [FromForm] string? remark, CancellationToken cancellationToken)
    {
        var (userId, realName) = GetUserInfo();
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (project == null) return Ok(new { code = 404, message = "项目不存在" });

        var item = await _db.ProjectFileItems.FirstOrDefaultAsync(f => f.Id == itemId && f.ProjectId == id);
        if (item == null) return Ok(new { code = 404, message = "文件项不存在" });

        // 数据校验：项目经理 或 文件责任人
        bool isProjectManager = project.ProjectManagerId == userId;
        bool isAssignee = item.AssigneeId == userId;
        if (!isProjectManager && !isAssignee)
            return Forbid();

        if (files == null || files.Count == 0)
            return Ok(new { code = 400, message = "请选择至少一个文件" });

        // 查询最大版本号
        var maxVersion = await _db.ProjectFileVersions
            .Where(v => v.ProjectFileItemId == itemId)
            .MaxAsync(v => (int?)v.VersionNumber) ?? 0;

        // 创建版本记录
        var version = new ProjectFileVersion
        {
            ProjectFileItemId = itemId,
            VersionNumber = maxVersion + 1,
            UploadedBy = userId,
            UploadedByName = realName,
            UploadedAt = DateTime.Now,
            Remark = remark
        };
        _db.ProjectFileVersions.Add(version);
        await _db.SaveChangesAsync(); // 先保存以获取 version.Id

        // 保存每个物理文件
        var relativeDir = Path.Combine("ProjectFiles", id.ToString());
        var uploadDir = Path.Combine(_env.ContentRootPath, relativeDir);
        Directory.CreateDirectory(uploadDir);

        var savedFiles = new List<object>();
        var addedFiles = new List<ProjectFileVersionFile>();
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadDir, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var versionFile = new ProjectFileVersionFile
            {
                ProjectFileVersionId = version.Id,
                FilePath = $"{relativeDir}/{fileName}",
                OriginalFileName = file.FileName,
                FileSize = file.Length,
                FileExt = ext
            };
            _db.ProjectFileVersionFiles.Add(versionFile);
            addedFiles.Add(versionFile);
        }
        await _db.SaveChangesAsync();

        // SaveChangesAsync 后 ID 由数据库生成，再从追踪实体读取
        foreach (var vf in addedFiles)
        {
            savedFiles.Add(new
            {
                id = vf.Id,
                originalFileName = vf.OriginalFileName,
                fileSize = vf.FileSize,
                fileExt = vf.FileExt
            });
        }

        // 更新 LatestVersionId
        item.LatestVersionId = version.Id;
        await _db.SaveChangesAsync();

        await LogOp(id, "上传文件", $"上传文件「{item.FileName}」v{version.VersionNumber}（{files.Count}个文件）");
        return Ok(new { code = 0, message = "success", data = new
        {
            versionId = version.Id,
            versionNumber = version.VersionNumber,
            files = savedFiles,
            uploadedByName = version.UploadedByName,
            uploadedAt = version.UploadedAt
        }});
    }

    [HttpGet("{id:long}/file-items/{itemId:long}/download")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadFileItem(long id, long itemId, [FromQuery] int? version, [FromQuery] long? fileId)
    {
        // 认证由 JwtBearer 中间件统一处理（OnMessageReceived 从 ?token= 提取）
        var (userId, _) = GetUserInfo();
        if (userId == 0) return Unauthorized();
        var codes = await ProjectPermissionHelper.GetUserPermissionCodesAsync(_db, userId);
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (project == null) return Ok(new { code = 404, message = "项目不存在" });

        var item = await _db.ProjectFileItems.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == itemId && f.ProjectId == id);
        if (item == null) return Ok(new { code = 404, message = "文件项不存在" });

        var memberIds = await _db.ProjectMembers.Where(m => m.ProjectId == id).Select(m => m.MemberId).ToListAsync();
        var canViewPublic = codes.Contains("project:file:view:public");
        var canViewNonPublic = codes.Contains("project:file:view:nonpublic");

        if (!CanViewFile(item.IsPublic, item.ViewRoles, item.AssigneeId, project, userId, memberIds, canViewPublic, canViewNonPublic))
            return Forbid();

        // 如果指定了 fileId，直接下载该文件
        if (fileId.HasValue)
        {
            var file = await _db.ProjectFileVersionFiles
                .Include(f => f.Version)
                .FirstOrDefaultAsync(f => f.Id == fileId.Value);
            if (file == null) return Ok(new { code = 404, message = "文件不存在" });
            if (file.Version.ProjectFileItemId != itemId) return Ok(new { code = 404, message = "文件不属于该文件项" });

            var fileFullPath = Path.Combine(_env.ContentRootPath, file.FilePath);
            if (!System.IO.File.Exists(fileFullPath))
                return Ok(new { code = 404, message = "物理文件不存在" });

            var fileStream = new FileStream(fileFullPath, FileMode.Open, FileAccess.Read);
            var downloadName = !string.IsNullOrEmpty(file.OriginalFileName) ? file.OriginalFileName : item.FileName;
            return File(fileStream, "application/octet-stream", downloadName);
        }

        // 未指定 fileId：查找指定版本
        ProjectFileVersion? fileVersion;
        if (version.HasValue)
        {
            fileVersion = await _db.ProjectFileVersions
                .Include(v => v.Files)
                .FirstOrDefaultAsync(v => v.ProjectFileItemId == itemId && v.VersionNumber == version.Value);
            if (fileVersion == null) return Ok(new { code = 404, message = "指定版本不存在" });
        }
        else
        {
            if (!item.LatestVersionId.HasValue) return Ok(new { code = 404, message = "文件尚未上传" });
            fileVersion = await _db.ProjectFileVersions
                .Include(v => v.Files)
                .FirstOrDefaultAsync(v => v.Id == item.LatestVersionId.Value);
            if (fileVersion == null) return Ok(new { code = 404, message = "文件版本不存在" });
        }

        // 取该版本第一个文件下载（兼容旧行为）
        var firstFile = fileVersion.Files.OrderBy(f => f.Id).FirstOrDefault();
        if (firstFile == null) return Ok(new { code = 404, message = "该版本没有文件" });

        var fullPath = Path.Combine(_env.ContentRootPath, firstFile.FilePath);
        if (!System.IO.File.Exists(fullPath))
            return Ok(new { code = 404, message = "物理文件不存在" });

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
        var name = !string.IsNullOrEmpty(firstFile.OriginalFileName) ? firstFile.OriginalFileName : item.FileName;
        return File(stream, "application/octet-stream", name);
    }

    [HttpGet("{id:long}/file-items/{itemId:long}/versions")]
    public async Task<IActionResult> GetFileVersions(long id, long itemId)
    {
        var (userId, _) = GetUserInfo();
        var codes = await ProjectPermissionHelper.GetUserPermissionCodesAsync(_db, userId);
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (project == null) return Ok(new { code = 404, message = "项目不存在" });

        var item = await _db.ProjectFileItems.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == itemId && f.ProjectId == id);
        if (item == null) return Ok(new { code = 404, message = "文件项不存在" });

        var memberIds = await _db.ProjectMembers.Where(m => m.ProjectId == id).Select(m => m.MemberId).ToListAsync();
        var canViewPublic = codes.Contains("project:file:view:public");
        var canViewNonPublic = codes.Contains("project:file:view:nonpublic");

        if (!CanViewFile(item.IsPublic, item.ViewRoles, item.AssigneeId, project, userId, memberIds, canViewPublic, canViewNonPublic))
            return Ok(new { code = 0, message = "success", data = Array.Empty<object>() });

        var versions = await _db.ProjectFileVersions
            .Where(v => v.ProjectFileItemId == itemId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new
            {
                v.Id,
                v.VersionNumber,
                v.UploadedByName,
                v.UploadedAt,
                v.Remark,
                Files = v.Files.OrderBy(f => f.Id).Select(f => new
                {
                    f.Id,
                    f.OriginalFileName,
                    f.FileSize,
                    f.FileExt
                }).ToList()
            })
            .ToListAsync();

        return Ok(new { code = 0, message = "success", data = versions });
    }

    [HttpDelete("{id:long}/file-items/{itemId:long}")]
    public async Task<IActionResult> DeleteFileItem(long id, long itemId)
    {
        var denied = await ForbidIfCannotMaintainAsync(id);
        if (denied != null) return denied;

        var item = await _db.ProjectFileItems
            .Include(f => f.Versions)
                .ThenInclude(v => v.Files)
            .FirstOrDefaultAsync(f => f.Id == itemId && f.ProjectId == id);
        if (item == null) return Ok(new { code = 404, message = "文件项不存在" });

        int versionCount = item.Versions.Count;

        if (item.Required)
        {
            // 必填项：删除所有版本及物理文件，但保留清单项
            foreach (var v in item.Versions)
            {
                foreach (var vf in v.Files)
                {
                    var fullPath = Path.Combine(_env.ContentRootPath, vf.FilePath);
                    if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                }
            }
            _db.ProjectFileVersions.RemoveRange(item.Versions);
            item.LatestVersionId = null;
        }
        else
        {
            // 非必填项：删除清单项及其所有版本
            foreach (var v in item.Versions)
            {
                foreach (var vf in v.Files)
                {
                    var fullPath = Path.Combine(_env.ContentRootPath, vf.FilePath);
                    if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                }
            }
            // 分两步删除，避免 LatestVersionId+Versions 级联循环引用
            item.LatestVersionId = null;
            await _db.SaveChangesAsync();
            _db.ProjectFileItems.Remove(item);
        }
        await _db.SaveChangesAsync();

        var action = item.Required ? "清除文件版本" : "删除文件项";
        await LogOp(id, action, $"删除文件「{item.FileName}」{(versionCount > 0 ? $"(含{versionCount}个版本)" : "")}");

        return Ok(new { code = 0, message = "success" });
    }

    // ────────── 文件资料：重置状态 ──────────

    [HttpPost("{id:long}/file-items/{itemId:long}/reset")]
    public async Task<IActionResult> ResetFileItem(long id, long itemId)
    {
        var (userId, _) = GetUserInfo();

        // 仅系统管理员可重置
        if (!await ProjectPermissionHelper.IsAdminAsync(_db, userId))
            return Forbid();

        var item = await _db.ProjectFileItems
            .FirstOrDefaultAsync(f => f.Id == itemId && f.ProjectId == id);
        if (item == null) return Ok(new { code = 404, message = "文件项不存在" });

        if (!item.LatestVersionId.HasValue)
            return Ok(new { code = 400, message = "文件项状态为待上传，无需重置" });

        item.LatestVersionId = null;
        await _db.SaveChangesAsync();

        await LogOp(id, "文件管理", $"重置文件「{item.FileName}」状态为待上传");

        return Ok(new { code = 0, message = "success" });
    }

    // ────────── 操作日志 ──────────

    [HttpGet("{id:long}/operation-logs")]
    public async Task<IActionResult> GetOperationLogs(
        long id,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] long? deptId,
        [FromQuery] long? userId)
    {
        var query = _db.OperationLogs.Where(l => l.ProjectId == id);
        if (startDate.HasValue) query = query.Where(l => l.CreatedAt >= startDate.Value);
        if (endDate.HasValue) query = query.Where(l => l.CreatedAt < endDate.Value.AddDays(1));
        if (userId.HasValue) query = query.Where(l => l.UserId == userId.Value);
        if (deptId.HasValue)
        {
            var deptUserIds = await _db.Users.Where(u => u.DepartmentId == deptId.Value).Select(u => u.Id).ToListAsync();
            query = query.Where(l => deptUserIds.Contains(l.UserId));
        }
        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new { l.Id, l.UserId, l.UserName, l.Operation, l.Content, l.CreatedAt })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = logs });
    }

    // ────────── 文件管理（全局视图）──────────

    /// <summary>获取当前用户所在激活项目中责任人为当前用户的文件资料</summary>
    [HttpGet("files/my")]
    public async Task<IActionResult> GetMyFiles()
    {
        var (userId, _) = GetUserInfo();

        // 用户所在且状态为激活(1)的项目ID
        var myProjectIds = await _db.ProjectMembers
            .Where(m => m.MemberId == userId)
            .Select(m => m.ProjectId)
            .Distinct()
            .ToListAsync();

        var myActiveProjectIds = await _db.Projects
            .Where(p => myProjectIds.Contains(p.Id) && p.Status == 1)
            .Select(p => p.Id)
            .ToListAsync();

        if (myActiveProjectIds.Count == 0)
            return Ok(new { code = 0, message = "success", data = Array.Empty<object>() });

        var items = await _db.ProjectFileItems
            .Where(f => myActiveProjectIds.Contains(f.ProjectId))
            .Where(f => f.AssigneeId == userId)
            .OrderBy(f => f.Project.ProjectCode)
            .ThenBy(f => f.SortOrder)
            .Select(f => new
            {
                f.Id,
                f.ProjectId,
                ProjectCode = f.Project.ProjectCode,
                ProjectName = f.Project.ProjectName,
                f.SortOrder,
                f.FileName,
                f.Required,
                f.IsPublic,
                f.AssigneeId,
                f.AssigneeName,
                f.DeptId,
                f.DeptName,
                f.PlanFinishDate,
                f.LatestVersionId,
                f.Remark,
                VersionCount = f.Versions.Count,
                LatestVersion = f.LatestVersion != null
                    ? new
                    {
                        f.LatestVersion.Id,
                        f.LatestVersion.VersionNumber,
                        f.LatestVersion.UploadedByName,
                        f.LatestVersion.UploadedAt,
                        Files = f.LatestVersion.Files
                            .OrderBy(vf => vf.Id)
                            .Select(vf => new { vf.Id, vf.OriginalFileName, vf.FileSize, vf.FileExt })
                            .ToList()
                    }
                    : null
            })
            .ToListAsync();

        var today = DateTime.Today;
        var result = items.Select(f =>
        {
            string? planFinishStatus = null;
            if (f.PlanFinishDate.HasValue && f.LatestVersionId.HasValue)
            {
                var planDate = f.PlanFinishDate.Value;
                if (today > planDate) planFinishStatus = "overdue";
                else if ((planDate - today).TotalDays <= 7) planFinishStatus = "expiring";
                else planFinishStatus = "normal";
            }

            return new
            {
                f.Id,
                f.ProjectId,
                f.ProjectCode,
                f.ProjectName,
                f.SortOrder,
                f.FileName,
                f.Required,
                f.IsPublic,
                f.AssigneeId,
                f.AssigneeName,
                f.DeptId,
                f.DeptName,
                f.PlanFinishDate,
                PlanFinishStatus = planFinishStatus,
                HasUpload = f.LatestVersionId.HasValue,
                LatestVersion = f.LatestVersion,
                VersionCount = f.VersionCount,
                f.Remark
            };
        });

        return Ok(new { code = 0, message = "success", data = result });
    }

    /// <summary>当前用户未上传文件统计（总数, 必须数），给侧边栏菜单角标使用</summary>
    [HttpGet("files/my/pending-count")]
    public async Task<IActionResult> GetMyPendingFileCount()
    {
        var (userId, _) = GetUserInfo();
        var myMemberProjectIds = await _db.ProjectMembers
            .Where(m => m.MemberId == userId)
            .Select(m => m.ProjectId)
            .Distinct()
            .ToListAsync();
        var myActiveProjectIds = await _db.Projects
            .Where(p => myMemberProjectIds.Contains(p.Id) && p.Status == 1)
            .Select(p => p.Id)
            .ToListAsync();
        if (myActiveProjectIds.Count == 0)
            return Ok(new { code = 0, message = "success", data = new { total = 0, required = 0 } });

        var query = _db.ProjectFileItems
            .Where(f => myActiveProjectIds.Contains(f.ProjectId))
            .Where(f => f.AssigneeId == userId)
            .Where(f => f.LatestVersionId == null);

        var total = await query.CountAsync();
        var required = await query.Where(f => f.Required).CountAsync();

        return Ok(new { code = 0, message = "success", data = new { total, required } });
    }

    // ────────── 辅助 ──────────

    private static bool CanViewFile(bool isPublic, string? viewRoles, long? assigneeId, Project project, long userId, List<long?> memberIds, bool canViewPublic, bool canViewNonPublic)
    {
        // 公开文件：用户有「公开」权限即可查看
        if (isPublic && canViewPublic) return true;

        // 非公开文件
        if (!isPublic)
        {
            // 用户有「非公开」权限 → 所有非公开文件可见
            if (canViewNonPublic) return true;

            // 用户无「非公开」权限 → 按文件的实际权限配置 (项目经理/项目组成员/责任人) 判定
            if (!string.IsNullOrEmpty(viewRoles))
            {
                var roles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(viewRoles) ?? new();
                if (roles.Count == 0) return false;
                var isPm = project.ProjectManagerId == userId;
                var isMember = memberIds.Contains(userId);
                var isAssignee = assigneeId == userId;
                if (roles.Contains("pm") && isPm) return true;
                if (roles.Contains("member") && isMember) return true;
                if (roles.Contains("assignee") && isAssignee) return true;
            }
        }
        return false;
    }

    private (long userId, string realName) GetUserInfo()
    {
        var realName = User.FindFirst("realName")?.Value ?? "";
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(userIdClaim, out var userId);
        return (userId, realName);
    }

    private async Task<IActionResult?> ForbidIfCannotMaintainAsync(long projectId)
    {
        var (userId, _) = GetUserInfo();
        if (!await ProjectDataScopeResolver.CanMaintainProjectAsync(_db, projectId, userId))
            return Forbid();
        return null;
    }

    private async Task<IActionResult?> ForbidIfCannotAccessAsync(long projectId)
    {
        var (userId, _) = GetUserInfo();
        var codes = await ProjectPermissionHelper.GetUserPermissionCodesAsync(_db, userId);
        if (!await ProjectPermissionHelper.CanAccessProjectAsync(_db, projectId, userId, codes))
            return Forbid();
        return null;
    }

    private async Task LogOp(long projectId, string operation, string content)
    {
        var (userId, realName) = GetUserInfo();
        _db.OperationLogs.Add(new OperationLog
        {
            ProjectId = projectId,
            UserId = userId,
            UserName = realName,
            Operation = operation,
            Content = content,
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();
    }

    private static string StatusLabel(int s) => new[] { "未激活", "进行中", "已完成", "暂停" }[s >= 0 && s < 4 ? s : 0];

    private static void Compare(List<string> diffs, string label, string? oldVal, string? newVal)
    {
        if ((oldVal ?? "") != (newVal ?? ""))
            diffs.Add($"【{label}】{(string.IsNullOrEmpty(oldVal) ? "(空)" : oldVal)} → {(string.IsNullOrEmpty(newVal) ? "(空)" : newVal)}");
    }

    private static void CompareDate(List<string> diffs, string label, DateTime? oldVal, DateTime? newVal)
    {
        var o = oldVal?.ToString("yyyy-MM-dd") ?? "";
        var n = newVal?.ToString("yyyy-MM-dd") ?? "";
        if (o != n) diffs.Add($"【{label}】{(string.IsNullOrEmpty(o) ? "(空)" : o)} → {(string.IsNullOrEmpty(n) ? "(空)" : n)}");
    }

    private static void CompareInt<T>(List<string> diffs, string label, T? oldVal, T? newVal, Func<T, string> fmt) where T : struct
    {
        var o = oldVal.HasValue ? fmt(oldVal.Value) : "";
        var n = newVal.HasValue ? fmt(newVal.Value) : "";
        if (o != n) diffs.Add($"【{label}】{(string.IsNullOrEmpty(o) ? "(空)" : o)} → {(string.IsNullOrEmpty(n) ? "(空)" : n)}");
    }

    private static void CompareLong(List<string> diffs, string label, long? oldVal, long? newVal, Func<long, string> fmt)
    {
        var o = oldVal.HasValue ? fmt(oldVal.Value) : "";
        var n = newVal.HasValue ? fmt(newVal.Value) : "";
        if (o != n) diffs.Add($"【{label}】{(string.IsNullOrEmpty(o) ? "(空)" : o)} → {(string.IsNullOrEmpty(n) ? "(空)" : n)}");
    }
}

// ────────── Request Models ──────────

public class CreateProjectRequest
{
    public string ProjectCode { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string? ProjectType { get; set; }
    public string? ContractCode { get; set; }
    public string? EngineeringCenter { get; set; }
    public string? CategoryCode { get; set; }
    public string? CustomerName { get; set; }
    public long? RegionalManagerId { get; set; }
    public string? RegionalManagerName { get; set; }
    public string? CustomerContactPhone { get; set; }
    public string? CustomerContactEmail { get; set; }
    public long? SalesManagerId { get; set; }
    public string? SalesManagerName { get; set; }
    public long? PreSalesManagerId { get; set; }
    public string? PreSalesManagerName { get; set; }
    public string? SalesRegion { get; set; }
    public long? ProjectManagerId { get; set; }
    public string? ProjectManagerName { get; set; }
    public string? PmCenter { get; set; }
    public string? OwnerContactPhone { get; set; }
    public string? BusinessContactEmail { get; set; }
    public DateTime? PlanStartDate { get; set; }
    public DateTime? RequiredDelivery { get; set; }
    public DateTime? AcceptedDelivery { get; set; }
    public string? DeliveryLocation { get; set; }
    public string? FinalCustomer { get; set; }
    public string? ProjectScope { get; set; }
    public string? SpecialTerms { get; set; }
    public string? Remark { get; set; }
    public string? ProgressDesc { get; set; }
    public List<long>? UploadedFileIds { get; set; }
    public List<ProductInput> Products { get; set; } = new();
}

public class UpdateProjectRequest : CreateProjectRequest
{
    public DateTime? ActualFinishDate { get; set; }
    public string? QualityStrategy { get; set; }
    public string? ProjectDelivery { get; set; }
    public string? ReportContent { get; set; }
    public string? RiskStatus { get; set; }
    public DateTime? CurrentPhaseDate { get; set; }
    public string? NextStatus { get; set; }
}

public class CreateTasksFromTemplateRequest { public long TemplateId { get; set; } }
public class CopyProjectRequest { public string? NewProjectCode { get; set; } }
public class SaveProjectMembersRequest { public List<MemberInput> Members { get; set; } = new(); }
public class SaveProjectMilestonesRequest { public List<MilestoneInput> Milestones { get; set; } = new(); }
public class SavePlanReceiptsRequest { public List<PlanReceiptInput> Records { get; set; } = new(); }
public class SaveReceiptsRequest { public List<ReceiptInput> Records { get; set; } = new(); }
public class SaveInvoicesRequest { public List<InvoiceInput> Records { get; set; } = new(); }

public class SaveProjectFileItemsRequest
{
    public List<FileItemInput> Items { get; set; } = new();
}

public class FileItemInput
{
    public long? Id { get; set; }
    public long? TemplateItemId { get; set; }
    public int SortOrder { get; set; }
    public string FileName { get; set; } = "";
    public bool Required { get; set; }
    public bool IsPublic { get; set; } = true;
    public List<string>? ViewRoles { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public long? DeptId { get; set; }
    public string? DeptName { get; set; }
    public DateTime? PlanFinishDate { get; set; }
    public string? Remark { get; set; }
}
