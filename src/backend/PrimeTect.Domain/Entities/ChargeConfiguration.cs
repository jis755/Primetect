using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class ChargeConfiguration
{
    private ChargeConfiguration()
    {
    }

    public ChargeConfiguration(
        Guid organizationId,
        Guid propertyId,
        Guid? unitId,
        ChargeType chargeType,
        ChargeSettingMode mode,
        DateOnly billingMonth,
        decimal? amount,
        string? customLabel,
        ChargeSettingMode? warningMode = null,
        decimal? warningThreshold = null)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PropertyId = propertyId;
        UnitId = unitId;
        ChargeType = chargeType;
        Mode = mode;
        BillingMonth = billingMonth;
        Amount = amount;
        CustomLabel = string.IsNullOrWhiteSpace(customLabel) ? null : customLabel.Trim();
        SetWarning(warningMode, warningThreshold);
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid PropertyId { get; private set; }

    public Guid? UnitId { get; private set; }

    public ChargeType ChargeType { get; private set; }

    public ChargeSettingMode Mode { get; private set; }

    public DateOnly BillingMonth { get; private set; }

    public decimal? Amount { get; private set; }

    public string? CustomLabel { get; private set; }

    public ChargeSettingMode WarningMode { get; private set; }

    public decimal? WarningThreshold { get; private set; }

    public void Update(
        ChargeSettingMode mode,
        DateOnly billingMonth,
        decimal? amount,
        string? customLabel,
        ChargeSettingMode? warningMode = null,
        decimal? warningThreshold = null)
    {
        Mode = mode;
        BillingMonth = billingMonth;
        Amount = amount;
        CustomLabel = string.IsNullOrWhiteSpace(customLabel) ? null : customLabel.Trim();
        if (warningMode is not null)
        {
            SetWarning(warningMode, warningThreshold);
        }
    }

    private void SetWarning(ChargeSettingMode? warningMode, decimal? warningThreshold)
    {
        if (ChargeType != ChargeType.Electricity)
        {
            WarningMode = ChargeSettingMode.Disabled;
            WarningThreshold = null;
            return;
        }

        WarningMode = warningMode ?? (UnitId is null ? ChargeSettingMode.Custom : ChargeSettingMode.Inherited);
        WarningThreshold = WarningMode == ChargeSettingMode.Custom
            ? warningThreshold ?? 200m
            : null;
    }
}
