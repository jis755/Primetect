namespace PrimeTect.Domain.Entities;

public sealed class StaffPropertyAssignment
{
    private StaffPropertyAssignment()
    {
    }

    public StaffPropertyAssignment(Guid organizationId, Guid userId, Guid propertyId)
    {
        OrganizationId = organizationId;
        UserId = userId;
        PropertyId = propertyId;
        AssignedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid PropertyId { get; private set; }

    public DateTimeOffset AssignedAtUtc { get; private set; }
}
