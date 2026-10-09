using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class Unit
{
    private Unit()
    {
    }

    public Unit(Guid organizationId, Guid propertyId, Guid floorId, string name)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PropertyId = propertyId;
        FloorId = floorId;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Unit name is required.", nameof(name)) : name.Trim();
        OccupancyStatus = OccupancyStatus.Unknown;
        IsEnabled = true;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid PropertyId { get; private set; }

    public Guid FloorId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public bool IsArchived { get; private set; }

    public OccupancyStatus OccupancyStatus { get; private set; }

    public void Update(string name, bool isEnabled, bool isArchived)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Unit name is required.", nameof(name)) : name.Trim();
        IsEnabled = isEnabled;
        IsArchived = isArchived;
    }

    public void SetOccupancyStatus(OccupancyStatus status) => OccupancyStatus = status;
}
