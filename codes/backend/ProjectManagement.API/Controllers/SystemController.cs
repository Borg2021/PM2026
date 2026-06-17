using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectManagement.Application.System;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Infrastructure.Data;

namespace ProjectManagement.API.Controllers;

[ApiController]
[Route("api/v1/system")]
[Authorize(Roles = "admin")]
public class SystemController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly AppDbContext _db;

    public SystemController(IMediator mediator, AppDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    /* ===================== 用户管理 ===================== */

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? keyword,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? departmentIds = null)
    {
        var result = await _mediator.Send(new GetUserListQuery(keyword, pageIndex, pageSize, departmentIds));
        return Ok(new { code = 0, message = "success", data = result });
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var id = await _mediator.Send(new CreateUserCommand(request.Username, request.Password, request.RealName, request.Role, request.DepartmentId, request.FunctionIds));
        return Ok(new { code = 0, message = "success", data = new { id } });
    }

    [HttpPut("users/{id:long}")]
    public async Task<IActionResult> UpdateUser(long id, [FromBody] UpdateUserRequest request)
    {
        await _mediator.Send(new UpdateUserCommand(id, request.RealName, request.Role, request.Status, request.DepartmentId, request.FunctionIds));
        return Ok(new { code = 0, message = "success" });
    }

    [HttpPut("users/{id:long}/reset-password")]
    public async Task<IActionResult> ResetPassword(long id, [FromBody] ResetPasswordRequest request)
    {
        await _mediator.Send(new ResetPasswordCommand(id, request.NewPassword));
        return Ok(new { code = 0, message = "success" });
    }

    [HttpDelete("users/{id:long}")]
    public async Task<IActionResult> DeleteUser(long id)
    {
        await _mediator.Send(new DeleteUserCommand(id));
        return Ok(new { code = 0, message = "success" });
    }

    /* ===================== 用户 RBAC 角色 ===================== */

    [HttpGet("users/{id:long}/roles")]
    public async Task<IActionResult> GetUserRoles(long id)
    {
        var roleIds = await _db.UserRoles
            .Where(ur => ur.UserId == id)
            .Select(ur => ur.RoleId)
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = roleIds });
    }

    [HttpPut("users/{id:long}/roles")]
    public async Task<IActionResult> UpdateUserRoles(long id, [FromBody] UpdateUserRolesRequest request)
    {
        var existing = await _db.UserRoles.Where(ur => ur.UserId == id).ToListAsync();
        _db.UserRoles.RemoveRange(existing);

        _db.UserRoles.AddRange(
            request.RoleIds.Select(rid => new ProjectManagement.Domain.Entities.UserRole { UserId = id, RoleId = rid })
        );
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    /* ===================== 部门管理 ===================== */

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var depts = await _db.Departments
            .OrderBy(d => d.SortOrder)
            .Select(d => new { d.Id, d.Name, d.ParentId, d.SortOrder })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = depts });
    }

    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment([FromBody] SaveDepartmentRequest request)
    {
        var dept = new ProjectManagement.Domain.Entities.Department
        {
            Name = request.Name.Trim(),
            ParentId = request.ParentId,
            SortOrder = request.SortOrder
        };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success", data = new { id = dept.Id } });
    }

    [HttpPut("departments/{id:long}")]
    public async Task<IActionResult> UpdateDepartment(long id, [FromBody] SaveDepartmentRequest request)
    {
        var dept = await _db.Departments.FindAsync(id);
        if (dept == null) return Ok(new { code = 404, message = "部门不存在" });

        if (request.ParentId.HasValue && request.ParentId.Value == id)
            throw new InvalidOperationException("不能将自身设为上级部门");

        dept.Name = request.Name.Trim();
        dept.ParentId = request.ParentId;
        dept.SortOrder = request.SortOrder;
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    [HttpDelete("departments/{id:long}")]
    public async Task<IActionResult> DeleteDepartment(long id)
    {
        var hasChildren = await _db.Departments.AnyAsync(d => d.ParentId == id);
        if (hasChildren) throw new InvalidOperationException("存在子部门，无法删除");

        var dept = await _db.Departments.FindAsync(id);
        if (dept == null) return Ok(new { code = 404, message = "部门不存在" });

        _db.Departments.Remove(dept);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    /* ===================== 角色管理 ===================== */

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _db.RoleDicts
            .OrderBy(r => r.Id)
            .Select(r => new { r.Id, r.Name })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = roles });
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole([FromBody] SaveRoleRequest request)
    {
        var name = request.Name.Trim();
        if (await _db.RoleDicts.AnyAsync(r => r.Name == name))
            throw new InvalidOperationException("角色名称已存在");

        var role = new ProjectManagement.Domain.Entities.RoleDict { Name = name };
        _db.RoleDicts.Add(role);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success", data = new { id = role.Id } });
    }

    [HttpPut("roles/{id:long}")]
    public async Task<IActionResult> UpdateRole(long id, [FromBody] SaveRoleRequest request)
    {
        var name = request.Name.Trim();
        if (await _db.RoleDicts.AnyAsync(r => r.Name == name && r.Id != id))
            throw new InvalidOperationException("角色名称已存在");

        var role = await _db.RoleDicts.FindAsync(id);
        if (role == null) return Ok(new { code = 404, message = "角色不存在" });

        role.Name = name;
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    [HttpDelete("roles/{id:long}")]
    public async Task<IActionResult> DeleteRole(long id)
    {
        var role = await _db.RoleDicts.FindAsync(id);
        if (role == null) return Ok(new { code = 404, message = "角色不存在" });

        _db.RoleDicts.Remove(role);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    /* ===================== RBAC 角色管理 ===================== */

    [HttpGet("rbac-roles")]
    public async Task<IActionResult> GetRbacRoles()
    {
        var roles = await _db.Roles
            .OrderBy(r => r.Id)
            .Select(r => new { r.Id, r.Name, r.Code, r.Description, r.Status, r.DataScope })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = roles });
    }

    [HttpPost("rbac-roles")]
    public async Task<IActionResult> CreateRbacRole([FromBody] SaveRbacRoleRequest request)
    {
        var code = request.Code.Trim().ToLower().Replace(" ", "_");
        if (await _db.Roles.AnyAsync(r => r.Code == code))
            throw new InvalidOperationException("角色编码已存在");

        var role = new ProjectManagement.Domain.Entities.Role
        {
            Name = request.Name.Trim(),
            Code = code,
            Description = request.Description?.Trim(),
            Status = 1,
            DataScope = request.DataScope ?? 5
        };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success", data = new { id = role.Id } });
    }

    [HttpPut("rbac-roles/{id:long}")]
    public async Task<IActionResult> UpdateRbacRole(long id, [FromBody] SaveRbacRoleRequest request)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null) return Ok(new { code = 404, message = "角色不存在" });

        role.Name = request.Name.Trim();
        role.Description = request.Description?.Trim();
        if (request.DataScope.HasValue)
            role.DataScope = request.DataScope.Value;
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    [HttpDelete("rbac-roles/{id:long}")]
    public async Task<IActionResult> DeleteRbacRole(long id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null) return Ok(new { code = 404, message = "角色不存在" });

        _db.RolePermissions.RemoveRange(_db.RolePermissions.Where(rp => rp.RoleId == id));
        _db.UserRoles.RemoveRange(_db.UserRoles.Where(ur => ur.RoleId == id));
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    /* ===================== 权限管理 ===================== */

    [HttpGet("permissions/tree")]
    public async Task<IActionResult> GetPermissionTree()
    {
        var all = await _db.Permissions
            .OrderBy(p => p.SortOrder)
            .Select(p => new PermFlat { Id = p.Id, Code = p.Code, Name = p.Name, ParentId = p.ParentId, Type = p.Type, SortOrder = p.SortOrder, Icon = p.Icon, Path = p.Path })
            .ToListAsync();

        var tree = BuildPermTree(all, null);
        return Ok(new { code = 0, message = "success", data = tree });
    }

    [HttpGet("roles/{id:long}/permissions")]
    public async Task<IActionResult> GetRolePermissions(long id)
    {
        var permissionIds = await _db.RolePermissions
            .Where(rp => rp.RoleId == id)
            .Select(rp => rp.PermissionId)
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = permissionIds });
    }

    [HttpPut("roles/{id:long}/permissions")]
    public async Task<IActionResult> UpdateRolePermissions(long id, [FromBody] UpdateRolePermissionsRequest request)
    {
        var existing = await _db.RolePermissions.Where(rp => rp.RoleId == id).ToListAsync();
        _db.RolePermissions.RemoveRange(existing);

        _db.RolePermissions.AddRange(
            request.PermissionIds.Select(pid => new ProjectManagement.Domain.Entities.RolePermission { RoleId = id, PermissionId = pid })
        );
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    private static List<object> BuildPermTree(List<PermFlat> items, long? parentId)
    {
        return items
            .Where(i => i.ParentId == parentId)
            .Select(i => (object)new
            {
                i.Id, i.Code, i.Name, i.ParentId, i.Type, i.SortOrder, i.Icon, i.Path,
                children = BuildPermTree(items, (long)i.Id)
            })
            .ToList();
    }

    /* ===================== 字典类型管理 ===================== */

    [HttpGet("dict-types")]
    public async Task<IActionResult> GetDictTypes()
    {
        var types = await _db.DictTypes
            .OrderBy(t => t.Id)
            .Select(t => new
            {
                t.Id,
                t.DictTypeCode,
                t.DictTypeName,
                t.Remark,
                itemCount = _db.DictItems.Count(d => d.DictType == t.DictTypeCode)
            })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = types });
    }

    [HttpPost("dict-types")]
    public async Task<IActionResult> CreateDictType([FromBody] SaveDictTypeRequest request)
    {
        var code = request.DictTypeCode.Trim();
        if (await _db.DictTypes.AnyAsync(t => t.DictTypeCode == code))
            return Ok(new { code = 400, message = "字典类型编号已存在" });

        var entity = new ProjectManagement.Domain.Entities.DictType
        {
            DictTypeCode = code,
            DictTypeName = request.DictTypeName.Trim(),
            Remark = request.Remark?.Trim()
        };
        _db.DictTypes.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success", data = new { id = entity.Id } });
    }

    [HttpPut("dict-types/{id:long}")]
    public async Task<IActionResult> UpdateDictType(long id, [FromBody] SaveDictTypeRequest request)
    {
        var entity = await _db.DictTypes.FindAsync(id);
        if (entity == null) return Ok(new { code = 404, message = "字典类型不存在" });

        var newCode = request.DictTypeCode.Trim();
        if (await _db.DictTypes.AnyAsync(t => t.DictTypeCode == newCode && t.Id != id))
            return Ok(new { code = 400, message = "字典类型编号已存在" });

        entity.DictTypeCode = newCode;
        entity.DictTypeName = request.DictTypeName.Trim();
        entity.Remark = request.Remark?.Trim();
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    [HttpDelete("dict-types/{id:long}")]
    public async Task<IActionResult> DeleteDictType(long id)
    {
        var entity = await _db.DictTypes.FindAsync(id);
        if (entity == null) return Ok(new { code = 404, message = "字典类型不存在" });

        _db.DictTypes.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    /* ===================== 字典管理 ===================== */

    [HttpGet("dicts")]
    public async Task<IActionResult> GetDicts([FromQuery] string? dictType)
    {
        var query = _db.DictItems.AsQueryable();
        if (!string.IsNullOrEmpty(dictType))
            query = query.Where(d => d.DictType == dictType);
        var list = await query
            .OrderBy(d => d.DictType).ThenBy(d => d.SortOrder)
            .Select(d => new { d.Id, d.DictType, d.DictCode, d.DictLabel, d.SortOrder, d.Status })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = list });
    }

    [HttpPost("dicts")]
    public async Task<IActionResult> CreateDict([FromBody] SaveDictRequest request)
    {
        var item = new ProjectManagement.Domain.Entities.DictItem
        {
            DictType = request.DictType.Trim(),
            DictCode = request.DictCode.Trim(),
            DictLabel = request.DictLabel.Trim(),
            SortOrder = request.SortOrder
        };
        _db.DictItems.Add(item);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success", data = new { id = item.Id } });
    }

    [HttpPut("dicts/{id:long}")]
    public async Task<IActionResult> UpdateDict(long id, [FromBody] SaveDictRequest request)
    {
        var item = await _db.DictItems.FindAsync(id);
        if (item == null) return Ok(new { code = 404, message = "字典项不存在" });

        item.DictType = request.DictType.Trim();
        item.DictCode = request.DictCode.Trim();
        item.DictLabel = request.DictLabel.Trim();
        item.SortOrder = request.SortOrder;
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    [HttpDelete("dicts/{id:long}")]
    public async Task<IActionResult> DeleteDict(long id)
    {
        var item = await _db.DictItems.FindAsync(id);
        if (item == null) return Ok(new { code = 404, message = "字典项不存在" });

        _db.DictItems.Remove(item);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    /* ===================== 系统参数 ===================== */

    [HttpGet("sys-params")]
    public async Task<IActionResult> GetSysParams()
    {
        var list = await _db.SysParams
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.ParamKey, p.ParamValue, p.Description })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = list });
    }

    [HttpGet("sys-params/{key}")]
    public async Task<IActionResult> GetSysParamByKey(string key)
    {
        var param = await _db.SysParams.FirstOrDefaultAsync(p => p.ParamKey == key);
        if (param == null) return Ok(new { code = 0, message = "success", data = new { paramKey = "", paramValue = "" } });
        return Ok(new { code = 0, message = "success", data = new { param.ParamKey, param.ParamValue } });
    }

    [HttpPost("sys-params")]
    public async Task<IActionResult> CreateSysParam([FromBody] SaveSysParamRequest request)
    {
        var key = request.ParamKey.Trim();
        if (await _db.SysParams.AnyAsync(p => p.ParamKey == key))
            return Ok(new { code = 400, message = "参数键已存在" });

        var entity = new ProjectManagement.Domain.Entities.SysParam
        {
            ParamKey = key,
            ParamValue = request.ParamValue.Trim(),
            Description = request.Description?.Trim()
        };
        _db.SysParams.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success", data = new { id = entity.Id } });
    }

    [HttpPut("sys-params/{id:long}")]
    public async Task<IActionResult> UpdateSysParam(long id, [FromBody] SaveSysParamRequest request)
    {
        var entity = await _db.SysParams.FindAsync(id);
        if (entity == null) return Ok(new { code = 404, message = "参数不存在" });

        var newKey = request.ParamKey.Trim();
        if (await _db.SysParams.AnyAsync(p => p.ParamKey == newKey && p.Id != id))
            return Ok(new { code = 400, message = "参数键已存在" });

        entity.ParamKey = newKey;
        entity.ParamValue = request.ParamValue.Trim();
        entity.Description = request.Description?.Trim();
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    [HttpDelete("sys-params/{id:long}")]
    public async Task<IActionResult> DeleteSysParam(long id)
    {
        var entity = await _db.SysParams.FindAsync(id);
        if (entity == null) return Ok(new { code = 404, message = "参数不存在" });

        _db.SysParams.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    /* ===================== 职能管理 ===================== */

    [HttpGet("functions")]
    public async Task<IActionResult> GetFunctions()
    {
        var list = await _db.Functions
            .OrderBy(f => f.SortOrder)
            .Select(f => new { f.Id, f.Code, f.Name, f.Description, f.SortOrder })
            .ToListAsync();
        return Ok(new { code = 0, message = "success", data = list });
    }

    [HttpPost("functions")]
    public async Task<IActionResult> CreateFunction([FromBody] SaveFunctionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return Ok(new { code = 400, message = "职能编号不能为空" });
        if (string.IsNullOrWhiteSpace(request.Name))
            return Ok(new { code = 400, message = "职能名称不能为空" });
        if (await _db.Functions.AnyAsync(f => f.Code == request.Code))
            return Ok(new { code = 400, message = "职能编号已存在" });

        var entity = new Function { Code = request.Code, Name = request.Name, Description = request.Description, SortOrder = request.SortOrder };
        _db.Functions.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success", data = new { entity.Id } });
    }

    [HttpPut("functions/{id:long}")]
    public async Task<IActionResult> UpdateFunction(long id, [FromBody] SaveFunctionRequest request)
    {
        var entity = await _db.Functions.FindAsync(id);
        if (entity == null) return Ok(new { code = 404, message = "职能不存在" });
        if (string.IsNullOrWhiteSpace(request.Code))
            return Ok(new { code = 400, message = "职能编号不能为空" });
        if (string.IsNullOrWhiteSpace(request.Name))
            return Ok(new { code = 400, message = "职能名称不能为空" });
        if (await _db.Functions.AnyAsync(f => f.Code == request.Code && f.Id != id))
            return Ok(new { code = 400, message = "职能编号已存在" });

        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.SortOrder = request.SortOrder;
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }

    [HttpDelete("functions/{id:long}")]
    public async Task<IActionResult> DeleteFunction(long id)
    {
        var entity = await _db.Functions.FindAsync(id);
        if (entity == null) return Ok(new { code = 404, message = "职能不存在" });
        _db.Functions.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(new { code = 0, message = "success" });
    }
}

/* ---- 请求体 DTO ---- */

public class CreateUserRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string RealName { get; set; } = "";
    public string Role { get; set; } = "";
    public long? DepartmentId { get; set; }
    public List<long> FunctionIds { get; set; } = new();
}

