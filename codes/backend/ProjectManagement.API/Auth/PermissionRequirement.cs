using Microsoft.AspNetCore.Authorization;

namespace ProjectManagement.API.Auth;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionCode { get; }

    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = permissionCode;
    }
}
