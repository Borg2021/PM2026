using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ProjectManagement.Infrastructure.Data;

namespace ProjectManagement.API.Auth;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly AppDbContext _db;

    public PermissionAuthorizationHandler(AppDbContext db) => _db = db;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            context.Fail();
            return;
        }

        if (await ProjectPermissionHelper.IsAdminAsync(_db, userId))
        {
            context.Succeed(requirement);
            return;
        }

        var userPermCodes = await ProjectPermissionHelper.GetUserPermissionCodesAsync(_db, userId);
        if (ProjectPermissionHelper.Satisfies(userPermCodes, requirement.PermissionCode))
            context.Succeed(requirement);
        else
            context.Fail();
    }
}
