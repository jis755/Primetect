using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using PrimeTect.Infrastructure.Identity;

namespace PrimeTect.Api.Security;

public static class AuthenticationCookie
{
    public static async Task SignInAsync(HttpContext httpContext, AppUser user, bool isPersistent = false, bool recoverySession = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("organization_id", user.OrganizationId.ToString()),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(SecurityClaims.SecurityStamp, user.SecurityStamp ?? string.Empty),
        };
        if (recoverySession)
        {
            claims.Add(new Claim(SecurityClaims.RecoverySession, "1"));
        }

        var identity = new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties { IsPersistent = isPersistent, AllowRefresh = true };

        await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, properties);
    }
}
