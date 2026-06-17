using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectManagement.Application.PlanBundles.Commands;
using ProjectManagement.Application.PlanBundles.Queries;

namespace ProjectManagement.API.Controllers;

[ApiController]
[Route("api/v1/plan-bundles")]
[Authorize]
public class PlanBundleController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlanBundleController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? keyword,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _mediator.Send(new GetPlanBundleListQuery(keyword, pageIndex, pageSize));
        return Ok(new { code = 0, message = "success", data = result });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetDetail(long id)
    {
        var result = await _mediator.Send(new GetPlanBundleDetailQuery(id));
        if (result == null) return Ok(new { code = 404, message = "模板集不存在" });
        return Ok(new { code = 0, message = "success", data = result });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePlanBundleRequest request)
    {
        var (userId, realName) = GetUserInfo();
        var id = await _mediator.Send(new CreatePlanBundleCommand(
            request.Name, request.Description, userId, realName, request.Items));
        return Ok(new { code = 0, message = "success", data = new { id } });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdatePlanBundleRequest request)
    {
        await _mediator.Send(new UpdatePlanBundleCommand(id, request.Name, request.Description, request.Items));
        return Ok(new { code = 0, message = "success" });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _mediator.Send(new DeletePlanBundleCommand(id));
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPost("{id:long}/assemble")]
    public async Task<IActionResult> Assemble(long id, [FromBody] AssembleRequest request)
    {
        var (userId, realName) = GetUserInfo();
        var newId = await _mediator.Send(new AssemblePlanBundleCommand(id, request.Name, userId, realName));
        return Ok(new { code = 0, message = "success", data = new { id = newId } });
    }

    private (long userId, string realName) GetUserInfo()
    {
        var realName = User.FindFirst("realName")?.Value ?? "";
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(userIdClaim, out var userId);
        return (userId, realName);
    }
}

public class CreatePlanBundleRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<PlanBundleItemInput> Items { get; set; } = new();
}

public class UpdatePlanBundleRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<PlanBundleItemInput> Items { get; set; } = new();
}

public class AssembleRequest
{
    public string Name { get; set; } = "";
}
