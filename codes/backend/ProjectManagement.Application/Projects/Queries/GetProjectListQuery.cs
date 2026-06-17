using MediatR;
using ProjectManagement.Application.Common;
using ProjectManagement.Application.Projects.DTOs;
using ProjectManagement.Domain.DataScope;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Projects.Queries;

public record GetProjectListQuery(
    string? ProjectCode,
    string? ProjectName,
    string? ProjectType,
    int? Status,
    string? ProjectManagerName,
    long? MemberId,
    long? AssigneeId,
    int PageIndex,
    int PageSize,
    ProjectListScopeFilter? ScopeFilter = null
) : IRequest<PagedResult<ProjectDto>>;

public class GetProjectListHandler : IRequestHandler<GetProjectListQuery, PagedResult<ProjectDto>>
{
    private readonly IProjectRepository _repo;

    public GetProjectListHandler(IProjectRepository repo) => _repo = repo;

    public async Task<PagedResult<ProjectDto>> Handle(GetProjectListQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repo.GetListAsync(
            request.ProjectCode, request.ProjectName, request.ProjectType,
            request.Status, request.ProjectManagerName, request.MemberId, request.AssigneeId,
            request.PageIndex, request.PageSize, request.ScopeFilter);

        var dtos = items.Select(p => new ProjectDto
        {
            Id = p.Id,
            ProjectCode = p.ProjectCode,
            ProjectName = p.ProjectName,
            ProjectType = p.ProjectType,
            ContractCode = p.ContractCode,
            Status = p.Status,
            EngineeringCenter = p.EngineeringCenter,
            CustomerName = p.CustomerName,
            ProjectManagerName = p.ProjectManagerName,
            PmCenter = p.PmCenter,
            SalesManagerName = p.SalesManagerName,
            SalesRegion = p.SalesRegion,
            RequiredDelivery = p.RequiredDelivery,
            AcceptedDelivery = p.AcceptedDelivery,
            PlanStartDate = p.PlanStartDate,
            ProgressDesc = p.ProgressDesc,
            CreatedByName = p.CreatedByName,
            CreatedAt = p.CreatedAt
        }).ToList();

        var projectIds = dtos.Select(d => d.Id).ToList();
        var progressMap = await _repo.GetFirstTaskProgressMapAsync(projectIds);
        foreach (var dto in dtos)
        {
            if (progressMap.TryGetValue(dto.Id, out var info))
            {
                dto.FirstTaskProgress = info.Progress;
                dto.PlannedProgress = info.PlannedProgress;
                dto.RootPlanStartDate = info.PlanStart;
                dto.RootPlanFinishDate = info.PlanFinish;
                dto.RootPlanDuration = info.PlanDuration;
            }
        }

        return new PagedResult<ProjectDto> { Total = total, Items = dtos };
    }
}
