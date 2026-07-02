using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectManagement.API.Auth;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Infrastructure.Data;

namespace ProjectManagement.API.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class IssuesController : ControllerBase
{
    private readonly AppDbContext _db;

    public IssuesController(AppDbContext db) => _db = db;

    private (long userId, string realName) GetUserInfo()
    {
        var realName = User.FindFirst("realName")?.Value ?? "";
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(userIdClaim, out var userId);
        return (userId, realName);
    }

    private async Task<IActionResult?> ForbidIfCannotAccessProjectAsync(long projectId)
    {
        var (userId, _) = GetUserInfo();
        var codes = await ProjectPermissionHelper.GetUserPermissionCodesAsync(_db, userId);
        if (!await ProjectPermissionHelper.CanAccessProjectAsync(_db, projectId, userId, codes))
            return Forbid();
        return null;
    }

    // ════════════════════════════════════════════════════════════════
    // 项目内接口
    // ════════════════════════════════════════════════════════════════

    [HttpGet("projects/{projectId:long}/issues")]
    [RequirePermission("issue:list")]
    public async Task<IActionResult> GetList(long projectId,
        [FromQuery] string? keyword, [FromQuery] string? issueType, [FromQuery] string? issueSource,
        [FromQuery] string? severity, [FromQuery] string? priority, [FromQuery] int? status,
        [FromQuery] long? assigneeId, [FromQuery] long? submitterId, [FromQuery] long? responsibleDeptId,
        [FromQuery] string? startDate, [FromQuery] string? endDate,
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
    {
        var denied = await ForbidIfCannotAccessProjectAsync(projectId);
        if (denied != null) return denied;

        var query = _db.ProjectIssues.Where(i => i.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(i => i.Title.Contains(keyword) || i.IssueCode.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(issueType))
            query = query.Where(i => i.IssueType == issueType);
        if (!string.IsNullOrWhiteSpace(issueSource))
            query = query.Where(i => i.IssueSource == issueSource);
        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(i => i.Severity == severity);
        if (!string.IsNullOrWhiteSpace(priority))
            query = query.Where(i => i.Priority == priority);
        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);
        if (assigneeId.HasValue)
            query = query.Where(i => i.AssigneeId == assigneeId.Value);
        if (submitterId.HasValue)
            query = query.Where(i => i.SubmitterId == submitterId.Value);
        if (responsibleDeptId.HasValue)
            query = query.Where(i => i.ResponsibleDeptId == responsibleDeptId.Value);
        if (DateOnly.TryParse(startDate, out var sd))
            query = query.Where(i => i.DiscoveredDate >= sd);
        if (DateOnly.TryParse(endDate, out var ed))
            query = query.Where(i => i.DiscoveredDate <= ed);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(i => i.CreatedAt)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return Ok(new { code = 0, message = "success", data = new { items, total, pageIndex, pageSize } });
    }

    [HttpGet("projects/{projectId:long}/issues/{id:long}")]
    [RequirePermission("issue:list")]
    public async Task<IActionResult> GetDetail(long projectId, long id)
    {
        var denied = await ForbidIfCannotAccessProjectAsync(projectId);
        if (denied != null) return denied;

        var issue = await _db.ProjectIssues
            .Include(i => i.Measures.OrderBy(m => m.SortOrder))
            .FirstOrDefaultAsync(i => i.Id == id && i.ProjectId == projectId);
        if (issue == null) return Ok(new { code = 404, message = "问题不存在" });
        return Ok(new { code = 0, message = "success", data = issue });
    }

    [HttpPost("projects/{projectId:long}/issues")]
    [RequirePermission("issue:create")]
    public async Task<IActionResult> Create(long projectId, [FromBody] CreateIssueRequest req)
    {
        var denied = await ForbidIfCannotAccessProjectAsync(projectId);
        if (denied != null) return denied;

        var (userId, realName) = GetUserInfo();
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) return Ok(new { code = 404, message = "项目不存在" });

        // 生成问题编号：{项目编号}-ISS-{序号}
        var prefix = await _db.SysParams.Where(p => p.ParamKey == "issue_code_prefix")
            .Select(p => p.ParamValue).FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(prefix)) prefix = "ISS";
        var existingCount = await _db.ProjectIssues.CountAsync(i => i.ProjectId == projectId);
        var seq = (existingCount + 1).ToString().PadLeft(3, '0');
        var issueCode = $"{project.ProjectCode}-{prefix}-{seq}";

