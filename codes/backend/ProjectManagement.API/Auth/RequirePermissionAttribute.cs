using Microsoft.AspNetCore.Authorization;

namespace ProjectManagement.API.Auth;

public class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "permission:";

    public RequirePermissionAttribute(string permissionCode)
    {
        Policy = $"{PolicyPrefix}{permissionCode}";
    }
}
