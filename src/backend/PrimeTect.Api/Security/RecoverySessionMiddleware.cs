namespace PrimeTect.Api.Security;

/// <summary>
/// Server-side enforcement that a restricted recovery session can only reach session status, the required
/// password change, logout, and the CSRF bootstrap. Every operational endpoint is blocked here rather than
/// relying on frontend navigation or a client-provided claim.
/// </summary>
public sealed class RecoverySessionMiddleware(RequestDelegate next)
{
    private static readonly string[] AllowedPaths =
    [
        "/api/auth/me",
        "/api/auth/csrf",
        "/api/auth/logout",
        "/api/auth/change-temporary-password",
        "/api/health",
        "/health",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var principal = context.User;
        if (principal.Identity?.IsAuthenticated == true && principal.HasClaim(SecurityClaims.RecoverySession, "1"))
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (!AllowedPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "Change your temporary password before continuing." });
                return;
            }
        }

        await next(context);
    }
}
