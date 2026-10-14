using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

/// <summary>
/// Property-scoped electricity utility account. Customer and meter numbers are stored as text so
/// leading zeros survive round-tripping; stable internal IDs are the relational keys.
/// Preparation for a future bill-payment workflow only: no payments or provider calls are made.
/// </summary>
public sealed class UtilityAccount
{
    private UtilityAccount()
    {
    }

    public UtilityAccount(Guid organizationId, Guid propertyId, string providerName, string customerNumber, string? displayLabel, Guid actorUserId)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PropertyId = propertyId;
        ProviderName = providerName.Trim();
        CustomerNumber = customerNumber.Trim();
        DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? null : displayLabel.Trim();
        CreatedAtUtc = DateTimeOffset.UtcNow;
        CreatedByUserId = actorUserId;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid PropertyId { get; private set; }

    public string ProviderName { get; private set; } = string.Empty;

    public string CustomerNumber { get; private set; } = string.Empty;

    public string? DisplayLabel { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public void Update(string providerName, string customerNumber, string? displayLabel)
    {
        ProviderName = providerName.Trim();
        CustomerNumber = customerNumber.Trim();
        DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? null : displayLabel.Trim();
    }
}

/// <summary>
/// A shared incoming electricity main meter linked to a utility account. One main meter can supply
/// many rooms; it is deliberately separate from the room billing submeters.
/// </summary>
public sealed class UtilityMainMeter
{
    private UtilityMainMeter()
    {
    }

    public UtilityMainMeter(Guid organizationId, Guid propertyId, Guid utilityAccountId, string meterNumber, string? displayLabel, DateOnly? installedOn, Guid actorUserId)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PropertyId = propertyId;
        UtilityAccountId = utilityAccountId;
        MeterNumber = meterNumber.Trim();
        DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? null : displayLabel.Trim();
        InstalledOn = installedOn;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        CreatedByUserId = actorUserId;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid PropertyId { get; private set; }

    public Guid UtilityAccountId { get; private set; }

    public string MeterNumber { get; private set; } = string.Empty;

    public string? DisplayLabel { get; private set; }

    public DateOnly? InstalledOn { get; private set; }

    public DateOnly? EndedOn { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public bool IsActive => EndedOn is null;

    public void Update(Guid utilityAccountId, string meterNumber, string? displayLabel, DateOnly? installedOn)
    {
        UtilityAccountId = utilityAccountId;
        MeterNumber = meterNumber.Trim();
        DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? null : displayLabel.Trim();
        InstalledOn = installedOn;
    }

    public void End(DateOnly endedOn)
    {
        EndedOn = endedOn;
    }
}

/// <summary>
/// A room billing submeter installation. Exactly one installation is active per unit at a time.
/// History is ordered by <see cref="InstallationDate"/> and creation time; replacement links the new
/// installation to the one it supersedes. Opening readings are never invented: unknown identity or
/// baselines stay explicitly unknown.
/// </summary>
public sealed class MeterInstallation
{
    private MeterInstallation()
    {
    }

    public MeterInstallation(
        Guid organizationId,
        Guid propertyId,
        Guid unitId,
        Guid? mainMeterId,
        string? serialNumber,
        string? displayLabel,
        DateOnly? installationDate,
        decimal? openingReading,
        DateOnly? openingReadingDate,
        Guid? replacedInstallationId,
        bool identityKnown,
        Guid actorUserId)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PropertyId = propertyId;
        UnitId = unitId;
        MainMeterId = mainMeterId;
        SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim();
        DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? null : displayLabel.Trim();
        InstallationDate = installationDate;
        OpeningReading = openingReading;
        OpeningReadingDate = openingReadingDate;
        ReplacedInstallationId = replacedInstallationId;
        IdentityKnown = identityKnown;
        Version = 1;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        CreatedByUserId = actorUserId;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid PropertyId { get; private set; }

    public Guid UnitId { get; private set; }

    public Guid? MainMeterId { get; private set; }

    public string? SerialNumber { get; private set; }

    public string? DisplayLabel { get; private set; }

    public DateOnly? InstallationDate { get; private set; }

    public decimal? OpeningReading { get; private set; }

    public DateOnly? OpeningReadingDate { get; private set; }

    public Guid? ReplacedInstallationId { get; private set; }

    public bool IdentityKnown { get; private set; }

    public DateOnly? EndedOn { get; private set; }

    public DateTimeOffset? EndedAtUtc { get; private set; }

    public Guid? EndedByUserId { get; private set; }

    public string? EndReason { get; private set; }

    public decimal? FinalReading { get; private set; }

    public bool FinalReadingUnavailable { get; private set; }