        var issue = new ProjectIssue
        {
            ProjectId = projectId,
            IssueCode = issueCode,
            Title = req.Title,
            Description = req.Description,
            IssueSource = req.IssueSource,
            IssueType = req.IssueType,
            Severity = req.Severity,
            Priority = req.Priority,
            CauseAnalysis = req.CauseAnalysis,
            DiscoveredDate = req.DiscoveredDate,
            PlannedDate = req.PlannedDate,
            ResponsibleDeptId = req.ResponsibleDeptId,
            ResponsibleDeptName = req.ResponsibleDeptName,
            AssigneeId = req.AssigneeId,
            AssigneeName = req.AssigneeName,
            SubmitterId = req.SubmitterId,
            SubmitterName = req.SubmitterName,
            CreatorId = userId,
            CreatorName = realName,
            VerifierId = req.VerifierId,
            VerifierName = req.VerifierName,
            VerifiedDate = req.VerifiedDate,
            Status = 0,
            ReopenCount = 0,
            CreatedAt = DateTime.UtcNow,
            Measures = req.Measures.Select((m, i) => new ProjectIssueMeasure
            {
                SortOrder = m.SortOrder > 0 ? m.SortOrder : (i + 1),
                Measure = m.Measure,
                MeasureType = m.MeasureType,
                ResponsibleDeptId = m.ResponsibleDeptId,
                ResponsibleDeptName = m.ResponsibleDeptName,
                ResponsibleUserId = m.ResponsibleUserId,
                ResponsibleUserName = m.ResponsibleUserName,
                Remark = m.Remark,
                PlannedDate = m.PlannedDate,
            }).ToList()
        };

