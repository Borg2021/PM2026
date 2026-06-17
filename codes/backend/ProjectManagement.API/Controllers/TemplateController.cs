using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagement.Application.Templates.Commands;
using ProjectManagement.Application.Templates.Queries;

namespace ProjectManagement.API.Controllers;

[ApiController]
[Route("api/v1/templates")]
[Authorize]
public class TemplateController : ControllerBase
{
    private readonly IMediator _mediator;

    public TemplateController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? templateCode,
        [FromQuery] string? templateName,
        [FromQuery] int? templateType,
        [FromQuery] string? createdBy,
        [FromQuery] string? description,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = new GetTemplateListQuery(templateCode, templateName, templateType, createdBy, description, pageIndex, pageSize);
        var result = await _mediator.Send(query);
        return Ok(new { code = 0, message = "success", data = result });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTemplateRequest request)
    {
        var (userId, realName) = GetUserInfo();
        var command = new CreateTemplateCommand(request.TemplateCode, request.TemplateName, request.TemplateType, request.Description, userId, realName);
        var id = await _mediator.Send(command);
        return Ok(new { code = 0, message = "success", data = new { id } });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetDetail(long id)
    {
        var result = await _mediator.Send(new GetTemplateDetailQuery(id));
        if (result == null) return Ok(new { code = 404, message = "模板不存在" });
        return Ok(new { code = 0, message = "success", data = result });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateTemplateRequest request)
    {
        var (userId, _) = GetUserInfo();
        await _mediator.Send(new UpdateTemplateCommand(id, request.TemplateName, request.Description, userId));
        return Ok(new { code = 0, message = "success" });
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "admin,templateAdmin")]
    public async Task<IActionResult> Delete(long id)
    {
        await _mediator.Send(new DeleteTemplateCommand(id));
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPut("{id:long}/plan-nodes")]
    public async Task<IActionResult> SavePlanNodes(long id, [FromBody] SavePlanNodesRequest request)
    {
        await _mediator.Send(new SavePlanNodesCommand(id, request.Nodes));
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPut("{id:long}/milestones")]
    public async Task<IActionResult> SaveMilestones(long id, [FromBody] SaveMilestonesRequest request)
    {
        await _mediator.Send(new SaveMilestonesCommand(id, request.Milestones));
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPut("{id:long}/members")]
    public async Task<IActionResult> SaveMembers(long id, [FromBody] SaveMembersRequest request)
    {
        await _mediator.Send(new SaveMembersCommand(id, request.Members));
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPut("{id:long}/files")]
    public async Task<IActionResult> SaveFiles(long id, [FromBody] SaveFilesRequest request)
    {
        await _mediator.Send(new SaveFilesCommand(id, request.Files));
        return Ok(new { code = 0, message = "success" });
    }

    private (long userId, string realName) GetUserInfo()
    {
        var realName = User.FindFirst("realName")?.Value ?? "";
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(userIdClaim, out var userId);
        return (userId, realName);
    }
}

public class CreateTemplateRequest { public string TemplateCode { get; set; } = ""; public string TemplateName { get; set; } = ""; public int TemplateType { get; set; } public string? Description { get; set; } }
public class UpdateTemplateRequest { public string TemplateName { get; set; } = ""; public string? Description { get; set; } }
public class SavePlanNodesRequest { public List<PlanNodeInput> Nodes { get; set; } = new(); }
public class SaveMilestonesRequest { public List<MilestoneInput> Milestones { get; set; } = new(); }
public class SaveMembersRequest { public List<MemberInput> Members { get; set; } = new(); }
public class SaveFilesRequest { public List<FileInput> Files { get; set; } = new(); }
