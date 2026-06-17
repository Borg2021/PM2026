using MediatR;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Templates.Commands;

public record DeleteTemplateCommand(long Id) : IRequest;

public class DeleteTemplateHandler : IRequestHandler<DeleteTemplateCommand>
{
    private readonly ITemplateRepository _repo;

    public DeleteTemplateHandler(ITemplateRepository repo) => _repo = repo;

    public async Task Handle(DeleteTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repo.GetByIdAsync(request.Id);
        if (template == null) throw new InvalidOperationException("模板不存在");

        template.Status = 0;
        template.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(template);
    }
}