        _db.ProjectIssues.Add(issue);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success", data = new { issue.Id, issue.IssueCode } });
    }

    [HttpPut("projects/{projectId:long}/issues/{id:long}")]
    [RequirePermission("issue:edit")]
    public async Task<IActionResult> Update(long projectId, long id, [FromBody] CreateIssueRequest req)
    {
        var denied = await ForbidIfCannotAccessProjectAsync(projectId);
        if (denied != null) return denied;

        var issue = await _db.ProjectIssues
            .Include(i => i.Measures)
            .FirstOrDefaultAsync(i => i.Id == id && i.ProjectId == projectId);
        if (issue == null) return Ok(new { code = 404, message = "问题不存在" });

        issue.Title = req.Title;
        issue.Description = req.Description;
        issue.IssueSource = req.IssueSource;
        issue.IssueType = req.IssueType;
        issue.Severity = req.Severity;
        issue.Priority = req.Priority;
        issue.CauseAnalysis = req.CauseAnalysis;
        issue.DiscoveredDate = req.DiscoveredDate;
        issue.PlannedDate = req.PlannedDate;
        issue.ResponsibleDeptId = req.ResponsibleDeptId;
        issue.ResponsibleDeptName = req.ResponsibleDeptName;
        issue.AssigneeId = req.AssigneeId;
        issue.AssigneeName = req.AssigneeName;
        issue.SubmitterId = req.SubmitterId;
        issue.SubmitterName = req.SubmitterName;
        issue.VerifierId = req.VerifierId;
        issue.VerifierName = req.VerifierName;
        issue.VerifiedDate = req.VerifiedDate;
        issue.UpdatedAt = DateTime.UtcNow;

        // 措施：全量替换
        _db.ProjectIssueMeasures.RemoveRange(issue.Measures);
        issue.Measures = req.Measures.Select((m, i) => new ProjectIssueMeasure
        {
            IssueId = id,
            SortOrder = m.SortOrder > 0 ? m.SortOrder : (i + 1),
            Measure = m.Measure,
            MeasureType = m.MeasureType,
            ResponsibleDeptId = m.ResponsibleDeptId,
            ResponsibleDeptName = m.ResponsibleDeptName,
            ResponsibleUserId = m.ResponsibleUserId,
            ResponsibleUserName = m.ResponsibleUserName,
            Remark = m.Remark,
            PlannedDate = m.PlannedDate,
        }).ToList();

        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    [HttpDelete("projects/{projectId:long}/issues/{id:long}")]
    [RequirePermission("issue:delete")]
    public async Task<IActionResult> Delete(long projectId, long id)
    {
        var denied = await ForbidIfCannotAccessProjectAsync(projectId);
        if (denied != null) return denied;

        var issue = await _db.ProjectIssues
            .Include(i => i.Measures)
            .FirstOrDefaultAsync(i => i.Id == id && i.ProjectId == projectId);
        if (issue == null) return Ok(new { code = 404, message = "问题不存在" });

        _db.ProjectIssueMeasures.RemoveRange(issue.Measures);
        _db.ProjectIssues.Remove(issue);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPut("projects/{projectId:long}/issues/{id:long}/status")]
    [RequirePermission("issue:edit")]
    public async Task<IActionResult> UpdateStatus(long projectId, long id, [FromBody] UpdateIssueStatusRequest req)
    {
        var denied = await ForbidIfCannotAccessProjectAsync(projectId);
        if (denied != null) return denied;

        var issue = await _db.ProjectIssues.FirstOrDefaultAsync(i => i.Id == id && i.ProjectId == projectId);
        if (issue == null) return Ok(new { code = 404, message = "问题不存在" });

        if (req.Status == 1 && issue.Status == 0)
        {
            // 待处理 → 处理中
            issue.Status = 1;
        }
        else if (req.Status == 2 && issue.Status == 1)
        {
            // 处理中 → 已完成
            issue.Status = 2;
            if (req.VerifierId.HasValue) issue.VerifierId = req.VerifierId;
            if (!string.IsNullOrWhiteSpace(req.VerifierName)) issue.VerifierName = req.VerifierName;
            if (req.VerifiedDate.HasValue) issue.VerifiedDate = req.VerifiedDate;
        }
        else if (req.Status == 1 && issue.Status == 2)
        {
            // 已完成 → 处理中（重新打开）
            issue.Status = 1;
            issue.ReopenCount++;
        }
        else
        {
            return Ok(new { code = 400, message = "无效的状态变更" });
        }

        if (!string.IsNullOrWhiteSpace(req.CauseAnalysis))
            issue.CauseAnalysis = req.CauseAnalysis;
        issue.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    // ════════════════════════════════════════════════════════════════
    // 全局接口（跨项目个人视图）
    // ════════════════════════════════════════════════════════════════

    [HttpGet("issues/my")]
    [RequirePermission("issue:list")]
    public async Task<IActionResult> GetMyIssues(
        [FromQuery] string? keyword, [FromQuery] string? issueType, [FromQuery] string? issueSource,
        [FromQuery] string? severity, [FromQuery] string? priority, [FromQuery] int? status,
        [FromQuery] long? projectId,
        [FromQuery] string? startDate, [FromQuery] string? endDate,
        [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
    {
        var (userId, _) = GetUserInfo();

        var query = _db.ProjectIssues.AsQueryable();

        // 仅当前用户相关的问题
        query = query.Where(i => i.AssigneeId == userId
                              || i.SubmitterId == userId
                              || i.CreatorId == userId
                              || i.VerifierId == userId);

        // 叠加项目数据范围
        var codes = await ProjectPermissionHelper.GetUserPermissionCodesAsync(_db, userId);
        if (!codes.Contains("admin") && !codes.Contains("project_admin"))
        {
            // 非管理员：过滤掉无访问权限的项目
            var userDeptId = await _db.Users.Where(u => u.Id == userId).Select(u => u.DepartmentId).FirstOrDefaultAsync();
            if (userDeptId.HasValue)
            {
                query = query.Where(i => _db.Projects.Any(p => p.Id == i.ProjectId && (
                    p.ProjectManagerId == userId || p.CreatedBy == userId ||
                    p.Members.Any(m => m.MemberId == userId))));
            }
        }

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(i => i.Title.Contains(keyword) || i.IssueCode.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(issueType))
            query = query.Where(i => i.IssueType == issueType);
        if (!string.IsNullOrWhiteSpace(issueSource))
            query = query.Where(i => i.IssueSource == issueSource);
        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(i => i.Severity == severity);
        if (!string.IsNullOrWhiteSpace(priority))
            query = query.Where(i => i.Priority == priority);
        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);
        if (projectId.HasValue)
            query = query.Where(i => i.ProjectId == projectId.Value);
        if (DateOnly.TryParse(startDate, out var sd))
            query = query.Where(i => i.DiscoveredDate >= sd);
        if (DateOnly.TryParse(endDate, out var ed))
            query = query.Where(i => i.DiscoveredDate <= ed);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(i => i.CreatedAt)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Join(_db.Projects, i => i.ProjectId, p => p.Id, (i, p) => new
            {
                i.Id, i.IssueCode, i.Title, i.IssueSource, i.IssueType, i.Severity, i.Priority,
                i.Status, i.DiscoveredDate, i.PlannedDate,
                i.AssigneeId, i.AssigneeName,
                i.SubmitterId, i.SubmitterName,
                i.CreatorId, i.CreatorName,
                i.ResponsibleDeptId, i.ResponsibleDeptName,
                i.CauseAnalysis, i.ReopenCount,
                i.VerifierId, i.VerifierName, i.VerifiedDate,
                i.CreatedAt, i.UpdatedAt,
                ProjectId = p.Id, ProjectCode = p.ProjectCode, ProjectName = p.ProjectName
            })
            .ToListAsync();

        return Ok(new { code = 0, message = "success", data = new { items, total, pageIndex, pageSize } });
    }

    [HttpGet("issues/my/count")]
    public async Task<IActionResult> GetMyIssueCount()
    {
        var (userId, _) = GetUserInfo();
        var count = await _db.ProjectIssues
            .Where(i => i.Status != 2)
            .Where(i => i.AssigneeId == userId
                     || i.SubmitterId == userId
                     || i.CreatorId == userId
                     || i.VerifierId == userId)
            .CountAsync();
        return Ok(new { code = 0, message = "success", data = new { count } });
    }

    [HttpPost("issues")]
    [RequirePermission("issue:create")]
    public async Task<IActionResult> CreateIssue([FromBody] CreateGlobalIssueRequest req)
    {
        var denied = await ForbidIfCannotAccessProjectAsync(req.ProjectId);
        if (denied != null) return denied;

        var (userId, realName) = GetUserInfo();
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == req.ProjectId);
        if (project == null) return Ok(new { code = 404, message = "项目不存在" });

        var prefix = await _db.SysParams.Where(p => p.ParamKey == "issue_code_prefix")
            .Select(p => p.ParamValue).FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(prefix)) prefix = "ISS";
        var existingCount = await _db.ProjectIssues.CountAsync(i => i.ProjectId == req.ProjectId);
        var seq = (existingCount + 1).ToString().PadLeft(3, '0');
        var issueCode = $"{project.ProjectCode}-{prefix}-{seq}";

        var issue = new ProjectIssue
        {
            ProjectId = req.ProjectId,
            IssueCode = issueCode,
            Title = req.Title,
            Description = req.Description,
            IssueSource = req.IssueSource,
            IssueType = req.IssueType,
            Severity = req.Severity,
            Priority = req.Priority,
            CauseAnalysis = req.CauseAnalysis,
            DiscoveredDate = req.DiscoveredDate,
            PlannedDate = req.PlannedDate,
            ResponsibleDeptId = req.ResponsibleDeptId,
            ResponsibleDeptName = req.ResponsibleDeptName,
            AssigneeId = req.AssigneeId,
            AssigneeName = req.AssigneeName,
            SubmitterId = req.SubmitterId,
            SubmitterName = req.SubmitterName,
            CreatorId = userId,
            CreatorName = realName,
            VerifierId = req.VerifierId,
            VerifierName = req.VerifierName,
            VerifiedDate = req.VerifiedDate,
            Status = 0,
            ReopenCount = 0,
            CreatedAt = DateTime.UtcNow,
            Measures = req.Measures.Select((m, i) => new ProjectIssueMeasure
            {
                SortOrder = m.SortOrder > 0 ? m.SortOrder : (i + 1),
                Measure = m.Measure,
                MeasureType = m.MeasureType,
                ResponsibleDeptId = m.ResponsibleDeptId,
                ResponsibleDeptName = m.ResponsibleDeptName,
                ResponsibleUserId = m.ResponsibleUserId,
                ResponsibleUserName = m.ResponsibleUserName,
                Remark = m.Remark,
                PlannedDate = m.PlannedDate,
            }).ToList()
        };

        _db.ProjectIssues.Add(issue);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success", data = new { issue.Id, issue.IssueCode } });
    }
}

