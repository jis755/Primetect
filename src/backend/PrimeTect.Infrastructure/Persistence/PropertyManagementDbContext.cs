using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PrimeTect.Domain.Entities;
using PrimeTect.Infrastructure.Identity;

namespace PrimeTect.Infrastructure.Persistence;

public sealed class PropertyManagementDbContext(DbContextOptions<PropertyManagementDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Property> Properties => Set<Property>();

    public DbSet<Floor> Floors => Set<Floor>();

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<StaffPropertyAssignment> StaffPropertyAssignments => Set<StaffPropertyAssignment>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<OccupancyAssignment> OccupancyAssignments => Set<OccupancyAssignment>();

    public DbSet<ChargeConfiguration> ChargeConfigurations => Set<ChargeConfiguration>();

    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    public DbSet<BillingCycle> BillingCycles => Set<BillingCycle>();

    public DbSet<BillingUnitBill> BillingUnitBills => Set<BillingUnitBill>();

    public DbSet<BillingChargeLine> BillingChargeLines => Set<BillingChargeLine>();

    public DbSet<BillingAuditEntry> BillingAuditEntries => Set<BillingAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(user => user.FullName).HasMaxLength(200).IsRequired();
            entity.Property(user => user.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(user => new { user.OrganizationId, user.Email });
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(organization => organization.Id);
            entity.Property(organization => organization.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Property>(entity =>
        {
            entity.ToTable("properties");
            entity.HasKey(property => property.Id);
            entity.Property(property => property.Name).HasMaxLength(200).IsRequired();
            entity.Property(property => property.Address).HasMaxLength(500).IsRequired();
            entity.Property(property => property.PropertyType).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.HasIndex(property => new { property.OrganizationId, property.Name });
            entity.HasOne<Organization>().WithMany().HasForeignKey(property => property.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Floor>(entity =>
        {
            entity.ToTable("floors");
            entity.HasKey(floor => floor.Id);
            entity.Property(floor => floor.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(floor => new { floor.PropertyId, floor.SortOrder });
            entity.HasOne<Property>().WithMany().HasForeignKey(floor => floor.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Unit>(entity =>
        {
            entity.ToTable("units");
            entity.HasKey(unit => unit.Id);
            entity.Property(unit => unit.Name).HasMaxLength(100).IsRequired();
            entity.Property(unit => unit.OccupancyStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(unit => new { unit.PropertyId, unit.Name });
            entity.HasOne<Property>().WithMany().HasForeignKey(unit => unit.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Floor>().WithMany().HasForeignKey(unit => unit.FloorId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StaffPropertyAssignment>(entity =>
        {
            entity.ToTable("staff_property_assignments");
            entity.HasKey(assignment => new { assignment.UserId, assignment.PropertyId });
            entity.HasOne<Property>().WithMany().HasForeignKey(assignment => assignment.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(assignment => assignment.OrganizationId);
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(tenant => tenant.Id);
            entity.Property(tenant => tenant.Name).HasMaxLength(200);
            entity.Property(tenant => tenant.Phone).HasMaxLength(50);
            entity.Property(tenant => tenant.OnboardingStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.HasIndex(tenant => tenant.OrganizationId);
        });

        modelBuilder.Entity<OccupancyAssignment>(entity =>
        {
            entity.ToTable("occupancy_assignments");
            entity.HasKey(assignment => assignment.Id);
            entity.HasIndex(assignment => new { assignment.UnitId, assignment.EndDate })
                .IsUnique()
                .HasFilter("\"EndDate\" IS NULL");
            entity.HasOne<Unit>().WithMany().HasForeignKey(assignment => assignment.UnitId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(assignment => assignment.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChargeConfiguration>(entity =>
        {
            entity.ToTable("charge_configurations");
            entity.HasKey(configuration => configuration.Id);
            entity.Property(configuration => configuration.ChargeType).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(configuration => configuration.Mode).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(configuration => configuration.Amount).HasPrecision(18, 4);
            entity.Property(configuration => configuration.CustomLabel).HasMaxLength(120);
            entity.HasIndex(configuration => new { configuration.OrganizationId, configuration.PropertyId, configuration.UnitId, configuration.ChargeType, configuration.BillingMonth });
            entity.HasOne<Property>().WithMany().HasForeignKey(configuration => configuration.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Unit>().WithMany().HasForeignKey(configuration => configuration.UnitId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserInvitation>(entity =>
        {
            entity.ToTable("user_invitations");
            entity.HasKey(invitation => invitation.Id);
            entity.Property(invitation => invitation.Email).HasMaxLength(320).IsRequired();
            entity.Property(invitation => invitation.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(invitation => invitation.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(invitation => invitation.TokenHash).IsUnique();
            entity.HasIndex(invitation => new { invitation.OrganizationId, invitation.Email });
        });

        modelBuilder.Entity<BillingCycle>(entity =>
        {
            entity.ToTable("billing_cycles");
            entity.HasKey(cycle => cycle.Id);
            entity.Property(cycle => cycle.BillingMonth).HasColumnType("date");
            entity.Property(cycle => cycle.PropertyNameSnapshot).HasMaxLength(200);
            entity.Property(cycle => cycle.PropertyAddressSnapshot).HasMaxLength(500);
            entity.Property(cycle => cycle.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(cycle => cycle.Version).IsConcurrencyToken();
            entity.Property(cycle => cycle.ApprovedTotal).HasPrecision(18, 2);
            entity.HasIndex(cycle => new { cycle.OrganizationId, cycle.PropertyId, cycle.BillingMonth }).IsUnique();
            entity.HasOne<Property>().WithMany().HasForeignKey(cycle => cycle.PropertyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BillingUnitBill>(entity =>
        {
            entity.ToTable("billing_unit_bills");
            entity.HasKey(bill => bill.Id);
            entity.Property(bill => bill.UnitNameSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(bill => bill.FloorNameSnapshot).HasMaxLength(100);
            entity.Property(bill => bill.TenantNameSnapshot).HasMaxLength(200);
            entity.Property(bill => bill.TenantPhoneSnapshot).HasMaxLength(50);
            entity.Property(bill => bill.BlockingReason).HasMaxLength(300);
            entity.Property(bill => bill.Total).HasPrecision(18, 2);
            entity.HasIndex(bill => new { bill.BillingCycleId, bill.UnitId }).IsUnique();
            entity.HasOne<BillingCycle>().WithMany().HasForeignKey(bill => bill.BillingCycleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Unit>().WithMany().HasForeignKey(bill => bill.UnitId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OccupancyAssignment>().WithMany().HasForeignKey(bill => bill.OccupancyAssignmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(bill => bill.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BillingChargeLine>(entity =>
        {
            entity.ToTable("billing_charge_lines");
            entity.HasKey(line => line.Id);
            entity.Property(line => line.ChargeType).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(line => line.ChargeNameSnapshot).HasMaxLength(120).IsRequired();
            entity.Property(line => line.SettingModeSnapshot).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(line => line.PreviousReadingSource).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(line => line.RateSnapshot).HasPrecision(18, 4);
            entity.Property(line => line.PreviousReading).HasPrecision(18, 4);
            entity.Property(line => line.CurrentReading).HasPrecision(18, 4);
            entity.Property(line => line.ConsumptionUnits).HasPrecision(18, 4);
            entity.Property(line => line.Amount).HasPrecision(18, 2);
            entity.HasIndex(line => new { line.BillingUnitBillId, line.ChargeType }).IsUnique();
            entity.HasOne<BillingUnitBill>().WithMany().HasForeignKey(line => line.BillingUnitBillId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BillingAuditEntry>(entity =>
        {
            entity.ToTable("billing_audit_entries");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(audit => audit.Reason).HasMaxLength(500);
            entity.Property(audit => audit.BeforeJson).HasColumnType("text");
            entity.Property(audit => audit.AfterJson).HasColumnType("text");
            entity.HasIndex(audit => new { audit.OrganizationId, audit.BillingCycleId, audit.OccurredAtUtc });
            entity.HasOne<BillingCycle>().WithMany().HasForeignKey(audit => audit.BillingCycleId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
