using Microsoft.EntityFrameworkCore;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Infrastructure.Data;

namespace ProjectManagement.Infrastructure.Repositories;

public class PlanBundleRepository : IPlanBundleRepository
{
    private readonly AppDbContext _db;
    public PlanBundleRepository(AppDbContext db) => _db = db;

    public async Task<(List<PlanBundle> Items, int Total)> GetListAsync(string? keyword, int pageIndex, int pageSize)
    {
        var query = _db.PlanBundles.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(b => b.Name.ToLower().Contains(kw));
        }
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Include(b => b.Items)
            .ToListAsync();
        return (items, total);
    }

    public async Task<PlanBundle?> GetByIdAsync(long id)
    {
        return await _db.PlanBundles
            .Include(b => b.Items.OrderBy(i => i.SortOrder))
            .ThenInclude(i => i.Template)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<PlanBundle> AddAsync(PlanBundle bundle)
    {
        _db.PlanBundles.Add(bundle);
        await _db.SaveChangesAsync();
        return bundle;
    }

    public async Task UpdateAsync(PlanBundle bundle)
    {
        _db.PlanBundles.Update(bundle);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(PlanBundle bundle)
    {
        _db.PlanBundles.Remove(bundle);
        await _db.SaveChangesAsync();
    }

    public async Task<List<Template>> GetTemplatesByIdsAsync(List<long> ids)
    {
        return await _db.Templates
            .IgnoreQueryFilters()
            .Where(t => ids.Contains(t.Id))
            .ToListAsync();
    }

    public async Task<List<PlanNode>> GetPlanNodesByTemplateIdsAsync(List<long> ids)
    {
        return await _db.PlanNodes
            .Where(n => ids.Contains(n.TemplateId))
            .ToListAsync();
    }

    public async Task<List<PlanNodeDependency>> GetDependenciesByNodeIdsAsync(List<long> nodeIds)
    {
        if (nodeIds.Count == 0) return new List<PlanNodeDependency>();
        return await _db.PlanNodeDependencies
            .Where(d => nodeIds.Contains(d.PlanNodeId) || nodeIds.Contains(d.PredecessorId))
            .ToListAsync();
    }

    public async Task<Template> AddTemplateAsync(Template template)
    {
        _db.Templates.Add(template);
        await _db.SaveChangesAsync();
        return template;
    }

    public async Task AddPlanNodesAsync(List<PlanNode> nodes)
    {
        _db.PlanNodes.AddRange(nodes);
        await _db.SaveChangesAsync();
    }

    public async Task AddDependenciesAsync(List<PlanNodeDependency> deps)
    {
        if (deps.Count == 0) return;
        _db.PlanNodeDependencies.AddRange(deps);
        await _db.SaveChangesAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }

    public async Task<string?> GetSysParamValueAsync(string key)
    {
        var param = await _db.SysParams.FirstOrDefaultAsync(p => p.ParamKey == key);
        return param?.ParamValue;
    }
}