    public long Version { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public bool IsActive => EndedOn is null;

    public void UpdateIdentity(Guid? mainMeterId, string? serialNumber, string? displayLabel, DateOnly? installationDate, bool identityKnown)
    {
        MainMeterId = mainMeterId;
        SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim();
        DisplayLabel = string.IsNullOrWhiteSpace(displayLabel) ? null : displayLabel.Trim();
        InstallationDate = installationDate;
        IdentityKnown = identityKnown;
        Version++;
    }

    public void End(DateOnly endedOn, Guid actorUserId, string? reason, decimal? finalReading, bool finalReadingUnavailable)
    {
        EndedOn = endedOn;
        EndedAtUtc = DateTimeOffset.UtcNow;
        EndedByUserId = actorUserId;
        EndReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        FinalReading = finalReading;
        FinalReadingUnavailable = finalReadingUnavailable;
        Version++;
    }

    /// <summary>
    /// Records an opening baseline on an installation that does not already have one. It never overwrites an
    /// existing baseline, closing reading, or replacement opening, and it never invents installation history.
    /// </summary>
    public void SetOpeningBaseline(decimal openingReading, DateOnly openingReadingDate)
    {
        if (OpeningReading is not null)
        {
            throw new InvalidOperationException("This installation already has an opening baseline.");
        }

        OpeningReading = openingReading;
        OpeningReadingDate = openingReadingDate;
        Version++;
    }
}

/// <summary>
/// Immutable per-installation reading segment for one metered billing charge line. A billing period
/// that spans a replacement stores one segment per installation and never subtracts across meters.
/// </summary>
public sealed class BillingMeterSegment
{
    private BillingMeterSegment()
    {
    }

    public BillingMeterSegment(
        Guid organizationId,
        Guid billingChargeLineId,
        Guid? meterInstallationId,
        int sortOrder,
        DateOnly? segmentStartDate,
        DateOnly? segmentEndDate,
        decimal? previousReading,
        MeterReadingSource previousReadingSource,
        DateOnly? previousReadingDate,
        decimal? currentReading,
        DateOnly? currentReadingDate,
        decimal? consumptionUnits,
        bool finalReadingUnavailable)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        BillingChargeLineId = billingChargeLineId;
        MeterInstallationId = meterInstallationId;
        SortOrder = sortOrder;
        SegmentStartDate = segmentStartDate;
        SegmentEndDate = segmentEndDate;
        PreviousReading = previousReading;
        PreviousReadingSource = previousReadingSource;
        PreviousReadingDate = previousReadingDate;
        CurrentReading = currentReading;
        CurrentReadingDate = currentReadingDate;
        ConsumptionUnits = consumptionUnits;
        FinalReadingUnavailable = finalReadingUnavailable;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid BillingChargeLineId { get; private set; }

    public Guid? MeterInstallationId { get; private set; }

    public int SortOrder { get; private set; }

    public DateOnly? SegmentStartDate { get; private set; }

    public DateOnly? SegmentEndDate { get; private set; }

    public decimal? PreviousReading { get; private set; }

    public MeterReadingSource PreviousReadingSource { get; private set; }

    public DateOnly? PreviousReadingDate { get; private set; }

    public decimal? CurrentReading { get; private set; }

    public DateOnly? CurrentReadingDate { get; private set; }

    public decimal? ConsumptionUnits { get; private set; }

    public bool FinalReadingUnavailable { get; private set; }
}

/// <summary>
/// Audit of meter installation setup, adoption, and replacement. One row per event.
/// </summary>
public sealed class MeterInstallationAuditEntry
{
    private MeterInstallationAuditEntry()
    {
    }

    public MeterInstallationAuditEntry(
        Guid organizationId,
        Guid propertyId,
        Guid unitId,
        Guid installationId,
        Guid? previousInstallationId,
        Guid actorUserId,
        MeterInstallationAuditAction action,
        DateOnly? replacementDate,
        string? reason,
        string? beforeJson,
        string? afterJson,
        Guid submissionKey)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PropertyId = propertyId;
        UnitId = unitId;
        InstallationId = installationId;
        PreviousInstallationId = previousInstallationId;
        ActorUserId = actorUserId;
        Action = action;
        ReplacementDate = replacementDate;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        SubmissionKey = submissionKey;
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid PropertyId { get; private set; }

    public Guid UnitId { get; private set; }

    public Guid InstallationId { get; private set; }

    public Guid? PreviousInstallationId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public MeterInstallationAuditAction Action { get; private set; }

    public DateOnly? ReplacementDate { get; private set; }

    public string? Reason { get; private set; }

    public string? BeforeJson { get; private set; }

    public string? AfterJson { get; private set; }

    public Guid SubmissionKey { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }
}
