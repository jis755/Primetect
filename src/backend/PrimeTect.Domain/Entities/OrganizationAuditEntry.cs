namespace PrimeTect.Domain.Entities;

public sealed class OrganizationAuditEntry
{
    private OrganizationAuditEntry()
    {
    }

    public OrganizationAuditEntry(Guid organizationId, Guid actorUserId, string action, string? reason, string? beforeJson, string? afterJson)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ActorUserId = actorUserId;
        Action = action;
        Reason = reason;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public string? Reason { get; private set; }

    public string? BeforeJson { get; private set; }

    public string? AfterJson { get; private set; }
}
