using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class BillingAuditEntry
{
    private BillingAuditEntry()
    {
    }

    public BillingAuditEntry(Guid organizationId, Guid billingCycleId, Guid actorUserId, BillingAuditAction action, string? reason, string? beforeJson, string? afterJson)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        BillingCycleId = billingCycleId;
        ActorUserId = actorUserId;
        Action = action;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid BillingCycleId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public BillingAuditAction Action { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string? Reason { get; private set; }

    public string? BeforeJson { get; private set; }

    public string? AfterJson { get; private set; }
}
