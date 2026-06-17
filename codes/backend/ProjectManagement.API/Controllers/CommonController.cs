using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectManagement.Infrastructure.Data;

namespace ProjectManagement.API.Controllers;

[ApiController]
[Route("api/v1")]
[AllowAnonymous]
public class CommonController : ControllerBase
{
    private readonly AppDbContext _db;

    public CommonController(AppDbContext db) => _db = db;

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var depts = await _db.Departments.OrderBy(d => d.SortOrder).Select(d => new { d.Id, d.Name, d.ParentId, d.LeaderId, LeaderName = d.Leader != null ? d.Leader.RealName : null }).ToListAsync();
        return Ok(new { code = 0, message = "success", data = depts });
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _db.RoleDicts.Select(r => new { r.Id, r.Name }).ToListAsync();
        return Ok(new { code = 0, message = "success", data = roles });
    }

    [HttpGet("users/search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string keyword = "", [FromQuery] long? departmentId = null, [FromQuery] long? functionId = null)
    {
        var query = _db.Users
            .Include(u => u.UserFunctions).ThenInclude(uf => uf.Function)
            .Where(u => u.Status == 1 && (u.RealName.Contains(keyword) || u.Username.Contains(keyword)));
        if (departmentId.HasValue)
            query = query.Where(u => u.DepartmentId == departmentId.Value);
        if (functionId.HasValue)
            query = query.Where(u => u.UserFunctions.Any(uf => uf.FunctionId == functionId.Value));
        var users = await query
            .Select(u => new { u.Id, u.RealName, u.Username, u.DepartmentId, DepartmentName = u.Department != null ? u.Department.Name : null, u.Role, FunctionIds = u.UserFunctions.Select(uf => uf.FunctionId).ToList(), FunctionNames = string.Join("、", u.UserFunctions.Select(uf => uf.Function.Name)) })
            .OrderBy(u => u.Id)
            .Take(string.IsNullOrEmpty(keyword) ? 500 : 50)
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = users });
    }

    [HttpGet("dicts/{dictType}")]
    public async Task<IActionResult> GetDictByType(string dictType)
    {
        var items = await _db.DictItems
            .Where(d => d.DictType == dictType && d.Status == 1)
            .OrderBy(d => d.SortOrder)
            .Select(d => new { d.Id, d.DictCode, d.DictLabel })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = items });
    }

    /// <summary>职能列表（项目编辑等页面使用，登录即可访问）</summary>
    [Authorize]
    [HttpGet("functions")]
    public async Task<IActionResult> GetFunctions()
    {
        var list = await _db.Functions
            .OrderBy(f => f.SortOrder)
            .Select(f => new { f.Id, f.Code, f.Name, f.Description, f.SortOrder })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = list });
    }

    /// <summary>按 key 读取系统参数（项目任务序号规则等）</summary>
    [Authorize]
    [HttpGet("sys-params/{key}")]
    public async Task<IActionResult> GetSysParamByKey(string key)
    {
        var param = await _db.SysParams.FirstOrDefaultAsync(p => p.ParamKey == key);
        if (param == null) return Ok(new { code = 0, message = "success", data = new { paramKey = "", paramValue = "" } });
        return Ok(new { code = 0, message = "success", data = new { param.ParamKey, param.ParamValue } });
    }
}
