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

    public DbSet<CollectionReport> CollectionReports => Set<CollectionReport>();

    public DbSet<CollectionReversal> CollectionReversals => Set<CollectionReversal>();

    public DbSet<CollectionAuditEntry> CollectionAuditEntries => Set<CollectionAuditEntry>();

    public DbSet<RoomMeterIssue> RoomMeterIssues => Set<RoomMeterIssue>();

    public DbSet<RoomMeterIssueAuditEntry> RoomMeterIssueAuditEntries => Set<RoomMeterIssueAuditEntry>();

    public DbSet<MeterQrLink> MeterQrLinks => Set<MeterQrLink>();

    public DbSet<MeterPhoto> MeterPhotos => Set<MeterPhoto>();

    public DbSet<UtilityAccount> UtilityAccounts => Set<UtilityAccount>();

    public DbSet<UtilityMainMeter> UtilityMainMeters => Set<UtilityMainMeter>();

    public DbSet<MeterInstallation> MeterInstallations => Set<MeterInstallation>();

    public DbSet<BillingMeterSegment> BillingMeterSegments => Set<BillingMeterSegment>();

    public DbSet<MeterInstallationAuditEntry> MeterInstallationAuditEntries => Set<MeterInstallationAuditEntry>();

    public DbSet<StaffCredentialAuditEntry> StaffCredentialAuditEntries => Set<StaffCredentialAuditEntry>();

    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    public DbSet<OrganizationRolePermission> OrganizationRolePermissions => Set<OrganizationRolePermission>();

    public DbSet<OrganizationAuditEntry> OrganizationAuditEntries => Set<OrganizationAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(user => user.FullName).HasMaxLength(200).IsRequired();
            entity.Property(user => user.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(user => new { user.OrganizationId, user.Email });
            // Login-phone uniqueness is a PostgreSQL boundary constraint. It is added only for Npgsql because
            // the InMemory provider ignores filtered indexes and would reject multiple null phone numbers.
            if (Database.IsNpgsql())
            {
                entity.HasIndex(user => user.PhoneNumber).IsUnique().HasFilter("\"PhoneNumber\" IS NOT NULL AND \"PhoneNumber\" <> ''");
            }
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(organization => organization.Id);
            entity.Property(organization => organization.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<OrganizationRolePermission>(entity =>
        {
            entity.ToTable("organization_role_permissions");
            entity.HasKey(permission => new { permission.OrganizationId, permission.Role, permission.Permission });
            entity.Property(permission => permission.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(permission => permission.Permission).HasConversion<string>().HasMaxLength(80).IsRequired();
            entity.Property(permission => permission.UpdatedAtUtc).IsRequired();
            entity.HasIndex(permission => new { permission.OrganizationId, permission.Role });
            entity.HasOne<Organization>().WithMany().HasForeignKey(permission => permission.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrganizationAuditEntry>(entity =>
        {
            entity.ToTable("organization_audit_entries");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Action).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.Reason).HasMaxLength(500);
            entity.Property(audit => audit.BeforeJson).HasColumnType("text");
            entity.Property(audit => audit.AfterJson).HasColumnType("text");
            entity.HasIndex(audit => new { audit.OrganizationId, audit.OccurredAtUtc });
            entity.HasOne<Organization>().WithMany().HasForeignKey(audit => audit.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Property>(entity =>
        {
            entity.ToTable("properties");
            entity.HasKey(property => property.Id);
            entity.Property(property => property.Name).HasMaxLength(200).IsRequired();
            entity.Property(property => property.Address).HasMaxLength(500).IsRequired();
            entity.Property(property => property.PropertyType).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(property => property.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.Property(property => property.Latitude).HasPrecision(9, 6);
            entity.Property(property => property.Longitude).HasPrecision(9, 6);
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
            entity.Property(configuration => configuration.WarningMode).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(configuration => configuration.WarningThreshold).HasPrecision(18, 2);
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
            entity.Property(line => line.PreviousReadingDate).HasColumnType("date");
            entity.Property(line => line.CurrentReading).HasPrecision(18, 4);
            entity.Property(line => line.CurrentReadingDate).HasColumnType("date");
            entity.Property(line => line.ConsumptionUnits).HasPrecision(18, 4);
            entity.Property(line => line.Amount).HasPrecision(18, 2);
            entity.Property(line => line.WarningThreshold).HasPrecision(18, 2);
            entity.Property(line => line.MeterInstallationId);
            entity.HasOne<MeterPhoto>().WithMany().HasForeignKey(line => line.MeterPhotoId).OnDelete(DeleteBehavior.SetNull);
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

        modelBuilder.Entity<CollectionReport>(entity =>
        {
            entity.ToTable("collection_reports");
            entity.HasKey(report => report.Id);
            entity.Property(report => report.UnitNameSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(report => report.FloorNameSnapshot).HasMaxLength(100);
            entity.Property(report => report.TenantNameSnapshot).HasMaxLength(200);
            entity.Property(report => report.TenantPhoneSnapshot).HasMaxLength(50);
            entity.Property(report => report.BillingMonth).HasColumnType("date");
            entity.Property(report => report.BilledAmountSnapshot).HasPrecision(18, 2);
            entity.Property(report => report.AmountReceived).HasPrecision(18, 2);
            entity.Property(report => report.ReceivedDate).HasColumnType("date");
            entity.Property(report => report.PaymentMethod).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(report => report.ReferenceNote).HasMaxLength(500);
            entity.Property(report => report.SubmissionKey).IsRequired();
            entity.Property(report => report.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(report => report.Version).IsConcurrencyToken();
            entity.HasIndex(report => new { report.OrganizationId, report.SubmissionKey }).IsUnique();
            entity.HasIndex(report => new { report.OrganizationId, report.BillingUnitBillId, report.Status });
            entity.HasOne<BillingUnitBill>().WithMany().HasForeignKey(report => report.BillingUnitBillId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CollectionReversal>(entity =>
        {
            entity.ToTable("collection_reversals");
            entity.HasKey(reversal => reversal.Id);
            entity.Property(reversal => reversal.Reason).HasMaxLength(500).IsRequired();
            entity.Property(reversal => reversal.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(reversal => reversal.Version).IsConcurrencyToken();
            entity.HasIndex(reversal => new { reversal.OrganizationId, reversal.CollectionReportId, reversal.Status });
            entity.HasOne<CollectionReport>().WithMany().HasForeignKey(reversal => reversal.CollectionReportId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CollectionAuditEntry>(entity =>
        {
            entity.ToTable("collection_audit_entries");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Action).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(audit => audit.Reason).HasMaxLength(500);
            entity.Property(audit => audit.BeforeJson).HasColumnType("text");
            entity.Property(audit => audit.AfterJson).HasColumnType("text");
            entity.HasIndex(audit => new { audit.OrganizationId, audit.CollectionReportId, audit.OccurredAtUtc });
            entity.HasOne<CollectionReport>().WithMany().HasForeignKey(audit => audit.CollectionReportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<CollectionReversal>().WithMany().HasForeignKey(audit => audit.CollectionReversalId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RoomMeterIssue>(entity =>
        {
            entity.ToTable("room_meter_issues");
            entity.HasKey(issue => issue.Id);
            entity.Property(issue => issue.IssueType).HasConversion<string>().HasMaxLength(40);
            entity.Property(issue => issue.Description).HasMaxLength(2000).IsRequired();
            entity.Property(issue => issue.ObservedDate).HasColumnType("date");
            entity.Property(issue => issue.Target).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(issue => issue.SubmissionKey);
            entity.Property(issue => issue.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(issue => issue.Version).IsConcurrencyToken();
            entity.Property(issue => issue.ResolutionReason).HasMaxLength(500);
            entity.HasIndex(issue => new { issue.OrganizationId, issue.PropertyId, issue.UnitId, issue.Status });
            entity.HasIndex(issue => new { issue.OrganizationId, issue.ReporterUserId, issue.SubmissionKey }).IsUnique();
            entity.HasIndex(issue => new { issue.OrganizationId, issue.CreatedAtUtc });
            entity.HasOne<Property>().WithMany().HasForeignKey(issue => issue.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Unit>().WithMany().HasForeignKey(issue => issue.UnitId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoomMeterIssueAuditEntry>(entity =>
        {
            entity.ToTable("room_meter_issue_audit_entries");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Action).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(audit => audit.Reason).HasMaxLength(500);
            entity.Property(audit => audit.BeforeJson).HasColumnType("text");
            entity.Property(audit => audit.AfterJson).HasColumnType("text");
            entity.HasIndex(audit => new { audit.OrganizationId, audit.RoomMeterIssueId, audit.OccurredAtUtc });
            entity.HasOne<RoomMeterIssue>().WithMany().HasForeignKey(audit => audit.RoomMeterIssueId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.ToTable("user_notifications");
            entity.HasKey(notification => notification.Id);
            entity.Property(notification => notification.CreatedAtUtc).IsRequired();
            entity.HasIndex(notification => new { notification.OrganizationId, notification.RecipientUserId, notification.CreatedAtUtc });
            entity.HasIndex(notification => new { notification.OrganizationId, notification.RecipientUserId, notification.RoomMeterIssueId }).IsUnique();
            entity.HasOne<Property>().WithMany().HasForeignKey(notification => notification.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<RoomMeterIssue>().WithMany().HasForeignKey(notification => notification.RoomMeterIssueId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeterQrLink>(entity =>
        {
            entity.ToTable("meter_qr_links");
            entity.HasKey(link => link.Id);
            entity.Property(link => link.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(link => link.Placement).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.HasIndex(link => link.TokenHash).IsUnique();
            entity.HasIndex(link => new { link.OrganizationId, link.PropertyId, link.UnitId, link.RevokedAtUtc });
            entity.HasOne<Property>().WithMany().HasForeignKey(link => link.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Unit>().WithMany().HasForeignKey(link => link.UnitId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeterPhoto>(entity =>
        {
            entity.ToTable("meter_photos");
            entity.HasKey(photo => photo.Id);
            entity.Property(photo => photo.StorageKey).HasMaxLength(120).IsRequired();
            entity.Property(photo => photo.ContentType).HasMaxLength(80).IsRequired();
            entity.Property(photo => photo.FileExtension).HasMaxLength(10).IsRequired();
            entity.Property(photo => photo.OriginalFileName).HasMaxLength(120);
            entity.Property(photo => photo.OcrOutcome).HasMaxLength(40).IsRequired();
            entity.Property(photo => photo.OcrProviderVersion).HasMaxLength(100);
            entity.Property(photo => photo.OcrSuggestion).HasPrecision(18, 4);
            entity.Property(photo => photo.OcrConfidence).HasPrecision(5, 4);
            entity.Property(photo => photo.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(photo => photo.ConfirmedReading).HasPrecision(18, 4);
            entity.Property(photo => photo.MeterInstallationId);
            entity.HasIndex(photo => new { photo.OrganizationId, photo.BillingCycleId, photo.UnitId, photo.Status });
            entity.HasIndex(photo => new { photo.OrganizationId, photo.StorageKey }).IsUnique();
            entity.HasOne<Property>().WithMany().HasForeignKey(photo => photo.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<BillingCycle>().WithMany().HasForeignKey(photo => photo.BillingCycleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<BillingUnitBill>().WithMany().HasForeignKey(photo => photo.BillingUnitBillId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Unit>().WithMany().HasForeignKey(photo => photo.UnitId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UtilityAccount>(entity =>
        {
            entity.ToTable("utility_accounts");
            entity.HasKey(account => account.Id);
            entity.Property(account => account.ProviderName).HasMaxLength(200).IsRequired();
            entity.Property(account => account.CustomerNumber).HasMaxLength(80).IsRequired();
            entity.Property(account => account.DisplayLabel).HasMaxLength(120);
            entity.HasIndex(account => new { account.OrganizationId, account.PropertyId });
            entity.HasOne<Property>().WithMany().HasForeignKey(account => account.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UtilityMainMeter>(entity =>
        {
            entity.ToTable("utility_main_meters");
            entity.HasKey(meter => meter.Id);
            entity.Property(meter => meter.MeterNumber).HasMaxLength(80).IsRequired();
            entity.Property(meter => meter.DisplayLabel).HasMaxLength(120);
            entity.HasIndex(meter => new { meter.OrganizationId, meter.PropertyId });
            entity.HasIndex(meter => new { meter.OrganizationId, meter.UtilityAccountId });
            entity.HasOne<UtilityAccount>().WithMany().HasForeignKey(meter => meter.UtilityAccountId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Property>().WithMany().HasForeignKey(meter => meter.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeterInstallation>(entity =>
        {
            entity.ToTable("meter_installations");
            entity.HasKey(installation => installation.Id);
            entity.Property(installation => installation.SerialNumber).HasMaxLength(120);
            entity.Property(installation => installation.DisplayLabel).HasMaxLength(120);
            entity.Property(installation => installation.InstallationDate).HasColumnType("date");
            entity.Property(installation => installation.OpeningReading).HasPrecision(18, 4);
            entity.Property(installation => installation.OpeningReadingDate).HasColumnType("date");
            entity.Property(installation => installation.EndedOn).HasColumnType("date");
            entity.Property(installation => installation.EndReason).HasMaxLength(300);
            entity.Property(installation => installation.FinalReading).HasPrecision(18, 4);
            entity.Property(installation => installation.Version).IsConcurrencyToken();
            entity.HasIndex(installation => new { installation.OrganizationId, installation.PropertyId, installation.UnitId });
            entity.HasIndex(installation => installation.UnitId)
                .IsUnique()
                .HasFilter("\"EndedOn\" IS NULL")
                .HasDatabaseName("IX_meter_installations_active_unit");
            entity.HasOne<Property>().WithMany().HasForeignKey(installation => installation.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Unit>().WithMany().HasForeignKey(installation => installation.UnitId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<UtilityMainMeter>().WithMany().HasForeignKey(installation => installation.MainMeterId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BillingMeterSegment>(entity =>
        {
            entity.ToTable("billing_meter_segments");
            entity.HasKey(segment => segment.Id);
            entity.Property(segment => segment.PreviousReadingSource).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(segment => segment.PreviousReading).HasPrecision(18, 4);
            entity.Property(segment => segment.PreviousReadingDate).HasColumnType("date");
            entity.Property(segment => segment.CurrentReading).HasPrecision(18, 4);
            entity.Property(segment => segment.CurrentReadingDate).HasColumnType("date");
            entity.Property(segment => segment.ConsumptionUnits).HasPrecision(18, 4);
            entity.Property(segment => segment.SegmentStartDate).HasColumnType("date");
            entity.Property(segment => segment.SegmentEndDate).HasColumnType("date");
            entity.HasIndex(segment => new { segment.OrganizationId, segment.BillingChargeLineId });
            entity.HasOne<BillingChargeLine>().WithMany().HasForeignKey(segment => segment.BillingChargeLineId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeterInstallationAuditEntry>(entity =>
        {
            entity.ToTable("meter_installation_audit_entries");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(audit => audit.ReplacementDate).HasColumnType("date");
            entity.Property(audit => audit.Reason).HasMaxLength(500);
            entity.Property(audit => audit.BeforeJson).HasColumnType("text");
            entity.Property(audit => audit.AfterJson).HasColumnType("text");
            entity.HasIndex(audit => new { audit.OrganizationId, audit.InstallationId, audit.OccurredAtUtc });
            entity.HasIndex(audit => new { audit.OrganizationId, audit.SubmissionKey }).IsUnique();
            entity.HasOne<Property>().WithMany().HasForeignKey(audit => audit.PropertyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Unit>().WithMany().HasForeignKey(audit => audit.UnitId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StaffCredentialAuditEntry>(entity =>
        {
            entity.ToTable("staff_credential_audit_entries");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Action).HasMaxLength(40).IsRequired();
            entity.Property(audit => audit.Reason).HasMaxLength(500);
            entity.Property(audit => audit.BeforeJson).HasColumnType("text");
            entity.Property(audit => audit.AfterJson).HasColumnType("text");
            entity.HasIndex(audit => new { audit.OrganizationId, audit.TargetUserId, audit.OccurredAtUtc });
            entity.HasIndex(audit => new { audit.OrganizationId, audit.SubmissionKey }).IsUnique();
            entity.HasOne<Organization>().WithMany().HasForeignKey(audit => audit.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
