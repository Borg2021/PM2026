using ProjectManagement.Domain.Entities;

namespace ProjectManagement.Domain.Interfaces;

public interface IPlanBundleRepository
{
    Task<(List<PlanBundle> Items, int Total)> GetListAsync(string? keyword, int pageIndex, int pageSize);
    Task<PlanBundle?> GetByIdAsync(long id);
    Task<PlanBundle> AddAsync(PlanBundle bundle);
    Task UpdateAsync(PlanBundle bundle);
    Task DeleteAsync(PlanBundle bundle);
    Task<List<Template>> GetTemplatesByIdsAsync(List<long> ids);
    Task<List<PlanNode>> GetPlanNodesByTemplateIdsAsync(List<long> ids);
    Task<List<PlanNodeDependency>> GetDependenciesByNodeIdsAsync(List<long> nodeIds);
    Task<Template> AddTemplateAsync(Template template);
    Task AddPlanNodesAsync(List<PlanNode> nodes);
    Task AddDependenciesAsync(List<PlanNodeDependency> deps);
    Task SaveChangesAsync();
    Task<string?> GetSysParamValueAsync(string key);
}
