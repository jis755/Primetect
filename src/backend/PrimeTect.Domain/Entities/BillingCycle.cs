using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class BillingCycle
{
    private BillingCycle()
    {
    }

    public BillingCycle(Guid organizationId, Guid propertyId, DateOnly billingMonth, Guid actorUserId)
    {
        if (billingMonth.Day != 1)
        {
            throw new ArgumentException("Billing month must be the first day of a month.", nameof(billingMonth));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PropertyId = propertyId;
        BillingMonth = billingMonth;
        Status = BillingCycleStatus.Draft;
        Version = 1;
        LastChangedByUserId = actorUserId;
        LastChangedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid PropertyId { get; private set; }

    public DateOnly BillingMonth { get; private set; }

    public BillingCycleStatus Status { get; private set; }

    public long Version { get; private set; }

    public Guid LastChangedByUserId { get; private set; }

    public DateTimeOffset LastChangedAtUtc { get; private set; }

    public Guid? SubmittedByUserId { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public Guid? ReturnedByUserId { get; private set; }

    public DateTimeOffset? ReturnedAtUtc { get; private set; }

    public string? ReturnReason { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public decimal? ApprovedTotal { get; private set; }

    public void MarkDataChanged(Guid actorUserId)
    {
        EnsureEditable();
        Version++;
        LastChangedByUserId = actorUserId;
        LastChangedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Submit(Guid actorUserId)
    {
        if (Status is not (BillingCycleStatus.Draft or BillingCycleStatus.Returned))
        {
            throw new InvalidOperationException("Only draft or returned billing cycles can be submitted.");
        }

        Status = BillingCycleStatus.Submitted;
        SubmittedByUserId = actorUserId;
        SubmittedAtUtc = DateTimeOffset.UtcNow;
        ReturnedByUserId = null;
        ReturnedAtUtc = null;
        ReturnReason = null;
        Version++;
    }

    public void Return(Guid actorUserId, string reason)
    {
        if (Status != BillingCycleStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted billing cycles can be returned.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A return reason is required.", nameof(reason));
        }

        Status = BillingCycleStatus.Returned;
        ReturnedByUserId = actorUserId;
        ReturnedAtUtc = DateTimeOffset.UtcNow;
        ReturnReason = reason.Trim();
        Version++;
    }

    public void Approve(Guid actorUserId, decimal approvedTotal)
    {
        if (Status != BillingCycleStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted billing cycles can be approved.");
        }

        if (actorUserId == LastChangedByUserId)
        {
            throw new InvalidOperationException("The person who last changed billing data cannot approve that version.");
        }

        Status = BillingCycleStatus.Approved;
        ApprovedByUserId = actorUserId;
        ApprovedAtUtc = DateTimeOffset.UtcNow;
        ApprovedTotal = approvedTotal;
        Version++;
    }

    private void EnsureEditable()
    {
        if (Status is not (BillingCycleStatus.Draft or BillingCycleStatus.Returned))
        {
            throw new InvalidOperationException("Submitted and approved billing cycles cannot be edited.");
        }
    }
}
