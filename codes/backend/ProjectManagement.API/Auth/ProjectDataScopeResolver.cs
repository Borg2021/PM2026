using Microsoft.EntityFrameworkCore;
using ProjectManagement.Domain.DataScope;
using ProjectManagement.Domain.Enums;
using ProjectManagement.Infrastructure.Data;

namespace ProjectManagement.API.Auth;

public static class ProjectDataScopeResolver
{
    public static async Task<ProjectListScopeFilter> BuildListFilterAsync(AppDbContext db, long userId)
    {
        if (await ProjectPermissionHelper.IsAdminAsync(db, userId))
            return new ProjectListScopeFilter { Bypass = true, UserId = userId };

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        string? deptName = null;
        if (user?.DepartmentId != null)
            deptName = await db.Departments.Where(d => d.Id == user.DepartmentId).Select(d => d.Name).FirstOrDefaultAsync();

        var roleScopes = await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => new { ur.Role.Code, ur.Role.DataScope })
            .ToListAsync();

        if (roleScopes.Any(r => r.Code == "project_admin"))
            return new ProjectListScopeFilter { Bypass = true, UserId = userId };

        DataScopeType effective;
        if (user?.DataScope != null)
            effective = (DataScopeType)user.DataScope.Value;
        else if (roleScopes.Count == 0)
            effective = DataScopeType.MemberOnly;
        else
            effective = (DataScopeType)roleScopes.Min(r => r.DataScope);

        if (effective == DataScopeType.All)
            return new ProjectListScopeFilter { Bypass = true, UserId = userId };

        var hasProjectManager = roleScopes.Any(r => r.Code == "project_manager");

        IReadOnlyList<long> allowedDeptIds = effective == DataScopeType.DeptAndChildren
            ? await GetDeptAndChildrenIdsAsync(db, user?.DepartmentId)
            : Array.Empty<long>();

        // 计算用户担任负责人的部门及其所有子孙部门
        var leaderDeptIds = await GetLeaderDeptTreeIdsAsync(db, userId);

        return new ProjectListScopeFilter
        {
            UserId = userId,
            Scope = effective,
            UserDeptId = user?.DepartmentId,
            UserDeptName = deptName,
            AllowedDeptIds = allowedDeptIds,
            LeaderDeptIds = leaderDeptIds,
            HideSuspended = !hasProjectManager
        };
    }

    public static async Task<bool> CanMaintainProjectAsync(AppDbContext db, long projectId, long userId)
    {
        if (await ProjectPermissionHelper.IsAdminAsync(db, userId)) return true;

        var isProjectAdmin = await db.UserRoles.AnyAsync(ur =>
            ur.UserId == userId && ur.Role.Code == "project_admin");
        if (isProjectAdmin) return true;

        var filter = await BuildListFilterAsync(db, userId);
        if (filter.Bypass) return true;

        if (filter.Scope == DataScopeType.ProjectManagerOwn)
        {
            return await db.Projects.AnyAsync(p => p.Id == projectId && p.ProjectManagerId == userId);
        }

        // 项目经理可维护项目（无需依赖 DataScope 或项目成员身份）
        if (await db.Projects.AnyAsync(p => p.Id == projectId && p.ProjectManagerId == userId))
            return true;

        // 项目成员可维护项目（编辑基本信息、成员、任务等）
        if (await ProjectPermissionHelper.IsProjectMemberAsync(db, projectId, userId))
            return true;

        return false;
    }

    /// <summary>更新任务：项目维护权、任务负责人、或项目成员（支持协作场景与级联日期调度）。</summary>
    public static async Task<bool> CanUpdateProjectTaskAsync(AppDbContext db, long projectId, long taskId, long userId)
    {
        if (await CanMaintainProjectAsync(db, projectId, userId)) return true;
        // 任务负责人可编辑自己负责的任务
        if (await db.ProjectTasks.AnyAsync(t => t.Id == taskId && t.ProjectId == projectId && t.AssigneeId == userId))
            return true;
        // 项目成员可编辑项目内任何任务（支持级联日期更新等协作场景）
        return await ProjectPermissionHelper.IsProjectMemberAsync(db, projectId, userId);
    }

    /// <summary>确认完成/暂停：仅系统管理员、项目管理员角色、项目经理可操作</summary>
    public static async Task<bool> CanChangeProjectStatusAsync(AppDbContext db, long projectId, long userId)
    {
        if (await ProjectPermissionHelper.IsAdminAsync(db, userId)) return true;
        if (await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.Role.Code == "project_admin"))
            return true;
        if (await db.Projects.AnyAsync(p => p.Id == projectId && p.ProjectManagerId == userId))
            return true;
        return false;
    }

    /// <summary>取消激活：仅系统管理员、纯项目管理员角色可操作（项目经理不可，即使同时拥有项目管理员角色）</summary>
    public static async Task<bool> CanDeactivateProjectAsync(AppDbContext db, long projectId, long userId)
    {
        if (await ProjectPermissionHelper.IsAdminAsync(db, userId)) return true;
        // 项目管理员角色可取消激活，但项目经理角色（即使同时拥有项目管理员角色）不可
        if (await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.Role.Code == "project_admin"))
        {
            var isProjectManager = await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.Role.Code == "project_manager");
            if (!isProjectManager) return true;
        }
        return false;
    }

    private static async Task<List<long>> GetDeptAndChildrenIdsAsync(AppDbContext db, long? rootDeptId)
    {
        if (rootDeptId == null) return new List<long>();
        var all = await db.Departments.AsNoTracking().Select(d => new { d.Id, d.ParentId }).ToListAsync();
        var result = new List<long> { rootDeptId.Value };
        void Collect(long parentId)
        {
            foreach (var child in all.Where(d => d.ParentId == parentId))
            {
                result.Add(child.Id);
                Collect(child.Id);
            }
        }
        Collect(rootDeptId.Value);
        return result;
    }

    /// <summary>获取用户担任负责人的部门及其所有子孙部门 ID</summary>
    private static async Task<List<long>> GetLeaderDeptTreeIdsAsync(AppDbContext db, long userId)
    {
        var leaderDeptIds = await db.DepartmentLeaders
            .Where(l => l.UserId == userId)
            .Select(l => l.DepartmentId)
            .Distinct()
            .ToListAsync();
        if (leaderDeptIds.Count == 0) return new List<long>();

        var allDepts = await db.Departments.AsNoTracking()
            .Select(d => new { d.Id, d.ParentId }).ToListAsync();
        var result = new HashSet<long>(leaderDeptIds);
        void Collect(long parentId)
        {
            foreach (var d in allDepts.Where(d => d.ParentId == parentId))
            {
                if (result.Add(d.Id)) Collect(d.Id);
            }
        }
        foreach (var rootId in leaderDeptIds) Collect(rootId);
        return result.ToList();
    }
}
