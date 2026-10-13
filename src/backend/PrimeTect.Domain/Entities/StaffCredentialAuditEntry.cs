namespace PrimeTect.Domain.Entities;

/// <summary>
/// Audit of Admin-assisted staff credential actions (login-phone assignment and password reset).
/// It records the actor, target, reason, and time only; it never stores passwords or tokens.
/// </summary>
public sealed class StaffCredentialAuditEntry
{
    private StaffCredentialAuditEntry()
    {
    }

    public StaffCredentialAuditEntry(
        Guid organizationId,
        Guid targetUserId,
        Guid actorUserId,
        string action,
        string? reason,
        Guid submissionKey,
        DateTimeOffset? temporaryPasswordExpiresAtUtc,
        string? beforeJson,
        string? afterJson)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        TargetUserId = targetUserId;
        ActorUserId = actorUserId;
        Action = action;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        SubmissionKey = submissionKey;
        TemporaryPasswordExpiresAtUtc = temporaryPasswordExpiresAtUtc;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid TargetUserId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string? Reason { get; private set; }

    public Guid SubmissionKey { get; private set; }

    public DateTimeOffset? TemporaryPasswordExpiresAtUtc { get; private set; }

    public string? BeforeJson { get; private set; }

    public string? AfterJson { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }
}
