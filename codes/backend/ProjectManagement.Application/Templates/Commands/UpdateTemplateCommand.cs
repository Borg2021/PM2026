using MediatR;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Templates.Commands;

public record UpdateTemplateCommand(
    long Id,
    string TemplateName,
    string? Description,
    long UpdatedBy
) : IRequest;

public class UpdateTemplateHandler : IRequestHandler<UpdateTemplateCommand>
{
    private readonly ITemplateRepository _repo;

    public UpdateTemplateHandler(ITemplateRepository repo) => _repo = repo;

    public async Task Handle(UpdateTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repo.GetByIdAsync(request.Id);
        if (template == null) throw new InvalidOperationException("模板不存在");

        template.TemplateName = request.TemplateName;
        template.Description = request.Description;
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = request.UpdatedBy;

        await _repo.UpdateAsync(template);
    }
}
