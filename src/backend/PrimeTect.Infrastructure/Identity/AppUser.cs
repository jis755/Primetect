using Microsoft.AspNetCore.Identity;
using PrimeTect.Domain.Enums;

namespace PrimeTect.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public Guid OrganizationId { get; set; }

    public UserRole Role { get; set; }

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Set by an Admin-assisted password reset; forces a restricted recovery session until changed.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>When the Admin-issued temporary password stops working.</summary>
    public DateTimeOffset? TemporaryPasswordExpiresAtUtc { get; set; }

    /// <summary>Distinguishes an Admin-assigned login phone (not SMS-verified) from a verified one.</summary>
    public bool AdminAssignedPhoneNumber { get; set; }
}
