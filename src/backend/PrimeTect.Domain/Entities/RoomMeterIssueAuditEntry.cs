using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class RoomMeterIssueAuditEntry
{
    private RoomMeterIssueAuditEntry()
    {
    }

    public RoomMeterIssueAuditEntry(
        Guid organizationId,
        Guid roomMeterIssueId,
        Guid actorUserId,
        RoomMeterIssueAuditAction action,
        string? reason,
        string? beforeJson,
        string? afterJson)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        RoomMeterIssueId = roomMeterIssueId;
        ActorUserId = actorUserId;
        Action = action;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid RoomMeterIssueId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public RoomMeterIssueAuditAction Action { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string? Reason { get; private set; }
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }
}
