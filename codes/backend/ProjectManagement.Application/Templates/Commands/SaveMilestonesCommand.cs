using MediatR;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Templates.Commands;

public record SaveMilestonesCommand(long TemplateId, List<MilestoneInput> Milestones) : IRequest;

public class MilestoneInput
{
    public string MilestoneCode { get; set; } = "";
    public string MilestoneName { get; set; } = "";
    public string? Remark { get; set; }
}

public class SaveMilestonesHandler : IRequestHandler<SaveMilestonesCommand>
{
    private readonly ITemplateRepository _repo;

    public SaveMilestonesHandler(ITemplateRepository repo) => _repo = repo;

    public async Task Handle(SaveMilestonesCommand request, CancellationToken cancellationToken)
    {
        var milestones = request.Milestones.Select((m, i) => new Milestone
        {
            MilestoneCode = m.MilestoneCode,
            MilestoneName = m.MilestoneName,
            SortOrder = i + 1,
            Remark = m.Remark
        }).ToList();

        await _repo.SaveMilestonesAsync(request.TemplateId, milestones);
    }
}
