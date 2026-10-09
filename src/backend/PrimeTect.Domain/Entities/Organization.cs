namespace PrimeTect.Domain.Entities;

public sealed class Organization
{
    private Organization()
    {
    }

    public Organization(string name)
    {
        Id = Guid.NewGuid();
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Organization name is required.", nameof(name)) : name.Trim();
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