public class UpdateUserRequest
{
    public string RealName { get; set; } = "";
    public string Role { get; set; } = "";
    public int Status { get; set; }
    public long? DepartmentId { get; set; }
    public List<long> FunctionIds { get; set; } = new();
}

public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = "";
}

public class SaveDepartmentRequest
{
    public string Name { get; set; } = "";
    public long? ParentId { get; set; }
    public int SortOrder { get; set; }
}

public class SaveRoleRequest
{
    public string Name { get; set; } = "";
}

public class SaveDictTypeRequest
{
    public string DictTypeCode { get; set; } = "";
    public string DictTypeName { get; set; } = "";
    public string? Remark { get; set; }
}

public class SaveDictRequest
{
    public string DictType { get; set; } = "";
    public string DictCode { get; set; } = "";
    public string DictLabel { get; set; } = "";
    public int SortOrder { get; set; }
}

public class SaveSysParamRequest
{
    public string ParamKey { get; set; } = "";
    public string ParamValue { get; set; } = "";
    public string? Description { get; set; }
}

public class UpdateUserRolesRequest
{
    public List<long> RoleIds { get; set; } = new();
}

public class SaveRbacRoleRequest
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    public int? DataScope { get; set; }
}

public class UpdateRolePermissionsRequest
{
    public List<long> PermissionIds { get; set; } = new();
}

internal class PermFlat
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public long? ParentId { get; set; }
    public int Type { get; set; }
    public int SortOrder { get; set; }
    public string? Icon { get; set; }
    public string? Path { get; set; }
}

public class SaveFunctionRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}
