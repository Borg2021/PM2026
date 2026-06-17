using MediatR;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Templates.Commands;

public record SaveFilesCommand(long TemplateId, List<FileInput> Files) : IRequest;

public class FileInput
{
    public int SortOrder { get; set; }
    public string FileName { get; set; } = "";
    public bool Required { get; set; }
    public bool IsPublic { get; set; } = true;
    public string? ViewRoles { get; set; }
    public long? DeptId { get; set; }
    public string? DeptName { get; set; }
    public string? Remark { get; set; }
}

public class SaveFilesHandler : IRequestHandler<SaveFilesCommand>
{
    private readonly ITemplateRepository _repo;

    public SaveFilesHandler(ITemplateRepository repo) => _repo = repo;

    public async Task Handle(SaveFilesCommand request, CancellationToken cancellationToken)
    {
        var items = request.Files.Select((f, i) => new FileTemplateItem
        {
            SortOrder = i + 1,
            FileName = f.FileName,
            Required = f.Required,
            IsPublic = f.IsPublic,
            ViewRoles = f.ViewRoles,
            DeptId = f.DeptId,
            DeptName = f.DeptName,
            Remark = f.Remark
        }).ToList();

        await _repo.SaveFileItemsAsync(request.TemplateId, items);
    }
}
