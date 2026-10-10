using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class BillingChargeLine
{
    private BillingChargeLine()
    {
    }

    public BillingChargeLine(
        Guid organizationId,
        Guid billingUnitBillId,
        ChargeType chargeType,
        string chargeNameSnapshot,
        ChargeSettingMode settingModeSnapshot,
        bool isMetered,
        decimal? rateSnapshot,
        decimal amount,
        decimal? previousReading,
        MeterReadingSource previousReadingSource,
        DateOnly? previousReadingDate,
        bool warningEnabled,
        decimal? warningThreshold)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        BillingUnitBillId = billingUnitBillId;
        ChargeType = chargeType;
        ChargeNameSnapshot = chargeNameSnapshot;
        SettingModeSnapshot = settingModeSnapshot;
        IsMetered = isMetered;
        RateSnapshot = rateSnapshot;
        Amount = amount;
        PreviousReading = previousReading;
        PreviousReadingSource = previousReadingSource;
        PreviousReadingDate = previousReadingDate;
        WarningEnabled = warningEnabled;
        WarningThreshold = warningThreshold;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid BillingUnitBillId { get; private set; }

    public ChargeType ChargeType { get; private set; }

    public string ChargeNameSnapshot { get; private set; } = string.Empty;

    public ChargeSettingMode SettingModeSnapshot { get; private set; }

    public bool IsMetered { get; private set; }

    public decimal? RateSnapshot { get; private set; }

    public decimal? PreviousReading { get; private set; }

    public DateOnly? PreviousReadingDate { get; private set; }

    public decimal? CurrentReading { get; private set; }

    public decimal? ConsumptionUnits { get; private set; }

    public MeterReadingSource PreviousReadingSource { get; private set; }

    public bool WarningEnabled { get; private set; }

    public decimal? WarningThreshold { get; private set; }

    public decimal Amount { get; private set; }

    public Guid? MeterPhotoId { get; private set; }

    public void SetMeterReadings(
        decimal? previousReading,
        DateOnly? previousReadingDate,
        decimal? currentReading,
        DateOnly? currentReadingDate,
        MeterReadingSource previousReadingSource,
        decimal? consumptionUnits,
        decimal amount)
    {
        PreviousReading = previousReading;
        PreviousReadingDate = previousReadingDate;
        CurrentReading = currentReading;
        CurrentReadingDate = currentReadingDate;
        PreviousReadingSource = previousReadingSource;
        ConsumptionUnits = consumptionUnits;
        Amount = amount;
    }

    public void SetMeterPhoto(Guid? meterPhotoId) => MeterPhotoId = meterPhotoId;

    public DateOnly? CurrentReadingDate { get; private set; }
}
