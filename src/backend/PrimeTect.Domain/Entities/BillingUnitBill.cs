namespace PrimeTect.Domain.Entities;

public sealed class BillingUnitBill
{
    private BillingUnitBill()
    {
    }

    public BillingUnitBill(
        Guid organizationId,
        Guid billingCycleId,
        Guid unitId,
        string unitNameSnapshot,
        Guid? occupancyAssignmentId,
        Guid? tenantId,
        string? tenantNameSnapshot,
        string? tenantPhoneSnapshot,
        DateOnly? occupancyStartDate,
        DateOnly? occupancyEndDate,
        string? blockingReason)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        BillingCycleId = billingCycleId;
        UnitId = unitId;
        UnitNameSnapshot = unitNameSnapshot;
        OccupancyAssignmentId = occupancyAssignmentId;
        TenantId = tenantId;
        TenantNameSnapshot = tenantNameSnapshot;
        TenantPhoneSnapshot = tenantPhoneSnapshot;
        OccupancyStartDate = occupancyStartDate;
        OccupancyEndDate = occupancyEndDate;
        BlockingReason = blockingReason;
        Total = 0m;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid BillingCycleId { get; private set; }

    public Guid UnitId { get; private set; }

    public string UnitNameSnapshot { get; private set; } = string.Empty;

    public Guid? OccupancyAssignmentId { get; private set; }

    public Guid? TenantId { get; private set; }

    public string? TenantNameSnapshot { get; private set; }

    public string? TenantPhoneSnapshot { get; private set; }

    public DateOnly? OccupancyStartDate { get; private set; }

    public DateOnly? OccupancyEndDate { get; private set; }

    public string? BlockingReason { get; private set; }

    public decimal Total { get; private set; }

    public bool ElectricityWarning { get; private set; }

    public void SetBlockingReason(string? blockingReason) => BlockingReason = string.IsNullOrWhiteSpace(blockingReason) ? null : blockingReason.Trim();

    public void SetCalculation(decimal total, bool electricityWarning)
    {
        Total = total;
        ElectricityWarning = electricityWarning;
    }
}
