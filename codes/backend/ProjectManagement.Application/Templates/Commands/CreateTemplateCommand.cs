using MediatR;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Templates.Commands;

public record CreateTemplateCommand(
    string TemplateCode,
    string TemplateName,
    int TemplateType,
    string? Description,
    long CreatedBy,
    string CreatedByName
) : IRequest<long>;

public class CreateTemplateHandler : IRequestHandler<CreateTemplateCommand, long>
{
    private readonly ITemplateRepository _repo;

    public CreateTemplateHandler(ITemplateRepository repo) => _repo = repo;

    public async Task<long> Handle(CreateTemplateCommand request, CancellationToken cancellationToken)
    {
        if (await _repo.TemplateCodeExistsAsync(request.TemplateCode))
            throw new InvalidOperationException("模板编号已存在");

        var template = new Template
        {
            TemplateCode = request.TemplateCode,
            TemplateName = request.TemplateName,
            TemplateType = request.TemplateType,
            Description = request.Description,
            CreatedBy = request.CreatedBy,
            CreatedByName = request.CreatedByName,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _repo.AddAsync(template);
        return result.Id;
    }
}
