using Microsoft.EntityFrameworkCore;
using ProjectManagement.Domain.DataScope;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Enums;

namespace ProjectManagement.Infrastructure.Data;

public static class ProjectDataScopeFilter
{
    public static IQueryable<Project> Apply(IQueryable<Project> query, AppDbContext db, ProjectListScopeFilter? filter)
    {
        if (filter == null || filter.Bypass) return query;
        if (filter.WorkbenchSelfOnly) return ApplySelf(query, filter.UserId);

        // 保存原始 query 用于后续部门负责人 Union
        var baseQuery = query;

        query = filter.Scope switch
        {
            DataScopeType.All => query,
            DataScopeType.Self => ApplySelf(query, filter.UserId),
            DataScopeType.MemberOnly => query.Where(p => p.Members.Any(m => m.MemberId == filter.UserId)),
            DataScopeType.ProjectManagerOwn => query.Where(p => p.ProjectManagerId == filter.UserId),
            DataScopeType.Dept => ApplyDept(query, db, filter),
            DataScopeType.DeptAndChildren => ApplyDeptTree(query, db, filter),
            _ => query.Where(p => false)
        };

        // 部门负责人可见性：管辖部门成员参与的项目也对用户可见（Union 取并集）
        if (filter.LeaderDeptIds.Count > 0)
        {
            var ids = filter.LeaderDeptIds;
            var leaderQuery = baseQuery.Where(p =>
                p.Members.Any(m =>
                    m.MemberId != null &&
                    db.Users.Any(u => u.Id == m.MemberId && u.DepartmentId != null && ids.Contains(u.DepartmentId.Value))));
            query = query.Union(leaderQuery);
        }

        // 非管理员统一过滤未激活(0)，但项目经理可看到自己负责的未激活项目
        query = query.Where(p => p.Status != 0 || p.ProjectManagerId == filter.UserId);

        if (filter.HideSuspended)
            query = query.Where(p => p.Status != 3);

        return query;
    }

    public static async Task<bool> MatchesProjectAsync(AppDbContext db, Project project, ProjectListScopeFilter filter)
    {
        if (filter.Bypass) return true;
        if (filter.WorkbenchSelfOnly) return await IsSelfRelatedAsync(db, project.Id, filter.UserId);

        // 部门负责人可见性检查
        if (filter.LeaderDeptIds.Count > 0)
        {
            var memberIds = await db.ProjectMembers
                .Where(m => m.ProjectId == project.Id && m.MemberId != null)
                .Select(m => m.MemberId!.Value)
                .ToListAsync();
            if (memberIds.Count > 0)
            {
                var membersInDept = await db.Users
                    .AnyAsync(u => memberIds.Contains(u.Id) && u.DepartmentId != null && filter.LeaderDeptIds.Contains(u.DepartmentId.Value));
                if (membersInDept) return true;
            }
        }

        return filter.Scope switch
        {
            DataScopeType.All => true,
            DataScopeType.Self => await IsSelfRelatedAsync(db, project.Id, filter.UserId),
            DataScopeType.MemberOnly => await db.ProjectMembers.AnyAsync(m => m.ProjectId == project.Id && m.MemberId == filter.UserId),
            DataScopeType.ProjectManagerOwn => project.ProjectManagerId == filter.UserId,
            DataScopeType.Dept => await IsDeptRelatedAsync(db, project, filter) || await IsSelfRelatedAsync(db, project.Id, filter.UserId),
            DataScopeType.DeptAndChildren => await IsDeptTreeRelatedAsync(db, project, filter) || await IsSelfRelatedAsync(db, project.Id, filter.UserId),
            _ => false
        };
    }

    private static IQueryable<Project> ApplySelf(IQueryable<Project> query, long userId) =>
        query.Where(p =>
            p.Members.Any(m => m.MemberId == userId) ||
            p.Tasks.Any(t => t.AssigneeId == userId) ||
            p.ProjectManagerId == userId ||
            p.CreatedBy == userId);

    private static IQueryable<Project> ApplyDept(IQueryable<Project> query, AppDbContext db, ProjectListScopeFilter filter)
    {
        var userId = filter.UserId;
        var deptId = filter.UserDeptId;
        var deptName = filter.UserDeptName;
        return query.Where(p =>
            p.Members.Any(m => m.MemberId == userId) ||
            p.Tasks.Any(t => t.AssigneeId == userId) ||
            p.ProjectManagerId == userId ||
            p.CreatedBy == userId ||
            (deptId != null && p.ProjectManagerId != null &&
             db.Users.Any(u => u.Id == p.ProjectManagerId && u.DepartmentId == deptId)) ||
            (deptName != null && p.PmCenter != null && p.PmCenter == deptName));
    }

    private static IQueryable<Project> ApplyDeptTree(IQueryable<Project> query, AppDbContext db, ProjectListScopeFilter filter)
    {
        var allowed = filter.AllowedDeptIds;
        var userId = filter.UserId;
        if (allowed.Count == 0) return ApplySelf(query, userId);

        var deptNames = db.Departments.Where(d => allowed.Contains(d.Id)).Select(d => d.Name);
        return query.Where(p =>
            p.Members.Any(m => m.MemberId == userId) ||
            p.Tasks.Any(t => t.AssigneeId == userId) ||
            p.ProjectManagerId == userId ||
            p.CreatedBy == userId ||
            (p.ProjectManagerId != null &&
             db.Users.Any(u => u.Id == p.ProjectManagerId && u.DepartmentId != null && allowed.Contains(u.DepartmentId.Value))) ||
            (p.PmCenter != null && deptNames.Contains(p.PmCenter)));
    }

    private static async Task<bool> IsSelfRelatedAsync(AppDbContext db, long projectId, long userId)
    {
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId);
        if (p == null) return false;
        if (p.ProjectManagerId == userId || p.CreatedBy == userId) return true;
        if (await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.MemberId == userId)) return true;
        return await db.ProjectTasks.AnyAsync(t => t.ProjectId == projectId && t.AssigneeId == userId);
    }

    private static async Task<bool> IsDeptRelatedAsync(AppDbContext db, Project project, ProjectListScopeFilter filter)
    {
        if (filter.UserDeptId == null) return false;
        if (project.PmCenter != null && filter.UserDeptName != null && project.PmCenter == filter.UserDeptName)
            return true;
        if (project.ProjectManagerId == null) return false;
        var pmDept = await db.Users.Where(u => u.Id == project.ProjectManagerId).Select(u => u.DepartmentId).FirstOrDefaultAsync();
        return pmDept == filter.UserDeptId;
    }

    private static async Task<bool> IsDeptTreeRelatedAsync(AppDbContext db, Project project, ProjectListScopeFilter filter)
    {
        if (filter.AllowedDeptIds.Count == 0) return false;
        var allowed = filter.AllowedDeptIds;
        if (project.PmCenter != null)
        {
            var match = await db.Departments.AnyAsync(d => allowed.Contains(d.Id) && d.Name == project.PmCenter);
            if (match) return true;
        }
        if (project.ProjectManagerId == null) return false;
        var pmDept = await db.Users.Where(u => u.Id == project.ProjectManagerId).Select(u => u.DepartmentId).FirstOrDefaultAsync();
        return pmDept.HasValue && allowed.Contains(pmDept.Value);
    }
}
