using Microsoft.EntityFrameworkCore;
using PrimeTect.Domain.Entities;
using PrimeTect.Domain.Enums;
using PrimeTect.Infrastructure.Persistence;

namespace PrimeTect.Api.Security;

public static class OrganizationPermissionAuthorization
{
    public static async Task<bool> HasAsync(
        PropertyManagementDbContext db,
        RequestUser actor,
        OrganizationPermission permission,
        CancellationToken cancellationToken)
    {
        if (actor.IsAdmin)
        {
            return true;
        }

        var permissions = await GetAsync(db, actor, cancellationToken);
        return permissions.GetValueOrDefault(permission);
    }

    public static async Task<IReadOnlyDictionary<OrganizationPermission, bool>> GetAsync(
        PropertyManagementDbContext db,
        RequestUser actor,
        CancellationToken cancellationToken)
    {
        if (actor.IsAdmin)
        {
            return Enum.GetValues<OrganizationPermission>().ToDictionary(permission => permission, _ => true);
        }

        var defaults = OrganizationPermissionDefaults.ForRole(actor.Role).ToDictionary(pair => pair.Key, pair => pair.Value);
        var rows = await db.OrganizationRolePermissions.AsNoTracking()
            .Where(item => item.OrganizationId == actor.OrganizationId && item.Role == actor.Role)
            .ToListAsync(cancellationToken);

        if (rows.Count < Enum.GetValues<OrganizationPermission>().Length)
        {
            var existing = rows.Select(item => item.Permission).ToHashSet();
            var missing = OrganizationPermissionDefaults.ForRole(actor.Role)
                .Where(pair => !existing.Contains(pair.Key))
                .Select(pair => new OrganizationRolePermission(actor.OrganizationId, actor.Role, pair.Key, pair.Value))
                .ToList();
            if (missing.Count > 0)
            {
                db.OrganizationRolePermissions.AddRange(missing);
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                    rows.AddRange(missing);
                }
                catch (DbUpdateException)
                {
                    foreach (var item in missing)
                    {
                        db.Entry(item).State = EntityState.Detached;
                    }
                }
            }
        }

        foreach (var row in rows)
        {
            defaults[row.Permission] = row.IsEnabled;
        }

        return defaults;
    }

    public static async Task EnsureDefaultsAsync(PropertyManagementDbContext db, Guid organizationId, CancellationToken cancellationToken)
    {
        var existing = await db.OrganizationRolePermissions
            .Where(item => item.OrganizationId == organizationId)
            .Select(item => new { item.Role, item.Permission })
            .ToListAsync(cancellationToken);
        var existingKeys = existing.Select(item => (item.Role, item.Permission)).ToHashSet();
        var missing = OrganizationPermissionDefaults.CreateForOrganization(organizationId)
            .Where(item => !existingKeys.Contains((item.Role, item.Permission)))
            .ToList();
        if (missing.Count > 0)
        {
            db.OrganizationRolePermissions.AddRange(missing);
        }
    }
}
