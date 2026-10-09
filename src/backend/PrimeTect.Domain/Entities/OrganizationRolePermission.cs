using PrimeTect.Domain.Enums;

namespace PrimeTect.Domain.Entities;

public sealed class OrganizationRolePermission
{
    private OrganizationRolePermission()
    {
    }

    public OrganizationRolePermission(Guid organizationId, UserRole role, OrganizationPermission permission, bool isEnabled)
    {
        OrganizationId = organizationId;
        Role = role;
        Permission = permission;
        IsEnabled = isEnabled;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid OrganizationId { get; private set; }

    public UserRole Role { get; private set; }

    public OrganizationPermission Permission { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

public static class OrganizationPermissionDefaults
{
    private static readonly OrganizationPermission[] All = Enum.GetValues<OrganizationPermission>();

    public static IReadOnlyDictionary<OrganizationPermission, bool> ForRole(UserRole role)
    {
        var permissions = All.ToDictionary(permission => permission, _ => false);
        switch (role)
        {
            case UserRole.Manager:
            case UserRole.BuildingStaff:
                Enable(permissions,
                    OrganizationPermission.ViewAssignedProperties,
                    OrganizationPermission.ViewMeterReadings,
                    OrganizationPermission.EditMeterReadings,
                    OrganizationPermission.SubmitMeterReadings,
                    OrganizationPermission.ViewApprovedBills,
                    OrganizationPermission.ReportCollections,
                    OrganizationPermission.RequestCollectionReversals);
                break;
            case UserRole.FinanceManager:
                Enable(permissions,
                    OrganizationPermission.ViewAssignedProperties,
                    OrganizationPermission.ViewApprovedBills,
                    OrganizationPermission.ReviewCollections,
                    OrganizationPermission.ReviewCollectionReversals);
                break;
        }

        return permissions;
    }

    public static IEnumerable<OrganizationRolePermission> CreateForOrganization(Guid organizationId)
    {
        foreach (var role in new[] { UserRole.Manager, UserRole.BuildingStaff, UserRole.FinanceManager })
        {
            foreach (var pair in ForRole(role))
            {
                yield return new OrganizationRolePermission(organizationId, role, pair.Key, pair.Value);
            }
        }
    }

    private static void Enable(IDictionary<OrganizationPermission, bool> permissions, params OrganizationPermission[] values)
    {
        foreach (var permission in values)
        {
            permissions[permission] = true;
        }
    }
}
