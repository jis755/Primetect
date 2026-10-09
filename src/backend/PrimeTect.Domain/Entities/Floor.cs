namespace PrimeTect.Domain.Entities;

public sealed class Floor
{
    private Floor()
    {
    }

    public Floor(Guid organizationId, Guid propertyId, string name, int sortOrder)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        PropertyId = propertyId;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Floor name is required.", nameof(name)) : name.Trim();
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid PropertyId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsArchived { get; private set; }

    public void Update(string name, int sortOrder)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Floor name is required.", nameof(name)) : name.Trim();
        SortOrder = sortOrder;
    }

    public void SetArchived(bool isArchived) => IsArchived = isArchived;
}
