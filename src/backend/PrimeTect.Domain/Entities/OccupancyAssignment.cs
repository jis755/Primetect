namespace PrimeTect.Domain.Entities;

public sealed class OccupancyAssignment
{
    private OccupancyAssignment()
    {
    }

    public OccupancyAssignment(Guid organizationId, Guid tenantId, Guid unitId, DateOnly startDate, DateOnly? endDate)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        TenantId = tenantId;
        UnitId = unitId;
        StartDate = startDate;
        EndDate = endDate;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UnitId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void Close(DateOnly endDate)
    {
        if (endDate < StartDate)
        {
            throw new ArgumentException("Occupancy end date cannot be before the start date.", nameof(endDate));
        }

        EndDate = endDate;
    }
}
