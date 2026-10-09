using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using PrimeTect.Infrastructure.Identity;

namespace PrimeTect.Api.Security;

public static class AuthenticationCookie
{
    public static async Task SignInAsync(HttpContext httpContext, AppUser user, bool isPersistent = false)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim("organization_id", user.OrganizationId.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        };
        var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties { IsPersistent = isPersistent, AllowRefresh = true };

        await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, properties);
    }
}
