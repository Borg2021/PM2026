using ProjectManagement.Domain.Entities;

namespace ProjectManagement.Domain.Interfaces;

public interface ITemplateRepository
{
    Task<(List<Template> Items, int Total)> GetListAsync(
        string? templateCode, string? templateName, int? templateType,
        string? createdBy, string? description, int pageIndex, int pageSize);

    Task<Template?> GetByIdAsync(long id);
    Task<Template> AddAsync(Template template);
    Task UpdateAsync(Template template);
    Task DeletePlanNodesAsync(long templateId);
    Task<PlanNode> AddPlanNodeAsync(PlanNode node);
    Task SaveMilestonesAsync(long templateId, List<Milestone> milestones);
    Task SaveMembersAsync(long templateId, List<TemplateMember> members);
    Task SaveFileItemsAsync(long templateId, List<FileTemplateItem> items);
    Task<List<PlanNodeDependency>> GetDependenciesByNodeIdsAsync(List<long> nodeIds);
    Task AddDependenciesAsync(List<PlanNodeDependency> dependencies);
    Task<bool> TemplateCodeExistsAsync(string code, long? excludeId = null);
    Task<List<Department>> GetDepartmentsAsync();
    Task<List<RoleDict>> GetRolesAsync();
    Task<List<Domain.Entities.User>> SearchUsersAsync(string keyword);
    Task<string?> GetSysParamValueAsync(string key);
}
