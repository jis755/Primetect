using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class RoomMeterIssue
{
    private RoomMeterIssue()
    {
    }

    public RoomMeterIssue(
        Guid organizationId,
        Guid propertyId,
        Guid unitId,
        Guid reporterUserId,
        RoomMeterIssueType issueType,
        string description,
        DateOnly observedDate)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("An issue description is required.", nameof(description));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PropertyId = propertyId;
        UnitId = unitId;
        ReporterUserId = reporterUserId;
        IssueType = issueType;
        Description = description.Trim();
        ObservedDate = observedDate;
        Status = RoomMeterIssueStatus.Open;
        Version = 1;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        LastChangedAtUtc = CreatedAtUtc;
        LastChangedByUserId = reporterUserId;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid UnitId { get; private set; }
    public Guid ReporterUserId { get; private set; }
    public RoomMeterIssueType IssueType { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateOnly ObservedDate { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public RoomMeterIssueStatus Status { get; private set; }
    public long Version { get; private set; }
    public Guid LastChangedByUserId { get; private set; }
    public DateTimeOffset LastChangedAtUtc { get; private set; }
    public string? ResolutionReason { get; private set; }

    public void Acknowledge(Guid actorUserId, string? reason)
    {
        if (Status != RoomMeterIssueStatus.Open)
        {
            throw new InvalidOperationException("Only open room or meter issues can be acknowledged.");
        }

        Status = RoomMeterIssueStatus.Acknowledged;
        ResolutionReason = CleanReason(reason);
        Touch(actorUserId);
    }

    public void Resolve(Guid actorUserId, string reason)
    {
        if (Status != RoomMeterIssueStatus.Open && Status != RoomMeterIssueStatus.Acknowledged)
        {
            throw new InvalidOperationException("Only open or acknowledged room or meter issues can be resolved.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A resolution reason is required.", nameof(reason));
        }

        Status = RoomMeterIssueStatus.Resolved;
        ResolutionReason = reason.Trim();
        Touch(actorUserId);
    }

    private void Touch(Guid actorUserId)
    {
        Version++;
        LastChangedByUserId = actorUserId;
        LastChangedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string? CleanReason(string? reason) => string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
}
