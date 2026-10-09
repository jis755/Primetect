using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class CollectionAuditEntry
{
    private CollectionAuditEntry()
    {
    }

    public CollectionAuditEntry(
        Guid organizationId,
        Guid collectionReportId,
        Guid? collectionReversalId,
        Guid actorUserId,
        CollectionAuditAction action,
        string? reason,
        string? beforeJson,
        string? afterJson)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CollectionReportId = collectionReportId;
        CollectionReversalId = collectionReversalId;
        ActorUserId = actorUserId;
        Action = action;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid CollectionReportId { get; private set; }

    public Guid? CollectionReversalId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public CollectionAuditAction Action { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string? Reason { get; private set; }

    public string? BeforeJson { get; private set; }

    public string? AfterJson { get; private set; }
}
