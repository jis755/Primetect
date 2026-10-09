using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class ChargeConfiguration
{
    private ChargeConfiguration()
    {
    }

    public ChargeConfiguration(Guid organizationId, Guid propertyId, Guid? unitId, ChargeType chargeType, ChargeSettingMode mode, DateOnly billingMonth, decimal? amount, string? customLabel)
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

    public void Update(ChargeSettingMode mode, DateOnly billingMonth, decimal? amount, string? customLabel)
    {
        Mode = mode;
        BillingMonth = billingMonth;
        Amount = amount;
        CustomLabel = string.IsNullOrWhiteSpace(customLabel) ? null : customLabel.Trim();
    }
}
