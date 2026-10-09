using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class CollectionReport
{
    private CollectionReport()
    {
    }

    public CollectionReport(
        Guid organizationId,
        Guid billingUnitBillId,
        Guid? occupancyAssignmentId,
        Guid unitId,
        string unitNameSnapshot,
        string? floorNameSnapshot,
        Guid? tenantId,
        string? tenantNameSnapshot,
        string? tenantPhoneSnapshot,
        DateOnly billingMonth,
        decimal billedAmountSnapshot,
        Guid reporterUserId,
        decimal amountReceived,
        DateOnly receivedDate,
        PaymentMethod paymentMethod,
        string? referenceNote,
        Guid submissionKey)
    {
        ValidateAmount(amountReceived);
        if (submissionKey == Guid.Empty)
        {
            throw new ArgumentException("A submission key is required.", nameof(submissionKey));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        BillingUnitBillId = billingUnitBillId;
        OccupancyAssignmentIdSnapshot = occupancyAssignmentId;
        UnitId = unitId;
        UnitNameSnapshot = unitNameSnapshot.Trim();
        FloorNameSnapshot = floorNameSnapshot;
        TenantIdSnapshot = tenantId;
        TenantNameSnapshot = tenantNameSnapshot;
        TenantPhoneSnapshot = tenantPhoneSnapshot;
        BillingMonth = billingMonth;
        BilledAmountSnapshot = billedAmountSnapshot;
        ReporterUserId = reporterUserId;
        AmountReceived = amountReceived;
        ReceivedDate = receivedDate;
        PaymentMethod = paymentMethod;
        ReferenceNote = CleanNote(referenceNote);
        SubmissionKey = submissionKey;
        Status = CollectionReportStatus.Pending;
        Version = 1;
        LastChangedByUserId = reporterUserId;
        LastChangedAtUtc = DateTimeOffset.UtcNow;
        SubmittedAtUtc = LastChangedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid BillingUnitBillId { get; private set; }

    public Guid? OccupancyAssignmentIdSnapshot { get; private set; }

    public Guid UnitId { get; private set; }

    public string UnitNameSnapshot { get; private set; } = string.Empty;

    public string? FloorNameSnapshot { get; private set; }

    public Guid? TenantIdSnapshot { get; private set; }

    public string? TenantNameSnapshot { get; private set; }

    public string? TenantPhoneSnapshot { get; private set; }

    public DateOnly BillingMonth { get; private set; }

    public decimal BilledAmountSnapshot { get; private set; }

    public Guid ReporterUserId { get; private set; }

    public DateTimeOffset SubmittedAtUtc { get; private set; }

    public decimal AmountReceived { get; private set; }

    public DateOnly ReceivedDate { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

    public string? ReferenceNote { get; private set; }

    public Guid SubmissionKey { get; private set; }

    public CollectionReportStatus Status { get; private set; }

    public long Version { get; private set; }

    public Guid LastChangedByUserId { get; private set; }

    public DateTimeOffset LastChangedAtUtc { get; private set; }

    public string? ReturnReason { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public void Correct(
        Guid actorUserId,
        decimal amountReceived,
        DateOnly receivedDate,
        PaymentMethod paymentMethod,
        string? referenceNote)
    {
        if (Status != CollectionReportStatus.Returned)
        {
            throw new InvalidOperationException("Only returned collection reports can be corrected.");
        }

        ValidateAmount(amountReceived);
        AmountReceived = amountReceived;
        ReceivedDate = receivedDate;
        PaymentMethod = paymentMethod;
        ReferenceNote = CleanNote(referenceNote);
        Status = CollectionReportStatus.Pending;
        ReturnReason = null;
        ApprovedByUserId = null;
        ApprovedAtUtc = null;
        Version++;
        LastChangedByUserId = actorUserId;
        LastChangedAtUtc = DateTimeOffset.UtcNow;
        SubmittedAtUtc = LastChangedAtUtc;
    }

    public void Return(Guid actorUserId, string reason)
    {
        if (Status != CollectionReportStatus.Pending)
        {
            throw new InvalidOperationException("Only pending collection reports can be returned.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A return reason is required.", nameof(reason));
        }

        Status = CollectionReportStatus.Returned;
        ReturnReason = reason.Trim();
        Version++;
        LastChangedByUserId = actorUserId;
        LastChangedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Approve(Guid actorUserId)
    {
        if (Status != CollectionReportStatus.Pending)
        {
            throw new InvalidOperationException("Only pending collection reports can be approved.");
        }

        if (actorUserId == LastChangedByUserId)
        {
            throw new InvalidOperationException("The person who submitted or last changed this collection report cannot approve it.");
        }

        Status = CollectionReportStatus.Approved;
        ApprovedByUserId = actorUserId;
        ApprovedAtUtc = DateTimeOffset.UtcNow;
        Version++;
        LastChangedByUserId = actorUserId;
        LastChangedAtUtc = ApprovedAtUtc.Value;
    }

    private static void ValidateAmount(decimal amountReceived)
    {
        if (amountReceived <= 0m)
        {
            throw new ArgumentException("Amount received must be greater than zero.", nameof(amountReceived));
        }
    }

    private static string? CleanNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
