using MediatR;
using ProjectManagement.Application.Common;
using ProjectManagement.Application.PlanBundles.DTOs;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.PlanBundles.Queries;

public sealed record GetPlanBundleListQuery(
    string? Keyword,
    int PageIndex = 1,
    int PageSize = 10
) : IRequest<PagedResult<PlanBundleDto>>;

public class GetPlanBundleListHandler : IRequestHandler<GetPlanBundleListQuery, PagedResult<PlanBundleDto>>
{
    private readonly IPlanBundleRepository _repo;
    public GetPlanBundleListHandler(IPlanBundleRepository repo) => _repo = repo;

    public async Task<PagedResult<PlanBundleDto>> Handle(GetPlanBundleListQuery q, CancellationToken ct)
    {
        var (items, total) = await _repo.GetListAsync(q.Keyword, q.PageIndex, q.PageSize);
        var dtos = items.Select(b => new PlanBundleDto
        {
            Id = b.Id,
            Name = b.Name,
            Description = b.Description,
            TemplateCount = b.Items.Count,
            CreatedByName = b.CreatedByName,
            CreatedAt = b.CreatedAt
        }).ToList();
        return new PagedResult<PlanBundleDto> { Items = dtos, Total = total };
    }
}

public sealed record GetPlanBundleDetailQuery(long Id) : IRequest<PlanBundleDetailDto?>;

public class GetPlanBundleDetailHandler : IRequestHandler<GetPlanBundleDetailQuery, PlanBundleDetailDto?>
{
    private readonly IPlanBundleRepository _repo;
    public GetPlanBundleDetailHandler(IPlanBundleRepository repo) => _repo = repo;

    public async Task<PlanBundleDetailDto?> Handle(GetPlanBundleDetailQuery q, CancellationToken ct)
    {
        var bundle = await _repo.GetByIdAsync(q.Id);
        if (bundle == null) return null;

        return new PlanBundleDetailDto
        {
            Id = bundle.Id,
            Name = bundle.Name,
            Description = bundle.Description,
            TemplateCount = bundle.Items.Count,
            CreatedByName = bundle.CreatedByName,
            CreatedAt = bundle.CreatedAt,
            Items = bundle.Items.Select(i => new PlanBundleItemDto
            {
                Id = i.Id,
                TemplateId = i.TemplateId,
                TemplateCode = i.Template?.TemplateCode ?? "",
                TemplateName = i.Template?.TemplateName ?? "",
                SortOrder = i.SortOrder
            }).ToList()
        };
    }
}
