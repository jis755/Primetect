namespace PrimeTect.Domain.Entities;

public sealed class UserNotification
{
    private UserNotification()
    {
    }

    public UserNotification(Guid organizationId, Guid recipientUserId, Guid propertyId, Guid roomMeterIssueId)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        RecipientUserId = recipientUserId;
        PropertyId = propertyId;
        RoomMeterIssueId = roomMeterIssueId;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid RoomMeterIssueId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }

    public void MarkRead()
    {
        ReadAtUtc ??= DateTimeOffset.UtcNow;
    }
}
