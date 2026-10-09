using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class CollectionReversal
{
    private CollectionReversal()
    {
    }

    public CollectionReversal(Guid organizationId, Guid collectionReportId, Guid requestedByUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reversal reason is required.", nameof(reason));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        CollectionReportId = collectionReportId;
        RequestedByUserId = requestedByUserId;
        Reason = reason.Trim();
        Status = CollectionReversalStatus.Pending;
        Version = 1;
        LastChangedByUserId = requestedByUserId;
        LastChangedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid CollectionReportId { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public CollectionReversalStatus Status { get; private set; }

    public long Version { get; private set; }

    public Guid LastChangedByUserId { get; private set; }

    public DateTimeOffset LastChangedAtUtc { get; private set; }

    public string? ReturnReason { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public void Return(Guid actorUserId, string reason)
    {
        if (Status != CollectionReversalStatus.Pending)
        {
            throw new InvalidOperationException("Only pending reversals can be returned.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A return reason is required.", nameof(reason));
        }

        Status = CollectionReversalStatus.Returned;
        ReturnReason = reason.Trim();
        Version++;
        LastChangedByUserId = actorUserId;
        LastChangedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Approve(Guid actorUserId)
    {
        if (Status != CollectionReversalStatus.Pending)
        {
            throw new InvalidOperationException("Only pending reversals can be approved.");
        }

        if (actorUserId == LastChangedByUserId)
        {
            throw new InvalidOperationException("The person who requested or last changed this reversal cannot approve it.");
        }

        Status = CollectionReversalStatus.Approved;
        ApprovedByUserId = actorUserId;
        ApprovedAtUtc = DateTimeOffset.UtcNow;
        Version++;
        LastChangedByUserId = actorUserId;
        LastChangedAtUtc = ApprovedAtUtc.Value;
    }

    public void Resubmit(Guid actorUserId, string? updatedReason)
    {
        if (Status != CollectionReversalStatus.Returned)
        {
            throw new InvalidOperationException("Only returned reversals can be resubmitted.");
        }

        if (!string.IsNullOrWhiteSpace(updatedReason))
        {
            Reason = updatedReason.Trim();
        }

        Status = CollectionReversalStatus.Pending;
        ReturnReason = null;
        Version++;
        LastChangedByUserId = actorUserId;
        LastChangedAtUtc = DateTimeOffset.UtcNow;
    }
}
