using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProjectManagement.Application.Auth;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Infrastructure.Data;

namespace ProjectManagement.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _db;

    public AuthController(IMediator mediator, IConfiguration configuration, AppDbContext db)
    {
        _mediator = mediator;
        _configuration = configuration;
        _db = db;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _mediator.Send(new LoginCommand(request.Username, request.Password));
        if (!result.Success)
            return Ok(new { code = 401, message = result.Message });

        var token = GenerateToken(result.UserId, result.RealName!, result.Role!);
        return Ok(new
        {
            code = 0,
            message = "success",
            data = new { token, realName = result.RealName, role = result.Role, userId = result.UserId }
        });
    }

    [HttpPost("register")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _mediator.Send(new RegisterCommand(request.Username, request.Password, request.RealName, request.Role));
        if (!result.Success)
            return Ok(new { code = 400, message = result.Message });

        // 自动分配默认 RBAC 角色
        var newUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        var userRole = await _db.Roles.FirstOrDefaultAsync(r => r.Code == "user");
        if (newUser != null && userRole != null && !await _db.UserRoles.AnyAsync(ur => ur.UserId == newUser.Id))
        {
            _db.UserRoles.Add(new UserRole { UserId = newUser.Id, RoleId = userRole.Id });
            await _db.SaveChangesAsync();
        }

        return Ok(new { code = 0, message = "success" });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(userIdClaim, out var userId);
        if (userId == 0) return Ok(new { code = 401, message = "未登录" });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Ok(new { code = 404, message = "用户不存在" });

        if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.Password))
            return Ok(new { code = 400, message = "原密码错误" });

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return Ok(new { code = 400, message = "新密码至少6位" });

        user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();

        // 清除该用户所有 token（提示重新登录）
        return Ok(new { code = 0, message = "密码修改成功，请重新登录" });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var realName = User.FindFirst("realName")?.Value ?? "";
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        long.TryParse(userIdClaim, out var userId);

        // 通过 UserRoles → RolePermissions 解析权限
        List<string> permissionCodes;
        if (await ProjectManagement.API.Auth.ProjectPermissionHelper.IsAdminAsync(_db, userId))
        {
            permissionCodes = await _db.Permissions.Select(p => p.Code).ToListAsync();
        }
        else
        {
            permissionCodes = await _db.UserRoles
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Code)
                .Distinct()
                .ToListAsync();
        }

        // 拥有项目资料菜单时，前端同步隐含查看类权限
        if (permissionCodes.Contains("project:list"))
        {
            if (!permissionCodes.Contains("project:list:view")) permissionCodes.Add("project:list:view");
            if (!permissionCodes.Contains("project:detail:view")) permissionCodes.Add("project:detail:view");
        }
        // 工作台用户可查看本人相关项目（与列表 API 自查规则一致）
        if (permissionCodes.Contains("workbench"))
        {
            if (!permissionCodes.Contains("project:detail:view")) permissionCodes.Add("project:detail:view");
        }

        // 构建菜单树（仅 Type=1 的菜单权限）
        var allMenus = await _db.Permissions
            .Where(p => p.Type == 1 && !p.Code.StartsWith("project:field:"))
            .OrderBy(p => p.SortOrder)
            .Select(p => new MenuFlat { Id = p.Id, Code = p.Code, Name = p.Name, ParentId = p.ParentId, Icon = p.Icon, Path = p.Path })
            .ToListAsync();

        var allowedCodes = permissionCodes.ToHashSet();

        // 拥有子级按钮权限时，向上展开父级菜单 code（如仅有「查看列表」也应显示「项目资料管理」）
        var allPermsForMenu = await _db.Permissions
            .Select(p => new { p.Id, p.Code, p.Type, p.ParentId })
            .ToListAsync();
        var permById = allPermsForMenu.ToDictionary(p => p.Id);
        foreach (var code in permissionCodes)
        {
            var perm = allPermsForMenu.FirstOrDefault(p => p.Code == code);
            if (perm == null) continue;
            long? parentId = perm.ParentId;
            while (parentId.HasValue && permById.TryGetValue(parentId.Value, out var parent))
            {
                if (parent.Type == 1)
                    allowedCodes.Add(parent.Code);
                parentId = parent.ParentId;
            }
        }

        // 收集用户有权访问的菜单 ID（含祖先节点）
        var menuIds = new HashSet<long>();
        var menuMap = allMenus.ToDictionary(m => m.Id);
        foreach (var m in allMenus.Where(m => allowedCodes.Contains(m.Code)))
        {
            menuIds.Add(m.Id);
            var current = m.Id;
            while (menuMap.TryGetValue(current, out var node) && node.ParentId.HasValue)
            {
                menuIds.Add(node.ParentId.Value);
                current = node.ParentId.Value;
            }
        }

        var visibleMenus = allMenus.Where(m => menuIds.Contains(m.Id)).ToList();
        var menuTree = BuildMenuTree(visibleMenus, null);

        return Ok(new
        {
            code = 0,
            message = "success",
            data = new
            {
                userId,
                realName,
                role,
                permissions = permissionCodes,
                menus = menuTree
            }
        });
    }

    private static List<object> BuildMenuTree(List<MenuFlat> items, long? parentId)
    {
        return items
            .Where(i => i.ParentId == parentId)
            .Select(i => (object)new
            {
                i.Id, i.Code, i.Name, i.Path, i.Icon,
                children = BuildMenuTree(items, (long)i.Id)
            })
            .ToList();
    }

    private string GenerateToken(long userId, string realName, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expireHours = double.Parse(_configuration["Jwt:ExpireHours"]!);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("realName", realName),
            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expireHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record LoginRequest(string Username, string Password);
public record RegisterRequest(string Username, string Password, string RealName, string Role);
public record ChangePasswordRequest(string OldPassword, string NewPassword);
internal class MenuFlat
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public long? ParentId { get; set; }
    public string? Icon { get; set; }
    public string? Path { get; set; }
}
