using Microsoft.EntityFrameworkCore;
using ProjectManagement.Domain.Enums;
using ProjectManagement.Infrastructure.Data;

namespace ProjectManagement.API.Auth;

/// <summary>
/// 项目模块权限校验（含菜单隐含、工作台自查、项目成员查看、数据范围）。
/// </summary>
public static class ProjectPermissionHelper
{
    public static async Task<bool> IsAdminAsync(AppDbContext db, long userId)
    {
        return await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.Role.Code == "admin");
    }

    public static async Task<List<string>> GetUserPermissionCodesAsync(AppDbContext db, long userId)
    {
        if (await IsAdminAsync(db, userId))
            return await db.Permissions.Select(p => p.Code).ToListAsync();

        return await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync();
    }

    public static bool Satisfies(IReadOnlyCollection<string> codes, string required)
    {
        if (codes.Contains(required)) return true;
        if (codes.Contains("project:list")
            && (required == "project:list:view" || required == "project:detail:view"))
            return true;
        return false;
    }

    public static bool CanViewProjectList(IReadOnlyCollection<string> codes)
        => Satisfies(codes, "project:list:view");

    public static bool CanViewProjectDetail(IReadOnlyCollection<string> codes)
        => Satisfies(codes, "project:detail:view");

    /// <summary>工作台「我的项目」：仅查询本人相关项目。</summary>
    public static bool IsSelfScopedListQuery(long userId, long? memberId, long? assigneeId)
    {
        if (!memberId.HasValue && !assigneeId.HasValue) return false;
        return memberId == userId || assigneeId == userId;
    }

    public static async Task<bool> IsProjectMemberAsync(AppDbContext db, long projectId, long userId)
        => await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.MemberId == userId);

    public static async Task<bool> IsProjectTaskAssigneeAsync(AppDbContext db, long projectId, long userId)
        => await db.ProjectTasks.AnyAsync(t => t.ProjectId == projectId && t.AssigneeId == userId);

    /// <summary>查看项目详情：功能权限 + 数据范围 + 成员/负责人规则。</summary>
    public static async Task<bool> CanAccessProjectAsync(
        AppDbContext db, long projectId, long userId, IReadOnlyCollection<string> codes)
    {
        if (await IsAdminAsync(db, userId)) return true;
        // 项目经理可查看自己负责的项目（无需是成员列表中的一行）
        if (await db.Projects.AnyAsync(p => p.Id == projectId && p.ProjectManagerId == userId))
            return true;
        if (await IsProjectMemberAsync(db, projectId, userId)) return true;

        // 部门负责人可见性：若用户是某部门负责人，且项目中有成员属于该部门（含子部门），则可查看
        var leaderDeptIds = await db.DepartmentLeaders
            .Where(l => l.UserId == userId)
            .Select(l => l.DepartmentId)
            .Distinct()
            .ToListAsync();
        if (leaderDeptIds.Count > 0)
        {
            var allDepts = await db.Departments.AsNoTracking()
                .Select(d => new { d.Id, d.ParentId }).ToListAsync();
            var fullIds = new HashSet<long>(leaderDeptIds);
            void Collect(long pid)
            {
                foreach (var d in allDepts.Where(d => d.ParentId == pid))
                { if (fullIds.Add(d.Id)) Collect(d.Id); }
            }
            foreach (var id in leaderDeptIds) Collect(id);

            var memberIds = await db.ProjectMembers
                .Where(m => m.ProjectId == projectId && m.MemberId != null)
                .Select(m => m.MemberId!.Value)
                .ToListAsync();
            if (memberIds.Count > 0)
            {
                var hasMemberInDept = await db.Users
                    .AnyAsync(u => memberIds.Contains(u.Id) && u.DepartmentId != null && fullIds.Contains(u.DepartmentId.Value));
                if (hasMemberInDept) return true;
            }
        }

        var scopeFilter = await ProjectDataScopeResolver.BuildListFilterAsync(db, userId);
        var hasListOrDetail = CanViewProjectList(codes) || CanViewProjectDetail(codes);

        if (!hasListOrDetail)
            return await IsProjectTaskAssigneeAsync(db, projectId, userId);

        if (await IsProjectTaskAssigneeAsync(db, projectId, userId))
        {
            if (scopeFilter.Scope is DataScopeType.Self or DataScopeType.Dept or DataScopeType.DeptAndChildren)
                return true;
            if (scopeFilter.Bypass) return true;
        }

        if (!CanViewProjectDetail(codes) && !CanViewProjectList(codes))
            return false;

        if (scopeFilter.Bypass) return true;

        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) return false;

        if (scopeFilter.Scope == DataScopeType.MemberOnly)
            return await IsProjectMemberAsync(db, projectId, userId);

        return await ProjectDataScopeFilter.MatchesProjectAsync(db, project, scopeFilter);
    }
}
