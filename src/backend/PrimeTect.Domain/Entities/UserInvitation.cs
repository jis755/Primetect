using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class UserInvitation
{
    private UserInvitation()
    {
    }

    public UserInvitation(Guid organizationId, Guid invitedByUserId, string email, UserRole role, string tokenHash, DateTimeOffset expiresAtUtc)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        InvitedByUserId = invitedByUserId;
        Email = email.Trim().ToLowerInvariant();
        Role = role;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    public void MarkAccepted() => AcceptedAtUtc = DateTimeOffset.UtcNow;
}
