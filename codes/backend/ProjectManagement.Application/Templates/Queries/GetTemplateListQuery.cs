using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using ProjectManagement.Application.Common;
using ProjectManagement.Application.Templates.DTOs;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Templates.Queries;

public record GetTemplateListQuery(
    string? TemplateCode,
    string? TemplateName,
    int? TemplateType,
    string? CreatedBy,
    string? Description,
    int PageIndex = 1,
    int PageSize = 10
) : IRequest<PagedResult<TemplateDto>>;

public class GetTemplateListHandler : IRequestHandler<GetTemplateListQuery, PagedResult<TemplateDto>>
{
    private readonly ITemplateRepository _repo;
    private readonly ILogger<GetTemplateListHandler> _logger;

    public GetTemplateListHandler(ITemplateRepository repo, ILogger<GetTemplateListHandler> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<PagedResult<TemplateDto>> Handle(GetTemplateListQuery request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        var (items, total) = await _repo.GetListAsync(
            request.TemplateCode, request.TemplateName, request.TemplateType,
            request.CreatedBy, request.Description,
            request.PageIndex, request.PageSize);

        var dbMs = sw.Elapsed.TotalMilliseconds;

        var typeNames = new Dictionary<int, string> { { 1, "计划" }, { 2, "里程碑" }, { 3, "项目成员" }, { 4, "文件模板" } };

        var dtoSw = Stopwatch.StartNew();
        var result = new PagedResult<TemplateDto>
        {
            Total = total,
            Items = items.Select(t => new TemplateDto
            {
                Id = t.Id,
                TemplateCode = t.TemplateCode,
                TemplateName = t.TemplateName,
                TemplateType = t.TemplateType,
                TemplateTypeName = typeNames.GetValueOrDefault(t.TemplateType, ""),
                Description = t.Description,
                CreatedByName = t.CreatedByName,
                CreatedAt = t.CreatedAt,
                Status = t.Status
            }).ToList()
        };
        var dtoMs = dtoSw.Elapsed.TotalMilliseconds;

        _logger.LogDebug(
            "[TMPL-LIST] 总耗时={TotalMs:F1}ms | DB查询={DbMs:F1}ms | DTO组装={DtoMs:F1}ms | 总条数={Total} | 返回条数={Count}",
            sw.Elapsed.TotalMilliseconds, dbMs, dtoMs, total, result.Items.Count);

        return result;
    }
}
