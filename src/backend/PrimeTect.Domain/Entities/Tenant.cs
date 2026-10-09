using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class Tenant
{
    private Tenant()
    {
    }

    public Tenant(Guid organizationId, string? name, string? phone)
    {
        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Name = Normalize(name);
        Phone = Normalize(phone);
        OnboardingStatus = OnboardingStatus.NotConnected;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string? Name { get; private set; }

    public string? Phone { get; private set; }

    public OnboardingStatus OnboardingStatus { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public void UpdateDetails(string? name, string? phone)
    {
        Name = Normalize(name);
        Phone = Normalize(phone);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