// ────────── Request Models ──────────

public class CreateIssueRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string IssueSource { get; set; } = "";
    public string IssueType { get; set; } = "";
    public string Severity { get; set; } = "一般";
    public string Priority { get; set; } = "一般";
    public string? CauseAnalysis { get; set; }
    public DateOnly DiscoveredDate { get; set; }
    public DateOnly? PlannedDate { get; set; }
    public long? ResponsibleDeptId { get; set; }
    public string? ResponsibleDeptName { get; set; }
    public long AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public long SubmitterId { get; set; }
    public string? SubmitterName { get; set; }
    public long? VerifierId { get; set; }
    public string? VerifierName { get; set; }
    public DateOnly? VerifiedDate { get; set; }
    public List<MeasureInput> Measures { get; set; } = new();
}

public class MeasureInput
{
    public int SortOrder { get; set; }
    public string Measure { get; set; } = "";
    public string? MeasureType { get; set; }
    public long? ResponsibleDeptId { get; set; }
    public string? ResponsibleDeptName { get; set; }
    public long? ResponsibleUserId { get; set; }
    public string? ResponsibleUserName { get; set; }
    public string? Remark { get; set; }
    public DateOnly? PlannedDate { get; set; }
}

public class CreateGlobalIssueRequest : CreateIssueRequest
{
    public long ProjectId { get; set; }
}

public class UpdateIssueStatusRequest
{
    public int Status { get; set; }
    public string? CauseAnalysis { get; set; }
    public long? VerifierId { get; set; }
    public string? VerifierName { get; set; }
    public DateOnly? VerifiedDate { get; set; }
}
