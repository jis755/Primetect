using System.Security.Claims;
using PrimeTect.Domain.Enums;

namespace PrimeTect.Api.Security;

public sealed record RequestUser(Guid UserId, Guid OrganizationId, UserRole Role)
{
    public bool IsAdmin => Role == UserRole.Admin;

    public bool IsManagerOrAdmin => Role is UserRole.Admin or UserRole.Manager;

    public bool IsStaff => Role == UserRole.BuildingStaff;
}

public static class RequestUserClaims
{
    public static RequestUser? GetRequestUser(this ClaimsPrincipal principal)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var organizationIdValue = principal.FindFirstValue("organization_id");
        var roleValue = principal.FindFirstValue(ClaimTypes.Role);

        return Guid.TryParse(userIdValue, out var userId)
            && Guid.TryParse(organizationIdValue, out var organizationId)
            && Enum.TryParse<UserRole>(roleValue, out var role)
            ? new RequestUser(userId, organizationId, role)
            : null;
    }
}
