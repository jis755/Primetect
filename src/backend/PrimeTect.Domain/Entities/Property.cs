using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class Property
{
    private Property()
    {
    }

    public Property(Guid organizationId, string name, string address, PropertyType propertyType, string? timeZoneId = null)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Property name is required.", nameof(name)) : name.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? throw new ArgumentException("Property address is required.", nameof(address)) : address.Trim();
        PropertyType = propertyType;
        TimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Dhaka" : timeZoneId.Trim();
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;

    public PropertyType PropertyType { get; private set; }

    public string TimeZoneId { get; private set; } = "Asia/Dhaka";

    public bool IsArchived { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void Update(string name, string address, PropertyType propertyType, string? timeZoneId = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Property name is required.", nameof(name)) : name.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? throw new ArgumentException("Property address is required.", nameof(address)) : address.Trim();
        PropertyType = propertyType;
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            TimeZoneId = timeZoneId.Trim();
        }
    }

    public void SetArchived(bool isArchived) => IsArchived = isArchived;
}
