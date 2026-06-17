using Microsoft.EntityFrameworkCore;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Infrastructure.Data;

namespace ProjectManagement.Infrastructure.Repositories;

public class TemplateRepository : ITemplateRepository
{
    private readonly AppDbContext _db;

    public TemplateRepository(AppDbContext db) => _db = db;

    public async Task<(List<Template> Items, int Total)> GetListAsync(
        string? templateCode, string? templateName, int? templateType,
        string? createdBy, string? description, int pageIndex, int pageSize)
    {
        var query = _db.Templates.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(templateCode))
            query = query.Where(t => t.TemplateCode.Contains(templateCode));
        if (!string.IsNullOrWhiteSpace(templateName))
            query = query.Where(t => t.TemplateName.Contains(templateName));
        if (templateType.HasValue)
            query = query.Where(t => t.TemplateType == templateType.Value);
        if (!string.IsNullOrWhiteSpace(createdBy))
        {
            var names = createdBy.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(n => n.Trim()).ToList();
            query = query.Where(t => names.Contains(t.CreatedByName));
        }
        if (!string.IsNullOrWhiteSpace(description))
            query = query.Where(t => t.Description != null && t.Description.Contains(description));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var total = await query.CountAsync();
        var countMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        var items = await query
            .OrderBy(t => t.TemplateCode)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new Template
            {
                Id = t.Id,
                TemplateCode = t.TemplateCode,
                TemplateName = t.TemplateName,
                TemplateType = t.TemplateType,
                Description = t.Description,
                Status = t.Status,
                CreatedByName = t.CreatedByName,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();
        var queryMs = sw.Elapsed.TotalMilliseconds;

        Console.WriteLine($"[TMPL-REPO] Count={countMs:F1}ms | Query={queryMs:F1}ms | Total={total} | Returned={items.Count}");

        return (items, total);
    }

    public async Task<Template?> GetByIdAsync(long id)
    {
        var template = await _db.Templates
            .IgnoreQueryFilters()
            .Include(t => t.PlanNodes)
            .Include(t => t.Milestones)
            .Include(t => t.Members)
            .Include(t => t.FileItems)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id);

        return template;
    }

    public async Task<Template> AddAsync(Template template)
    {
        _db.Templates.Add(template);
        await _db.SaveChangesAsync();
        return template;
    }

    public async Task UpdateAsync(Template template)
    {
        _db.Templates.Update(template);
        await _db.SaveChangesAsync();
    }

    public async Task DeletePlanNodesAsync(long templateId)
    {
        var nodeIds = await _db.PlanNodes
            .Where(n => n.TemplateId == templateId)
            .Select(n => n.Id)
            .ToListAsync();

        if (nodeIds.Count > 0)
        {
            var deps = await _db.PlanNodeDependencies
                .Where(d => nodeIds.Contains(d.PlanNodeId) || nodeIds.Contains(d.PredecessorId))
                .ToListAsync();
            _db.PlanNodeDependencies.RemoveRange(deps);
        }

        var existing = await _db.PlanNodes.Where(n => n.TemplateId == templateId).ToListAsync();
        _db.PlanNodes.RemoveRange(existing);
        await _db.SaveChangesAsync();
    }

    public async Task<PlanNode> AddPlanNodeAsync(PlanNode node)
    {
        _db.PlanNodes.Add(node);
        await _db.SaveChangesAsync();
        return node;
    }

    public async Task SaveMilestonesAsync(long templateId, List<Milestone> milestones)
    {
        var existing = await _db.Milestones.Where(m => m.TemplateId == templateId).ToListAsync();
        _db.Milestones.RemoveRange(existing);

        foreach (var m in milestones)
        {
            m.TemplateId = templateId;
            m.Id = 0;
        }
        _db.Milestones.AddRange(milestones);
        await _db.SaveChangesAsync();
    }

    public async Task SaveMembersAsync(long templateId, List<TemplateMember> members)
    {
        var existing = await _db.TemplateMembers.Where(m => m.TemplateId == templateId).ToListAsync();
        _db.TemplateMembers.RemoveRange(existing);

        foreach (var m in members)
        {
            m.TemplateId = templateId;
            m.Id = 0;
        }
        _db.TemplateMembers.AddRange(members);
        await _db.SaveChangesAsync();
    }

    public async Task SaveFileItemsAsync(long templateId, List<FileTemplateItem> items)
    {
        var existing = await _db.FileTemplateItems.Where(m => m.TemplateId == templateId).ToListAsync();
        _db.FileTemplateItems.RemoveRange(existing);

        foreach (var m in items)
        {
            m.TemplateId = templateId;
            m.Id = 0;
        }
        _db.FileTemplateItems.AddRange(items);
        await _db.SaveChangesAsync();
    }

    public async Task<List<PlanNodeDependency>> GetDependenciesByNodeIdsAsync(List<long> nodeIds)
    {
        if (nodeIds.Count == 0) return new List<PlanNodeDependency>();
        return await _db.PlanNodeDependencies
            .Where(d => nodeIds.Contains(d.PlanNodeId))
            .ToListAsync();
    }

    public async Task AddDependenciesAsync(List<PlanNodeDependency> dependencies)
    {
        _db.PlanNodeDependencies.AddRange(dependencies);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> TemplateCodeExistsAsync(string code, long? excludeId = null)
    {
        var query = _db.Templates.IgnoreQueryFilters().Where(t => t.TemplateCode == code);
        if (excludeId.HasValue)
            query = query.Where(t => t.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task<string?> GetSysParamValueAsync(string key)
    {
        var param = await _db.SysParams.FirstOrDefaultAsync(p => p.ParamKey == key);
        return param?.ParamValue;
    }

    public async Task<List<Department>> GetDepartmentsAsync()
    {
        return await _db.Departments.OrderBy(d => d.SortOrder).ToListAsync();
    }

    public async Task<List<RoleDict>> GetRolesAsync()
    {
        return await _db.RoleDicts.ToListAsync();
    }

    public async Task<List<User>> SearchUsersAsync(string keyword)
    {
        return await _db.Users
            .Where(u => u.Status == 1 && (u.RealName.Contains(keyword) || u.Username.Contains(keyword)))
            .Select(u => new User { Id = u.Id, RealName = u.RealName, Username = u.Username })
            .Take(20)
            .ToListAsync();
    }
}
