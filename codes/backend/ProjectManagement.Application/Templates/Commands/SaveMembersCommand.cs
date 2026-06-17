using MediatR;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Templates.Commands;

public record SaveMembersCommand(long TemplateId, List<MemberInput> Members) : IRequest;

public class MemberInput
{
    public long? RoleId { get; set; }
    public string? RoleName { get; set; }
    public long? MemberId { get; set; }
    public string? MemberName { get; set; }
    public long? DeptId { get; set; }
    public string? DeptName { get; set; }
    public string? Remark { get; set; }
}

public class SaveMembersHandler : IRequestHandler<SaveMembersCommand>
{
    private readonly ITemplateRepository _repo;

    public SaveMembersHandler(ITemplateRepository repo) => _repo = repo;

    public async Task Handle(SaveMembersCommand request, CancellationToken cancellationToken)
    {
        var members = request.Members.Select((m, i) => new TemplateMember
        {
            SortOrder = i + 1,
            RoleId = m.RoleId,
            RoleName = m.RoleName,
            MemberId = m.MemberId,
            MemberName = m.MemberName,
            DeptId = m.DeptId,
            DeptName = m.DeptName,
            Remark = m.Remark
        }).ToList();

        await _repo.SaveMembersAsync(request.TemplateId, members);
    }
}
