using Microsoft.AspNetCore.Identity;
using PrimeTect.Domain.Enums;

namespace PrimeTect.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public Guid OrganizationId { get; set; }

    public UserRole Role { get; set; }

    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
