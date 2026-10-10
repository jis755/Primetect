using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class MeterQrLink
{
    private MeterQrLink()
    {
    }

    public MeterQrLink(Guid organizationId, Guid propertyId, Guid unitId, Guid createdByUserId, string tokenHash, MeterQrPlacement placement)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PropertyId = propertyId;
        UnitId = unitId;
        CreatedByUserId = createdByUserId;
        TokenHash = tokenHash;
        Placement = placement;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid UnitId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public MeterQrPlacement Placement { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? RevokedByUserId { get; private set; }

    public bool IsActive => RevokedAtUtc is null;

    public void Revoke(Guid actorUserId)
    {
        RevokedAtUtc ??= DateTimeOffset.UtcNow;
        RevokedByUserId ??= actorUserId;
    }
}
