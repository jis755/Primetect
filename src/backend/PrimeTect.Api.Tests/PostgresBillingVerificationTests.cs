using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PrimeTect.Domain.Enums;
using PrimeTect.Infrastructure.Persistence;
using Xunit;

namespace PrimeTect.Api.Tests;

/// <summary>
/// Creates and destroys one uniquely named PostgreSQL database per test run.
/// The fixture intentionally refuses ordinary application connection strings.
/// </summary>
public sealed class PostgresVerificationDatabase : IAsyncLifetime
{
    private const string AdminConnectionVariable = "PRIMETECT_TEST_POSTGRES_ADMIN_CONNECTION";
    private readonly string databaseName = $"primetect_verification_{Guid.NewGuid():N}";
    private readonly HashSet<string> createdDatabaseNames = [];
    private string? adminConnectionString;

    public string? Blocker { get; private set; }

    public bool IsAvailable => Blocker is null;

    public string ConnectionString
        => IsAvailable
            ? BuildConnectionString(databaseName)
            : throw new InvalidOperationException(Blocker);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(AdminConnectionVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            Blocker = $"{AdminConnectionVariable} is not set; PostgreSQL integration tests were not run.";
            return;
        }

        try
        {
            var adminBuilder = new NpgsqlConnectionStringBuilder(configured)
            {
                Pooling = false,
            };

            if (!IsSafeLocalAdminTarget(adminBuilder))
            {
                Blocker = $"{AdminConnectionVariable} must target a local PostgreSQL server and database 'postgres' or 'template1'; no database was created.";
                return;
            }

            adminConnectionString = adminBuilder.ConnectionString;
            await using var admin = new NpgsqlConnection(adminConnectionString);
            await admin.OpenAsync();

            await using (var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", admin))
            {
                exists.Parameters.AddWithValue("name", databaseName);
                if (await exists.ExecuteScalarAsync() is not null)
                {
                    Blocker = "The generated verification database name already exists; no database was modified.";
                    return;
                }
            }

            await using (var create = new NpgsqlCommand($"CREATE DATABASE {QuoteIdentifier(databaseName)}", admin))
            {
                await create.ExecuteNonQueryAsync();
            }

            createdDatabaseNames.Add(databaseName);
            await using var context = new PropertyManagementDbContext(
                new DbContextOptionsBuilder<PropertyManagementDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options);
            await context.Database.MigrateAsync();

            var applied = await context.Database.GetAppliedMigrationsAsync();
            var pending = await context.Database.GetPendingMigrationsAsync();
            if (applied.Count() != 11 || pending.Any())
            {
                throw new InvalidOperationException($"Expected eleven applied migrations and no pending migrations, found {applied.Count()} applied and {pending.Count()} pending.");
            }
        }
        catch (Exception exception)
        {
            await DropCreatedDatabaseAsync();
            Blocker = $"PostgreSQL integration tests could not create and migrate the disposable database: {exception.GetType().Name}: {exception.Message}";
        }
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException(Blocker);
        }

        EnsureGeneratedDatabaseTarget(ConnectionString, databaseName);
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<string> CreateAdditionalDatabaseAsync()
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(adminConnectionString))
        {
            throw new InvalidOperationException(Blocker ?? "The PostgreSQL verification database is unavailable.");
        }

        var additionalName = $"primetect_verification_{Guid.NewGuid():N}";
        var connectionString = BuildConnectionString(additionalName);
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();

        await using (var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", admin))
        {
            exists.Parameters.AddWithValue("name", additionalName);
            Assert.Null(await exists.ExecuteScalarAsync());
        }

        await using (var create = new NpgsqlCommand($"CREATE DATABASE {QuoteIdentifier(additionalName)}", admin))
        {
            await create.ExecuteNonQueryAsync();
        }

        createdDatabaseNames.Add(additionalName);
        return connectionString;
    }

    public Task DisposeAsync() => DropCreatedDatabaseAsync();

    private string BuildConnectionString(string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = database,
            Pooling = false,
        };
        EnsureGeneratedDatabaseTarget(builder.ConnectionString, database);
        return builder.ConnectionString;
    }

    private void EnsureGeneratedDatabaseTarget(string connectionString, string expectedDatabaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, expectedDatabaseName, StringComparison.Ordinal) || !IsSafeGeneratedName(builder.Database ?? string.Empty))
        {
            throw new InvalidOperationException("The effective PostgreSQL verification connection must target only the generated disposable database.");
        }
    }

    private async Task DropCreatedDatabaseAsync()
    {
        if (createdDatabaseNames.Count == 0 || string.IsNullOrWhiteSpace(adminConnectionString))
        {
            return;
        }

        foreach (var name in createdDatabaseNames.ToArray())
        {
            if (!IsSafeGeneratedName(name))
            {
                continue;
            }

            try
            {
                await using var admin = new NpgsqlConnection(adminConnectionString);
                await admin.OpenAsync();
                await using var drop = new NpgsqlCommand($"DROP DATABASE {QuoteIdentifier(name)} WITH (FORCE)", admin);
                await drop.ExecuteNonQueryAsync();
                createdDatabaseNames.Remove(name);
            }
            catch
            {
                // Do not attempt a broader cleanup target. The blocker is reported by the test run.
            }
        }

    }

    private static bool IsSafeLocalAdminTarget(NpgsqlConnectionStringBuilder builder)
    {
        var database = builder.Database?.Trim();
        var host = builder.Host?.Trim().ToLowerInvariant();
        return (string.Equals(database, "postgres", StringComparison.OrdinalIgnoreCase)
                || string.Equals(database, "template1", StringComparison.OrdinalIgnoreCase))
            && (host is "localhost" or "127.0.0.1" or "::1")
            && !string.IsNullOrWhiteSpace(builder.Username);
    }

    private static bool IsSafeGeneratedName(string name)
        => name.StartsWith("primetect_verification_", StringComparison.Ordinal)
           && name.Length == "primetect_verification_".Length + 32
           && name["primetect_verification_".Length..].All(Uri.IsHexDigit);

    private static string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}

public sealed class PostgresPrimeTectApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public bool FailNextSave { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "PostgreSQL");
        builder.UseSetting("ConnectionStrings:PrimeTectDatabase", connectionString);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "PostgreSQL",
            ["ConnectionStrings:PrimeTectDatabase"] = connectionString,
            ["AllowedOrigins:0"] = "http://localhost:3000",
        }));
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddSingleton<ISaveChangesInterceptor>(new FailNextSaveChangesInterceptor(() =>
            {
                if (!FailNextSave)
                {
                    return false;
                }

                FailNextSave = false;
                return true;
            }));
        });
    }

    private sealed class FailNextSaveChangesInterceptor(Func<bool> shouldFail) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (shouldFail())
            {
                throw new InvalidOperationException("Intentional test persistence failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PRIMETECT_TEST_POSTGRES_ADMIN_CONNECTION")))
        {
            Skip = "PRIMETECT_TEST_POSTGRES_ADMIN_CONNECTION is not set; PostgreSQL integration tests were not run.";
        }
    }
}

public sealed class PostgresBillingVerificationTests : IClassFixture<PostgresVerificationDatabase>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgresVerificationDatabase database;

    public PostgresBillingVerificationTests(PostgresVerificationDatabase database)
    {
        this.database = database;
    }

    [PostgresFact]
    public async Task Migrations_precision_and_database_constraints_are_verified()
    {
        RequirePostgres();

        await using var connection = await database.OpenConnectionAsync();
        Assert.Equal(11L, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM \"__EFMigrationsHistory\""));

        Assert.Equal((18, 4), await NumericDefinitionAsync(connection, "charge_configurations", "Amount"));
        Assert.Equal((18, 4), await NumericDefinitionAsync(connection, "billing_charge_lines", "RateSnapshot"));
        Assert.Equal((18, 4), await NumericDefinitionAsync(connection, "billing_charge_lines", "ConsumptionUnits"));
        Assert.Equal((18, 2), await NumericDefinitionAsync(connection, "billing_charge_lines", "Amount"));
        Assert.Equal(1L, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM pg_indexes WHERE tablename = 'billing_cycles' AND indexname = 'IX_billing_cycles_OrganizationId_PropertyId_BillingMonth' AND indexdef LIKE '%UNIQUE%'"));
    }

    [PostgresFact]
    public async Task PostgreSQL_feedback_http_workflow_preserves_targets_notifications_and_authorization()
    {
        RequirePostgres();

        using var api = new PostgresPrimeTectApiFactory(database.ConnectionString);
        using var admin = api.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await RegisterAsync(admin, $"feedback-pg-admin-{suffix}@example.test", "Feedback PostgreSQL Admin");

        var property = await ReadAsync<PropertyResponse>(await admin.PostAsJsonAsync("/api/properties", new
        {
            name = $"Feedback PostgreSQL Property {suffix}",
            address = "12 Verification Street",
            propertyType = "ApartmentBuilding",
            timeZoneId = "Asia/Dhaka",
            floors = new[]
            {
                new { name = "Ground", unitCount = 1, unitNames = new[] { "2A" } },
                new { name = "Upper", unitCount = 1, unitNames = new[] { "6B" } },
            },
        }));
        var unit = property.Floors.SelectMany(floor => floor.Units).Single(item => item.Name == "6B");

        using var staff = api.CreateClient();
        var staffInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"feedback-pg-staff-{suffix}@example.test", role = "BuildingStaff" }));
        var staffUser = await AcceptInvitationAsync(staff, staffInvitation.InvitationToken, "Feedback PostgreSQL Staff");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/properties/{property.Id}/staff/{staffUser.Id}", null)).StatusCode);

        using var manager = api.CreateClient();
        var managerInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"feedback-pg-manager-{suffix}@example.test", role = "Manager" }));
        var managerUser = await AcceptInvitationAsync(manager, managerInvitation.InvitationToken, "Feedback PostgreSQL Reviewer");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/properties/{property.Id}/staff/{managerUser.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/role-permissions", new
        {
            role = "Manager",
            permissions = new Dictionary<string, bool> { ["ReviewRoomMeterIssues"] = true },
            reason = "Disposable feedback endpoint verification",
        })).StatusCode);

        using var finance = api.CreateClient();
        var financeInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"feedback-pg-finance-{suffix}@example.test", role = "FinanceManager" }));
        var financeUser = await AcceptInvitationAsync(finance, financeInvitation.InvitationToken, "Feedback PostgreSQL Finance");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/properties/{property.Id}/staff/{financeUser.Id}", null)).StatusCode);

        using var unassignedReviewer = api.CreateClient();
        var unassignedInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"feedback-pg-unassigned-{suffix}@example.test", role = "Manager" }));
        await AcceptInvitationAsync(unassignedReviewer, unassignedInvitation.InvitationToken, "Unassigned Feedback Reviewer");

        var beforeData = await ReadPropertyDataCountsAsync(property.Id);
        var before = DateTimeOffset.UtcNow;
        var submissionKey = Guid.NewGuid();
        var unitReportResponse = await staff.PostAsJsonAsync($"/api/properties/{property.Id}/feedback-reports", new
        {
            unitId = unit.Id,
            note = "The 6B corridor light needs attention.",
            submissionKey,
        });
        Assert.Equal(HttpStatusCode.Created, unitReportResponse.StatusCode);
        var unitReport = await ReadAsync<FeedbackResponse>(unitReportResponse);
        var after = DateTimeOffset.UtcNow;
        Assert.Equal(property.Id, unitReport.PropertyId);
        Assert.Equal(unit.Id, unitReport.UnitId);
        Assert.Equal("6B", unitReport.UnitName);
        Assert.Equal(RoomMeterIssueTarget.Unit, unitReport.Target);
        Assert.Null(unitReport.IssueType);
        Assert.Null(unitReport.ObservedDate);
        Assert.InRange(unitReport.CreatedAtUtc, before, after);
        Assert.Equal(DateOnly.FromDateTime(unitReport.CreatedAtUtc.ToOffset(TimeSpan.FromHours(6)).DateTime), DateOnly.FromDateTime(DateTime.UtcNow.AddHours(6)));

        var duplicate = await ReadAsync<FeedbackResponse>(await staff.PostAsJsonAsync($"/api/properties/{property.Id}/feedback-reports", new
        {
            unitId = unit.Id,
            note = "The 6B corridor light needs attention.",
            submissionKey,
        }));
        Assert.Equal(unitReport.Id, duplicate.Id);

        var commonArea = await ReadAsync<FeedbackResponse>(await staff.PostAsJsonAsync($"/api/properties/{property.Id}/feedback-reports", new
        {
            unitId = (Guid?)null,
            note = "The shared entrance needs cleaning.",
            submissionKey = Guid.NewGuid(),
        }));
        Assert.Null(commonArea.UnitId);
        Assert.Equal("Building/Common Area", commonArea.UnitName);
        Assert.Equal(RoomMeterIssueTarget.BuildingCommonArea, commonArea.Target);
        Assert.Null(commonArea.IssueType);
        Assert.Null(commonArea.ObservedDate);

        var afterData = await ReadPropertyDataCountsAsync(property.Id);
        Assert.Equal(beforeData, afterData);

        var adminNotifications = await ReadAsync<List<NotificationResponse>>(await admin.GetAsync("/api/notifications"));
        var managerNotifications = await ReadAsync<List<NotificationResponse>>(await manager.GetAsync("/api/notifications"));
        Assert.Equal(2, adminNotifications.Count);
        Assert.Equal(2, managerNotifications.Count);
        var adminUnitNotification = Assert.Single(adminNotifications, item => item.RoomMeterIssueId == unitReport.Id);
        var managerUnitNotification = Assert.Single(managerNotifications, item => item.RoomMeterIssueId == unitReport.Id);
        Assert.Equal("New report · Room 6B", adminUnitNotification.Title);
        Assert.False(adminUnitNotification.IsRead);
        Assert.False(managerUnitNotification.IsRead);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/notifications/{adminUnitNotification.Id}/read", null)).StatusCode);
        var adminRead = Assert.Single((await ReadAsync<List<NotificationResponse>>(await admin.GetAsync("/api/notifications"))), item => item.Id == adminUnitNotification.Id);
        var managerUnread = Assert.Single((await ReadAsync<List<NotificationResponse>>(await manager.GetAsync("/api/notifications"))), item => item.Id == managerUnitNotification.Id);
        Assert.True(adminRead.IsRead);
        Assert.False(managerUnread.IsRead);

        var staffReports = await ReadAsync<List<FeedbackResponse>>(await staff.GetAsync($"/api/properties/{property.Id}/room-meter-issues"));
        Assert.Equal(2, staffReports.Count);
        Assert.All(staffReports, item => Assert.Equal(RoomMeterIssueStatus.Open, item.Status));
        var acknowledged = await ReadAsync<FeedbackResponse>(await manager.PostAsJsonAsync($"/api/room-meter-issues/{unitReport.Id}/acknowledge", new { expectedVersion = unitReport.Version, reason = "Reviewer notified." }));
        Assert.Equal(RoomMeterIssueStatus.Acknowledged, acknowledged.Status);
        Assert.Equal(2, acknowledged.Audit.Count);
        var resolved = await ReadAsync<FeedbackResponse>(await manager.PostAsJsonAsync($"/api/room-meter-issues/{unitReport.Id}/resolve", new { expectedVersion = acknowledged.Version, reason = "Light replacement scheduled." }));
        Assert.Equal(RoomMeterIssueStatus.Resolved, resolved.Status);
        Assert.Equal(3, resolved.Audit.Count);
        var staffUpdated = Assert.Single(await ReadAsync<List<FeedbackResponse>>(await staff.GetAsync($"/api/properties/{property.Id}/room-meter-issues")), item => item.Id == unitReport.Id);
        Assert.Equal(RoomMeterIssueStatus.Resolved, staffUpdated.Status);
        Assert.Equal(3, staffUpdated.Audit.Count);

        Assert.Equal(HttpStatusCode.Forbidden, (await finance.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unassignedReviewer.GetAsync($"/api/properties/{property.Id}/room-meter-issues")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await unassignedReviewer.PostAsJsonAsync($"/api/properties/{property.Id}/feedback-reports", new { unitId = unit.Id, note = "Not assigned", submissionKey = Guid.NewGuid() })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/role-permissions", new
        {
            role = "Manager",
            permissions = new Dictionary<string, bool> { ["ReviewRoomMeterIssues"] = false },
            reason = "Revoke disposable reviewer access",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/notifications")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/properties/{property.Id}/staff/{staffUser.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/api/properties/{property.Id}/room-meter-issues")).StatusCode);
        var adminReports = await ReadAsync<List<FeedbackResponse>>(await admin.GetAsync($"/api/properties/{property.Id}/room-meter-issues"));
        Assert.Equal(2, adminReports.Count);
    }

    [PostgresFact]
    public async Task PostgreSQL_upgrades_preceding_schema_and_backfills_legacy_warning_defaults()
    {
        RequirePostgres();

        var legacyConnectionString = await database.CreateAdditionalDatabaseAsync();
        var options = new DbContextOptionsBuilder<PropertyManagementDbContext>()
            .UseNpgsql(legacyConnectionString)
            .Options;
        await using var context = new PropertyManagementDbContext(options);
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20261009101949_PrintableBillingReports");

        var organizationId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var floorId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await using (var connection = new NpgsqlConnection(legacyConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                INSERT INTO "organizations" ("Id", "Name", "CreatedAtUtc")
                VALUES (@organization, 'Legacy Organization', @now);
                INSERT INTO "properties" ("Id", "OrganizationId", "Name", "Address", "PropertyType", "IsArchived", "CreatedAtUtc")
                VALUES (@property, @organization, 'Legacy Property', 'Legacy Address', 'ApartmentBuilding', FALSE, @now);
                INSERT INTO "floors" ("Id", "OrganizationId", "PropertyId", "Name", "SortOrder", "IsArchived")
                VALUES (@floor, @organization, @property, 'Ground', 0, FALSE);
                INSERT INTO "units" ("Id", "OrganizationId", "PropertyId", "FloorId", "Name", "IsEnabled", "IsArchived", "OccupancyStatus")
                VALUES (@unit, @organization, @property, @floor, 'G-1', TRUE, FALSE, 'Vacant');
                INSERT INTO "charge_configurations" ("Id", "OrganizationId", "PropertyId", "UnitId", "ChargeType", "Mode", "BillingMonth", "Amount", "CustomLabel")
                VALUES (@configuration, @organization, @property, NULL, 'Electricity', 'Custom', DATE '2026-01-01', 19.9000, NULL);
                INSERT INTO "billing_cycles" ("Id", "OrganizationId", "PropertyId", "BillingMonth", "Status", "Version", "LastChangedByUserId", "LastChangedAtUtc", "SubmittedByUserId", "SubmittedAtUtc", "ReturnedByUserId", "ReturnedAtUtc", "ReturnReason", "ApprovedByUserId", "ApprovedAtUtc", "ApprovedTotal")
                VALUES (@cycle, @organization, @property, DATE '2026-01-01', 'Approved', 2, @actor, @now, @actor, @now, NULL, NULL, NULL, @actor, @now, 199.00);
                INSERT INTO "billing_unit_bills" ("Id", "OrganizationId", "BillingCycleId", "UnitId", "UnitNameSnapshot", "OccupancyAssignmentId", "TenantId", "TenantNameSnapshot", "TenantPhoneSnapshot", "OccupancyStartDate", "OccupancyEndDate", "BlockingReason", "Total", "ElectricityWarning", "FloorNameSnapshot")
                VALUES (@bill, @organization, @cycle, @unit, 'G-1', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 199.00, TRUE, 'Ground');
                INSERT INTO "billing_charge_lines" ("Id", "OrganizationId", "BillingUnitBillId", "ChargeType", "ChargeNameSnapshot", "SettingModeSnapshot", "IsMetered", "RateSnapshot", "PreviousReading", "CurrentReading", "ConsumptionUnits", "PreviousReadingSource", "Amount")
                VALUES (@line, @organization, @bill, 'Electricity', 'Electricity', 'Custom', TRUE, 19.9000, 100.0000, 110.0000, 10.0000, 'Missing', 199.00);
                """, connection);
            command.Parameters.AddWithValue("organization", organizationId);
            command.Parameters.AddWithValue("property", propertyId);
            command.Parameters.AddWithValue("floor", floorId);
            command.Parameters.AddWithValue("unit", unitId);
            command.Parameters.AddWithValue("configuration", Guid.NewGuid());
            command.Parameters.AddWithValue("cycle", cycleId);
            command.Parameters.AddWithValue("bill", billId);
            command.Parameters.AddWithValue("line", lineId);
            command.Parameters.AddWithValue("actor", actorId);
            command.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
            await command.ExecuteNonQueryAsync();
        }

        await migrator.MigrateAsync();

        await using (var connection = new NpgsqlConnection(legacyConnectionString))
        {
            await connection.OpenAsync();
            await using (var configuration = new NpgsqlCommand("SELECT \"WarningMode\", \"WarningThreshold\" FROM \"charge_configurations\" WHERE \"PropertyId\" = @property", connection))
            {
                configuration.Parameters.AddWithValue("property", propertyId);
                await using var reader = await configuration.ExecuteReaderAsync(CommandBehavior.SingleRow);
                Assert.True(await reader.ReadAsync());
                Assert.Equal("Custom", reader.GetString(0));
                Assert.Equal(200m, reader.GetDecimal(1));
            }

            await using (var line = new NpgsqlCommand("SELECT \"RateSnapshot\", \"PreviousReading\", \"CurrentReading\", \"WarningEnabled\", \"WarningThreshold\", \"PreviousReadingDate\", \"CurrentReadingDate\" FROM \"billing_charge_lines\" WHERE \"Id\" = @line", connection))
            {
                line.Parameters.AddWithValue("line", lineId);
                await using var reader = await line.ExecuteReaderAsync(CommandBehavior.SingleRow);
                Assert.True(await reader.ReadAsync());
                Assert.Equal(19.9000m, reader.GetDecimal(0));
                Assert.Equal(100.0000m, reader.GetDecimal(1));
                Assert.Equal(110.0000m, reader.GetDecimal(2));
                Assert.True(reader.GetBoolean(3));
                Assert.Equal(200m, reader.GetDecimal(4));
                Assert.True(await reader.IsDBNullAsync(5));
                Assert.True(await reader.IsDBNullAsync(6));
            }

            await using var bill = new NpgsqlCommand("SELECT \"Total\", \"ElectricityWarning\" FROM \"billing_unit_bills\" WHERE \"Id\" = @bill", connection);
            bill.Parameters.AddWithValue("bill", billId);
            await using var billReader = await bill.ExecuteReaderAsync(CommandBehavior.SingleRow);
            Assert.True(await billReader.ReadAsync());
            Assert.Equal(199.00m, billReader.GetDecimal(0));
            Assert.True(billReader.GetBoolean(1));
        }
    }

    [PostgresFact]
    public async Task PostgreSQL_persists_rate_warning_modes_reading_dates_and_approved_snapshots()
    {
        RequirePostgres();

        using var api = new PostgresPrimeTectApiFactory(database.ConnectionString);
        using var admin = api.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await RegisterAsync(admin, $"settings-{suffix}@example.test", "Settings Admin");

        var property = await ReadAsync<PropertyResponse>(await admin.PostAsJsonAsync("/api/properties", new
        {
            name = $"Settings Property {suffix}",
            address = "7 Verification Street",
            propertyType = "ApartmentBuilding",
            floors = new[] { new { name = "Ground", unitCount = 3, unitNames = new[] { "R-1", "R-2", "R-3" } } },
        }));
        var tenant = await ReadAsync<TenantResponse>(await admin.PostAsJsonAsync("/api/tenants", new { name = (string?)null, phone = (string?)null }));
        foreach (var unit in property.Floors.Single().Units)
        {
            Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/units/{unit.Id}/occupancy", new
            {
                tenantId = tenant.Id,
                startDate = "2026-01-01",
                endDate = (string?)null,
            })).StatusCode);
        }

        await PutChargesAsync(admin, property.Id, new[]
        {
            new
            {
                chargeType = "Electricity",
                mode = "Custom",
                billingMonth = "2026-02-01",
                amount = (decimal?)10m,
                customLabel = (string?)null,
                warningMode = "Custom",
                warningThreshold = (decimal?)200m,
            },
        });
        await PutUnitChargesAsync(admin, property.Id, property.Floors.Single().Units[1].Id, new[]
        {
            new
            {
                chargeType = "Electricity",
                mode = "Custom",
                billingMonth = "2026-02-01",
                amount = (decimal?)12m,
                customLabel = (string?)null,
                warningMode = "Custom",
                warningThreshold = (decimal?)300m,
            },
        });
        await PutUnitChargesAsync(admin, property.Id, property.Floors.Single().Units[2].Id, new[]
        {
            new
            {
                chargeType = "Electricity",
                mode = "Inherited",
                billingMonth = "2026-02-01",
                amount = (decimal?)null,
                customLabel = (string?)null,
                warningMode = "Disabled",
                warningThreshold = (decimal?)null,
            },
        });

        var draft = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));
        var firstDraft = draft.Bills.Single(bill => bill.UnitName == "R-1").Charges.Single();
        var customDraft = draft.Bills.Single(bill => bill.UnitName == "R-2").Charges.Single();
        var disabledDraft = draft.Bills.Single(bill => bill.UnitName == "R-3").Charges.Single();
        Assert.Equal(10m, firstDraft.Rate);
        Assert.Equal(200m, firstDraft.WarningThreshold);
        Assert.True(firstDraft.WarningEnabled);
        Assert.Equal(12m, customDraft.Rate);
        Assert.Equal(300m, customDraft.WarningThreshold);
        Assert.True(customDraft.WarningEnabled);
        Assert.Equal(ChargeSettingMode.Custom, disabledDraft.SettingMode);
        Assert.Equal(10m, disabledDraft.Rate);
        Assert.False(disabledDraft.WarningEnabled);
        Assert.Null(disabledDraft.WarningThreshold);

        var saved = await ReadAsync<BillingCycleResponse>(await admin.PutAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{draft.Id}/meter-readings", new
        {
            expectedVersion = draft.Version,
            reason = "Opening readings and dates entered from the documented meter sheet",
            entries = draft.Bills.Select(bill => new
            {
                unitBillId = bill.Id,
                previousReading = (decimal?)10020m,
                currentReading = (decimal?)10040m,
                previousReadingDate = "2026-01-01",
                currentReadingDate = "2026-02-01",
            }),
        }));
        var firstSaved = saved.Bills.Single(bill => bill.UnitName == "R-1").Charges.Single();
        var customSaved = saved.Bills.Single(bill => bill.UnitName == "R-2").Charges.Single();
        var disabledSaved = saved.Bills.Single(bill => bill.UnitName == "R-3").Charges.Single();
        Assert.Equal(200m, firstSaved.Amount);
        Assert.False(firstSaved.WarningEnabled && firstSaved.WarningThreshold is not null && firstSaved.Amount < firstSaved.WarningThreshold.Value);
        Assert.Equal(240m, customSaved.Amount);
        Assert.True(customSaved.WarningEnabled && customSaved.WarningThreshold is not null && customSaved.Amount < customSaved.WarningThreshold.Value);
        Assert.Equal(new DateOnly(2026, 1, 1), firstSaved.PreviousReadingDate);
        Assert.Equal(new DateOnly(2026, 2, 1), firstSaved.CurrentReadingDate);
        Assert.Equal(200m, disabledSaved.Amount);
        Assert.False(disabledSaved.WarningEnabled);

        var submitted = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{draft.Id}/submit", new { expectedVersion = saved.Version }));
        var invitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"settings-approver-{suffix}@example.test", role = "Admin" }));
        using var manager = api.CreateClient();
        await AcceptInvitationAsync(manager, invitation.InvitationToken, "Settings Approver");
        var approved = await ReadAsync<BillingCycleResponse>(await manager.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{draft.Id}/approve", new { expectedVersion = submitted.Version }));
        Assert.Equal(BillingCycleStatus.Approved, approved.Status);

        await PutChargesAsync(admin, property.Id, new[]
        {
            new
            {
                chargeType = "Electricity",
                mode = "Custom",
                billingMonth = "2026-03-01",
                amount = (decimal?)99m,
                customLabel = (string?)null,
                warningMode = "Custom",
                warningThreshold = (decimal?)999m,
            },
        });
        var historical = await ReadAsync<BillingCycleResponse>(await admin.GetAsync($"/api/properties/{property.Id}/billing-cycles/2026-02"));
        Assert.Equal(10m, historical.Bills.Single(bill => bill.UnitName == "R-1").Charges.Single().Rate);
        Assert.Equal(200m, historical.Bills.Single(bill => bill.UnitName == "R-1").Charges.Single().WarningThreshold);
        Assert.Equal(12m, historical.Bills.Single(bill => bill.UnitName == "R-2").Charges.Single().Rate);
        Assert.Equal(300m, historical.Bills.Single(bill => bill.UnitName == "R-2").Charges.Single().WarningThreshold);
        Assert.False(historical.Bills.Single(bill => bill.UnitName == "R-3").Charges.Single().WarningEnabled);

        var next = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-03-01" }));
        var carried = next.Bills.Single(bill => bill.UnitName == "R-1").Charges.Single();
        Assert.Equal(MeterReadingSource.ApprovedPeriod, carried.PreviousReadingSource);
        Assert.Equal(10040m, carried.PreviousReading);
        Assert.Equal(new DateOnly(2026, 2, 1), carried.PreviousReadingDate);
        Assert.Equal(99m, carried.Rate);
        Assert.Equal(999m, carried.WarningThreshold);
    }

    [PostgresFact]
    public async Task Postgres_workflow_preserves_snapshots_gaps_isolation_and_atomic_approval()
    {
        RequirePostgres();

        using var api = new PostgresPrimeTectApiFactory(database.ConnectionString);
        using var admin = api.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adminEmail = $"billing-admin-{suffix}@example.test";
        await RegisterAsync(admin, adminEmail, "Billing Admin");

        var property = await ReadAsync<PropertyResponse>(await admin.PostAsJsonAsync("/api/properties", new
        {
            name = $"Billing Property {suffix}",
            address = "1 Verification Street",
            propertyType = "ApartmentBuilding",
            floors = new[]
            {
                new { name = "Ground", unitCount = 1, unitNames = new[] { "G-1" } },
                new { name = "Upper", unitCount = 2, unitNames = new[] { "U-1", "U-2" } },
            },
        }));
        Assert.Equal(new[] { 1, 2 }, property.Floors.Select(floor => floor.Units.Count).ToArray());

        var tenant = await ReadAsync<TenantResponse>(await admin.PostAsJsonAsync("/api/tenants", new { name = (string?)null, phone = (string?)null }));
        var occupiedUnit = property.Floors[0].Units[0];
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/units/{occupiedUnit.Id}/occupancy", new
        {
            tenantId = tenant.Id,
            startDate = "2026-01-01",
            endDate = (string?)null,
        })).StatusCode);

        await PutChargesAsync(admin, property.Id, new[]
        {
            new { chargeType = "Electricity", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)19.9000m, customLabel = (string?)null },
            new { chargeType = "ServiceCharge", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)12.3456m, customLabel = (string?)null },
        });

        var first = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" })).StatusCode);
        await AssertDatabaseRejectsDuplicateCycleAsync(property.Id, adminEmail);
        Assert.Null(first.Bills.Single(bill => bill.UnitName == "G-1").TenantName);
        Assert.Equal(3, first.Bills.Count);
        Assert.Contains("immediately preceding approved period", first.Bills.Single(bill => bill.UnitName == "G-1").BlockingReason);

        var electricity = first.Bills.Single(bill => bill.UnitName == "G-1").Charges.Single(charge => charge.ChargeType == ChargeType.Electricity);
        Assert.Equal(MeterReadingSource.Missing, electricity.PreviousReadingSource);
        Assert.Null(electricity.PreviousReading);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{first.Id}/meter-readings", new
        {
            expectedVersion = first.Version,
            entries = new[] { new { unitBillId = electricity.Id, previousReading = 100m, currentReading = 99m } },
        })).StatusCode);

        var policyDraft = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));
        Assert.True(policyDraft.BlockingBillCount > 0);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{policyDraft.Id}/submit", new { expectedVersion = policyDraft.Version })).StatusCode);

        var managerInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"billing-operator-{suffix}@example.test", role = "Admin" }));
        using var manager = api.CreateClient();
        await AcceptInvitationAsync(manager, managerInvitation.InvitationToken, "Billing Operator");

        var fixedProperty = await ReadAsync<PropertyResponse>(await admin.PostAsJsonAsync("/api/properties", new
        {
            name = $"Fixed Property {suffix}",
            address = "2 Verification Street",
            propertyType = "Shop",
            floors = new[] { new { name = "Ground", unitCount = 1, unitNames = new[] { "S-1" } } },
        }));
        var fixedUnit = fixedProperty.Floors.Single().Units.Single();
        var fixedTenant = await ReadAsync<TenantResponse>(await admin.PostAsJsonAsync("/api/tenants", new { name = "Fixed Tenant", phone = (string?)null }));
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{fixedProperty.Id}/units/{fixedUnit.Id}/occupancy", new { tenantId = fixedTenant.Id, startDate = "2026-01-01", endDate = (string?)null })).StatusCode);
        await PutChargesAsync(admin, fixedProperty.Id, new[] { new { chargeType = "ServiceCharge", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)50.00m, customLabel = (string?)null } });

        var fixedDraft = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{fixedProperty.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        var selfSubmitted = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{fixedProperty.Id}/billing-cycles/{fixedDraft.Id}/submit", new { expectedVersion = fixedDraft.Version }));
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/properties/{fixedProperty.Id}/billing-cycles/{fixedDraft.Id}/approve", new { expectedVersion = selfSubmitted.Version })).StatusCode);

        api.FailNextSave = true;
        Assert.Equal(HttpStatusCode.InternalServerError, (await manager.PostAsJsonAsync($"/api/properties/{fixedProperty.Id}/billing-cycles/{fixedDraft.Id}/approve", new { expectedVersion = selfSubmitted.Version })).StatusCode);
        var afterFailedApproval = await ReadAsync<BillingCycleResponse>(await admin.GetAsync($"/api/properties/{fixedProperty.Id}/billing-cycles/2026-01"));
        Assert.Equal(BillingCycleStatus.Submitted, afterFailedApproval.Status);
        Assert.DoesNotContain(afterFailedApproval.Audit, audit => audit.Action == BillingAuditAction.Approved);

        var approved = await ReadAsync<BillingCycleResponse>(await manager.PostAsJsonAsync($"/api/properties/{fixedProperty.Id}/billing-cycles/{fixedDraft.Id}/approve", new { expectedVersion = afterFailedApproval.Version }));
        Assert.Equal(BillingCycleStatus.Approved, approved.Status);
        await PutChargesAsync(admin, fixedProperty.Id, new[] { new { chargeType = "ServiceCharge", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)99.99m, customLabel = (string?)null } });
        var unchanged = await ReadAsync<BillingCycleResponse>(await admin.GetAsync($"/api/properties/{fixedProperty.Id}/billing-cycles/2026-01"));
        Assert.Equal(50.00m, unchanged.ApprovedTotal);
        Assert.Equal(50.00m, unchanged.Bills.Single().Charges.Single().Amount);

        var next = await ReadAsync<BillingCycleResponse>(await manager.PostAsJsonAsync($"/api/properties/{fixedProperty.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));
        Assert.Equal(BillingCycleStatus.Draft, next.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/properties/{fixedProperty.Id}/billing-cycles", new { billingMonth = "2026-02-01" })).StatusCode);

        using var otherOrganization = api.CreateClient();
        await RegisterAsync(otherOrganization, $"other-{suffix}@example.test", "Other Admin");
        Assert.Equal(HttpStatusCode.NotFound, (await otherOrganization.GetAsync($"/api/properties/{fixedProperty.Id}/billing-cycles/2026-01")).StatusCode);

        var meterProperty = await ReadAsync<PropertyResponse>(await admin.PostAsJsonAsync("/api/properties", new
        {
            name = $"Meter Property {suffix}",
            address = "4 Verification Street",
            propertyType = "ApartmentBuilding",
            floors = new[] { new { name = "Ground", unitCount = 1, unitNames = new[] { "M-1" } } },
        }));
        var meterUnit = meterProperty.Floors.Single().Units.Single();
        var meterTenant = await ReadAsync<TenantResponse>(await admin.PostAsJsonAsync("/api/tenants", new { name = (string?)null, phone = (string?)null }));
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{meterProperty.Id}/units/{meterUnit.Id}/occupancy", new { tenantId = meterTenant.Id, startDate = "2026-01-01", endDate = (string?)null })).StatusCode);
        await PutChargesAsync(admin, meterProperty.Id, new[] { new { chargeType = "Electricity", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)19.9000m, customLabel = (string?)null } });
        var meterJanuary = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{meterProperty.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        var meterJanuaryBill = meterJanuary.Bills.Single();
        var meterJanuarySaved = await ReadAsync<BillingCycleResponse>(await manager.PutAsJsonAsync($"/api/properties/{meterProperty.Id}/billing-cycles/{meterJanuary.Id}/meter-readings", new
        {
            expectedVersion = meterJanuary.Version,
            reason = "Opening reading entered from the documented meter sheet",
            entries = new[] { new { unitBillId = meterJanuaryBill.Id, previousReading = 100.1234m, currentReading = 110.1234m } },
        }));
        var meterJanuarySubmitted = await ReadAsync<BillingCycleResponse>(await manager.PostAsJsonAsync($"/api/properties/{meterProperty.Id}/billing-cycles/{meterJanuary.Id}/submit", new { expectedVersion = meterJanuarySaved.Version }));
        var meterJanuaryApproved = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{meterProperty.Id}/billing-cycles/{meterJanuary.Id}/approve", new { expectedVersion = meterJanuarySubmitted.Version }));
        Assert.Equal(BillingCycleStatus.Approved, meterJanuaryApproved.Status);

        await PutChargesAsync(admin, meterProperty.Id, new[] { new { chargeType = "Electricity", mode = "Custom", billingMonth = "2026-02-01", amount = (decimal?)99.9999m, customLabel = (string?)null } });
        var meterFebruary = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{meterProperty.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));
        var meterFebruaryCharge = meterFebruary.Bills.Single().Charges.Single();
        Assert.Equal(MeterReadingSource.ApprovedPeriod, meterFebruaryCharge.PreviousReadingSource);
        Assert.Equal(110.1234m, meterFebruaryCharge.PreviousReading);
        Assert.Equal(99.9999m, meterFebruaryCharge.Rate);

        var meterMarch = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{meterProperty.Id}/billing-cycles", new { billingMonth = "2026-03-01" }));
        Assert.Contains("immediately preceding approved period", meterMarch.Bills.Single().BlockingReason);
    }

    [PostgresFact]
    public async Task PostgreSQL_rejects_stale_versions_and_records_atomic_approval_audit()
    {
        RequirePostgres();

        using var api = new PostgresPrimeTectApiFactory(database.ConnectionString);
        using var admin = api.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await RegisterAsync(admin, $"concurrency-{suffix}@example.test", "Concurrency Admin");
        var property = await ReadAsync<PropertyResponse>(await admin.PostAsJsonAsync("/api/properties", new
        {
            name = $"Concurrency Property {suffix}",
            address = "3 Verification Street",
            propertyType = "Shop",
            floors = new[] { new { name = "Ground", unitCount = 1, unitNames = new[] { "C-1" } } },
        }));
        await PutChargesAsync(admin, property.Id, new[] { new { chargeType = "ServiceCharge", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)200m, customLabel = (string?)null } });
        var concurrencyTenant = await ReadAsync<TenantResponse>(await admin.PostAsJsonAsync("/api/tenants", new { name = "Concurrency Tenant", phone = (string?)null }));
        var concurrencyUnit = property.Floors.Single().Units.Single();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/units/{concurrencyUnit.Id}/occupancy", new { tenantId = concurrencyTenant.Id, startDate = "2026-01-01", endDate = (string?)null })).StatusCode);
        var draft = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));

        using var secondClient = api.CreateClient();
        await LoginAsync(secondClient, $"concurrency-{suffix}@example.test");
        var firstSubmit = await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{draft.Id}/submit", new { expectedVersion = draft.Version });
        Assert.Equal(HttpStatusCode.OK, firstSubmit.StatusCode);
        var staleSubmit = await secondClient.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{draft.Id}/submit", new { expectedVersion = draft.Version });
        Assert.Equal(HttpStatusCode.Conflict, staleSubmit.StatusCode);
    }

    [PostgresFact]
    public async Task Approved_reports_group_by_snapshot_identity_and_mark_incomplete_comparisons()
    {
        RequirePostgres();

        using var api = new PostgresPrimeTectApiFactory(database.ConnectionString);
        using var admin = api.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var adminEmail = $"reports-admin-{suffix}@example.test";
        await RegisterAsync(admin, adminEmail, "Reports Admin");
        var invitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"reports-operator-{suffix}@example.test", role = "Admin" }));
        using var manager = api.CreateClient();
        await AcceptInvitationAsync(manager, invitation.InvitationToken, "Reports Operator");

        var property = await ReadAsync<PropertyResponse>(await admin.PostAsJsonAsync("/api/properties", new
        {
            name = $"Snapshot Property {suffix}",
            address = "5 Report Street",
            propertyType = "ApartmentBuilding",
            floors = new[]
            {
                new { name = "Ground", unitCount = 2, unitNames = new[] { "A-1", "A-2" } },
                new { name = "Upper", unitCount = 1, unitNames = new[] { "B-1" } },
            },
        }));
        var tenantOne = await ReadAsync<TenantResponse>(await admin.PostAsJsonAsync("/api/tenants", new { name = (string?)null, phone = (string?)null }));
        var tenantTwo = await ReadAsync<TenantResponse>(await admin.PostAsJsonAsync("/api/tenants", new { name = (string?)null, phone = (string?)null }));
        var groundUnits = property.Floors[0].Units;
        var upperUnit = property.Floors[1].Units.Single();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/units/{groundUnits[0].Id}/occupancy", new { tenantId = tenantOne.Id, startDate = "2026-01-01", endDate = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/units/{groundUnits[1].Id}/occupancy", new { tenantId = tenantOne.Id, startDate = "2026-01-01", endDate = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/units/{upperUnit.Id}/occupancy", new { tenantId = tenantTwo.Id, startDate = "2026-01-01", endDate = (string?)null })).StatusCode);

        await PutChargesAsync(admin, property.Id, new[]
        {
            new { chargeType = "Electricity", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)10m, customLabel = (string?)null },
            new { chargeType = "Custom", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)0m, customLabel = (string?)"Storage" },
            new { chargeType = "Electricity", mode = "Custom", billingMonth = "2026-02-01", amount = (decimal?)20m, customLabel = (string?)null },
            new { chargeType = "Custom", mode = "Custom", billingMonth = "2026-02-01", amount = (decimal?)10m, customLabel = (string?)"Storage" },
        });

        var january = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        var januarySaved = await ReadAsync<BillingCycleResponse>(await manager.PutAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{january.Id}/meter-readings", new
        {
            expectedVersion = january.Version,
            reason = "Opening readings entered for report verification",
            entries = january.Bills.Select((bill, index) => new { unitBillId = bill.Id, previousReading = 100m + index, currentReading = 110m + index }).ToArray(),
        }));
        var januarySubmitted = await ReadAsync<BillingCycleResponse>(await manager.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{january.Id}/submit", new { expectedVersion = januarySaved.Version }));
        var januaryApproved = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{january.Id}/approve", new { expectedVersion = januarySubmitted.Version }));
        Assert.Equal(BillingCycleStatus.Approved, januaryApproved.Status);

        var february = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));
        var februarySaved = await ReadAsync<BillingCycleResponse>(await manager.PutAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{february.Id}/meter-readings", new
        {
            expectedVersion = february.Version,
            reason = "Current readings entered for report verification",
            entries = february.Bills.Select((bill, index) => new { unitBillId = bill.Id, previousReading = 110m + index, currentReading = 120m + index }).ToArray(),
        }));
        var februarySubmitted = await ReadAsync<BillingCycleResponse>(await manager.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{february.Id}/submit", new { expectedVersion = februarySaved.Version }));
        var februaryApproved = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{february.Id}/approve", new { expectedVersion = februarySubmitted.Version }));
        Assert.Equal(BillingCycleStatus.Approved, februaryApproved.Status);

        var tenantStatement = await ReadAsync<BillingStatementResponse>(await admin.GetAsync($"/api/properties/{property.Id}/billing-reports/2026-02/tenants/{tenantOne.Id}"));
        Assert.Equal(2, tenantStatement.Bills.Count);
        Assert.All(tenantStatement.Bills, bill =>
        {
            Assert.Equal(tenantOne.Id, bill.TenantId);
            Assert.Null(bill.TenantName);
            Assert.Null(bill.TenantPhone);
            Assert.Contains(bill.Charges, charge => charge.ChargeType == ChargeType.Electricity && charge.Rate == 20m);
        });

        var propertySummary = await ReadAsync<PropertySummaryResponse>(await admin.GetAsync($"/api/properties/{property.Id}/billing-reports/2026-02/property-summary"));
        Assert.Equal(2, propertySummary.Floors.Count);
        Assert.Equal(februaryApproved.ApprovedTotal, propertySummary.ApprovedTotal);
        Assert.Equal(new[] { "Ground", "Upper" }, propertySummary.Floors.Select(floor => floor.FloorName).OrderBy(name => name).ToArray());

        var comparison = await ReadAsync<ComparisonResponse>(await admin.GetAsync($"/api/properties/{property.Id}/billing-reports/2026-02/comparison"));
        Assert.True(comparison.IsComplete);
        Assert.Equal("Approved", comparison.PreviousPeriodStatus);
        Assert.Equal(30m, comparison.Electricity.CurrentUnits);
        Assert.Equal(30m, comparison.Electricity.PreviousUnits);
        var zeroBaseline = comparison.Charges.Single(charge => charge.Label == "Storage");
        Assert.Equal(0m, zeroBaseline.PreviousAmount);
        Assert.Equal(30m, zeroBaseline.CurrentAmount);
        Assert.False(zeroBaseline.PercentageChangeAvailable);
        Assert.Null(zeroBaseline.PercentageChange);

        await PutChargesAsync(admin, property.Id, new[] { new { chargeType = "Electricity", mode = "Custom", billingMonth = "2026-02-01", amount = (decimal?)99m, customLabel = (string?)null } });
        var unchanged = await ReadAsync<BillingStatementResponse>(await admin.GetAsync($"/api/properties/{property.Id}/billing-reports/2026-02/units/{groundUnits[0].Id}"));
        Assert.Equal(20m, unchanged.Bills.Single().Charges.Single(charge => charge.ChargeType == ChargeType.Electricity).Rate);

        var gapProperty = await ReadAsync<PropertyResponse>(await admin.PostAsJsonAsync("/api/properties", new
        {
            name = $"Gap Property {suffix}",
            address = "6 Report Street",
            propertyType = "Shop",
            floors = new[] { new { name = "Only", unitCount = 1, unitNames = new[] { "G-1" } } },
        }));
        var gapTenant = await ReadAsync<TenantResponse>(await admin.PostAsJsonAsync("/api/tenants", new { name = "Gap Tenant", phone = (string?)null }));
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{gapProperty.Id}/units/{gapProperty.Floors.Single().Units.Single().Id}/occupancy", new { tenantId = gapTenant.Id, startDate = "2026-03-01", endDate = (string?)null })).StatusCode);
        await PutChargesAsync(admin, gapProperty.Id, new[] { new { chargeType = "ServiceCharge", mode = "Custom", billingMonth = "2026-03-01", amount = (decimal?)5m, customLabel = (string?)null } });
        var gapDraft = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{gapProperty.Id}/billing-cycles", new { billingMonth = "2026-03-01" }));
        var gapSubmitted = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{gapProperty.Id}/billing-cycles/{gapDraft.Id}/submit", new { expectedVersion = gapDraft.Version }));
        await ReadAsync<BillingCycleResponse>(await manager.PostAsJsonAsync($"/api/properties/{gapProperty.Id}/billing-cycles/{gapDraft.Id}/approve", new { expectedVersion = gapSubmitted.Version }));
        var incomplete = await ReadAsync<ComparisonResponse>(await admin.GetAsync($"/api/properties/{gapProperty.Id}/billing-reports/2026-03/comparison"));
        Assert.False(incomplete.IsComplete);
        Assert.Equal("Missing", incomplete.PreviousPeriodStatus);
        Assert.Contains("missing", incomplete.IncompleteReason!, StringComparison.OrdinalIgnoreCase);

        using var otherOrganization = api.CreateClient();
        await RegisterAsync(otherOrganization, $"unauthorized-reports-{suffix}@example.test", "Unauthorized Reports");
        Assert.Equal(HttpStatusCode.NotFound, (await otherOrganization.GetAsync($"/api/properties/{property.Id}/billing-reports/2026-02/property-summary")).StatusCode);
    }

    [PostgresFact]
    public async Task PostgreSQL_manual_collection_reports_persist_independent_approval_and_balance_snapshots()
    {
        RequirePostgres();

        using var api = new PostgresPrimeTectApiFactory(database.ConnectionString);
        using var admin = api.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await RegisterAsync(admin, $"collections-admin-{suffix}@example.test", "Collections Admin");

        var property = await ReadAsync<PropertyResponse>(await admin.PostAsJsonAsync("/api/properties", new
        {
            name = $"Collections Property {suffix}",
            address = "7 Collection Street",
            propertyType = "ApartmentBuilding",
            floors = new[] { new { name = "Ground", unitCount = 1, unitNames = new[] { "C-1" } } },
        }));
        var unit = property.Floors.Single().Units.Single();
        var tenant = await ReadAsync<TenantResponse>(await admin.PostAsJsonAsync("/api/tenants", new { name = (string?)null, phone = (string?)null }));
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/units/{unit.Id}/occupancy", new { tenantId = tenant.Id, startDate = "2026-01-01", endDate = (string?)null })).StatusCode);
        await PutChargesAsync(admin, property.Id, new[] { new { chargeType = "ServiceCharge", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)300m, customLabel = (string?)null } });

        var draft = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        var submitted = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{draft.Id}/submit", new { expectedVersion = draft.Version }));
        var secondAdminInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"collections-approver-{suffix}@example.test", role = "Admin" }));
        using var secondAdmin = api.CreateClient();
        await AcceptInvitationAsync(secondAdmin, secondAdminInvitation.InvitationToken, "Collections Approver");
        var approvedBill = await ReadAsync<BillingCycleResponse>(await secondAdmin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{draft.Id}/approve", new { expectedVersion = submitted.Version }));
        Assert.Equal(BillingCycleStatus.Approved, approvedBill.Status);

        var managerInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"collections-manager-{suffix}@example.test", role = "Manager" }));
        using var manager = api.CreateClient();
        var managerUser = await AcceptInvitationAsync(manager, managerInvitation.InvitationToken, "Collections Manager");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/properties/{property.Id}/staff/{managerUser.Id}", null)).StatusCode);

        var financeInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"collections-finance-{suffix}@example.test", role = "FinanceManager" }));
        using var finance = api.CreateClient();
        var financeUser = await AcceptInvitationAsync(finance, financeInvitation.InvitationToken, "Collections Finance");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/properties/{property.Id}/staff/{financeUser.Id}", null)).StatusCode);

        var report = await ReadAsync<CollectionReportResponse>(await manager.PostAsJsonAsync($"/api/properties/{property.Id}/collection-reports", new
        {
            billingUnitBillId = approvedBill.Bills.Single().Id,
            amountReceived = 125.55m,
            receivedDate = "2026-01-15",
            paymentMethod = "BankTransfer",
            referenceNote = "Manual ledger reference",
            submissionKey = Guid.NewGuid(),
        }));
        Assert.Equal(CollectionReportStatus.Pending, report.Status);
        Assert.Equal(300m, report.BilledAmount);
        Assert.Equal(300m, report.RemainingBalance);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync($"/api/collection-reports/{report.Id}/approve", new { expectedVersion = report.Version })).StatusCode);

        var approvedReport = await ReadAsync<CollectionReportResponse>(await finance.PostAsJsonAsync($"/api/collection-reports/{report.Id}/approve", new { expectedVersion = report.Version }));
        Assert.Equal(CollectionReportStatus.Approved, approvedReport.Status);
        Assert.Equal(125.55m, approvedReport.ConfirmedCollectedAmount);
        Assert.Equal(174.45m, approvedReport.RemainingBalance);

        var failedReport = await ReadAsync<CollectionReportResponse>(await manager.PostAsJsonAsync($"/api/properties/{property.Id}/collection-reports", new
        {
            billingUnitBillId = approvedBill.Bills.Single().Id,
            amountReceived = 25m,
            receivedDate = "2026-01-16",
            paymentMethod = "Cash",
            referenceNote = "Atomic rollback check",
            submissionKey = Guid.NewGuid(),
        }));
        api.FailNextSave = true;
        Assert.Equal(HttpStatusCode.InternalServerError, (await finance.PostAsJsonAsync($"/api/collection-reports/{failedReport.Id}/approve", new { expectedVersion = failedReport.Version })).StatusCode);
        var afterFailedApproval = await ReadAsync<CollectionQueueResponse>(await finance.GetAsync($"/api/properties/{property.Id}/collection-reports/2026-01"));
        Assert.Equal(125.55m, afterFailedApproval.ConfirmedCollectedTotal);
        Assert.Equal(CollectionReportStatus.Pending, afterFailedApproval.Items.Single(item => item.Id == failedReport.Id).Status);
        var retryApproved = await ReadAsync<CollectionReportResponse>(await finance.PostAsJsonAsync($"/api/collection-reports/{failedReport.Id}/approve", new { expectedVersion = failedReport.Version }));
        Assert.Equal(CollectionReportStatus.Approved, retryApproved.Status);
        Assert.Equal(150.55m, retryApproved.ConfirmedCollectedAmount);
        Assert.Equal(149.45m, retryApproved.RemainingBalance);

        await PutChargesAsync(admin, property.Id, new[] { new { chargeType = "ServiceCharge", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)999.99m, customLabel = (string?)null } });
        var unchanged = await ReadAsync<CollectionQueueResponse>(await finance.GetAsync($"/api/properties/{property.Id}/collection-reports/2026-01"));
        Assert.Equal(300m, unchanged.BilledTotal);
        Assert.Equal(150.55m, unchanged.ConfirmedCollectedTotal);
        Assert.Equal(149.45m, unchanged.OutstandingTotal);

        using var otherOrganization = api.CreateClient();
        await RegisterAsync(otherOrganization, $"collections-other-{suffix}@example.test", "Other Collections Admin");
        Assert.Equal(HttpStatusCode.NotFound, (await otherOrganization.GetAsync($"/api/properties/{property.Id}/collection-reports/2026-01")).StatusCode);
    }

    private void RequirePostgres()
    {
        if (!database.IsAvailable)
        {
            Assert.Fail(database.Blocker!);
        }
    }

    private static async Task RegisterAsync(HttpClient client, string email, string fullName)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register-owner", new
        {
            organizationName = $"Organization {Guid.NewGuid():N}",
            fullName,
            email,
            password = "StrongPass!123",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "StrongPass!123" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<ApiUserResponse> AcceptInvitationAsync(HttpClient client, string token, string fullName)
    {
        var response = await client.PostAsJsonAsync("/api/auth/accept-invitation", new { token, fullName, password = "StrongPass!123" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiUserResponse>(JsonOptions))!;
    }

    private static async Task PutChargesAsync(HttpClient client, Guid propertyId, object[] items)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/properties/{propertyId}/charges", new { items })).StatusCode);
    }

    private static async Task PutUnitChargesAsync(HttpClient client, Guid propertyId, Guid unitId, object[] items)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/properties/{propertyId}/units/{unitId}/charges", new { items })).StatusCode);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private async Task<PropertyDataCounts> ReadPropertyDataCountsAsync(Guid propertyId)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT
                (SELECT COUNT(*) FROM "billing_cycles" WHERE "PropertyId" = @property),
                (SELECT COUNT(*) FROM "billing_unit_bills" bill JOIN "billing_cycles" cycle ON cycle."Id" = bill."BillingCycleId" WHERE cycle."PropertyId" = @property),
                (SELECT COUNT(*) FROM "billing_charge_lines" line JOIN "billing_unit_bills" bill ON bill."Id" = line."BillingUnitBillId" JOIN "billing_cycles" cycle ON cycle."Id" = bill."BillingCycleId" WHERE cycle."PropertyId" = @property),
                (SELECT COUNT(*) FROM "collection_reports" report JOIN "billing_unit_bills" bill ON bill."Id" = report."BillingUnitBillId" JOIN "billing_cycles" cycle ON cycle."Id" = bill."BillingCycleId" WHERE cycle."PropertyId" = @property)
            """, connection);
        command.Parameters.AddWithValue("property", propertyId);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
        Assert.True(await reader.ReadAsync());
        return new PropertyDataCounts(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3));
    }

    private static async Task<(int Precision, int Scale)> NumericDefinitionAsync(NpgsqlConnection connection, string table, string column)
    {
        await using var command = new NpgsqlCommand("SELECT numeric_precision, numeric_scale FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table AND column_name = @column", connection);
        command.Parameters.AddWithValue("table", table);
        command.Parameters.AddWithValue("column", column);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
        Assert.True(await reader.ReadAsync(), $"Missing numeric column {table}.{column}.");
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private async Task AssertDatabaseRejectsDuplicateCycleAsync(Guid propertyId, string adminEmail)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO "billing_cycles" ("Id", "OrganizationId", "PropertyId", "BillingMonth", "Status", "Version", "LastChangedByUserId", "LastChangedAtUtc")
            SELECT @id, "OrganizationId", @property, DATE '2026-01-01', 'Draft', 0, "Id", NOW()
            FROM "users"
            WHERE "Email" = @email
            """, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("property", propertyId);
        command.Parameters.AddWithValue("email", adminEmail);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("23505", exception.SqlState);
        Assert.Contains("billing_cycles", exception.ConstraintName ?? exception.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private sealed record PropertyResponse(Guid Id, string Name, string Address, PropertyType PropertyType, bool IsArchived, List<FloorResponse> Floors);

    private sealed record FloorResponse(Guid Id, string Name, int SortOrder, bool IsArchived, List<UnitResponse> Units);

    private sealed record UnitResponse(Guid Id, string Name, bool IsEnabled, bool IsArchived, OccupancyStatus OccupancyStatus);

    private sealed record TenantResponse(Guid Id, string? Name, string? Phone, OnboardingStatus OnboardingStatus);

    private sealed record InvitationResponse(Guid Id, string Email, UserRole Role, DateTimeOffset ExpiresAtUtc, string InvitationToken, string DeliveryStatus);

    private sealed record ApiUserResponse(Guid Id, string Email, string FullName, UserRole Role, Guid OrganizationId);

    private sealed record BillingCycleResponse(Guid Id, Guid PropertyId, DateOnly BillingMonth, BillingCycleStatus Status, long Version, Guid LastChangedByUserId, DateTimeOffset LastChangedAtUtc, string? ReturnReason, decimal DraftTotal, decimal? ApprovedTotal, int BlockingBillCount, List<BillingUnitBillResponse> Bills, List<BillingAuditResponse> Audit);

    private sealed record BillingUnitBillResponse(Guid Id, Guid UnitId, string UnitName, Guid? OccupancyAssignmentId, Guid? TenantId, string? TenantName, string? TenantPhone, DateOnly? OccupancyStartDate, DateOnly? OccupancyEndDate, string? BlockingReason, decimal Total, bool ElectricityWarning, List<BillingChargeLineResponse> Charges);

    private sealed record BillingChargeLineResponse(Guid Id, ChargeType ChargeType, string ChargeName, ChargeSettingMode SettingMode, bool IsMetered, decimal? Rate, decimal? PreviousReading, DateOnly? PreviousReadingDate, decimal? CurrentReading, DateOnly? CurrentReadingDate, decimal? ConsumptionUnits, MeterReadingSource PreviousReadingSource, bool WarningEnabled, decimal? WarningThreshold, decimal Amount);

    private sealed record BillingAuditResponse(Guid Id, Guid ActorUserId, BillingAuditAction Action, DateTimeOffset OccurredAtUtc, string? Reason, string? Before, string? After);

    private sealed record CollectionReportResponse(Guid Id, Guid BillingUnitBillId, Guid BillingCycleId, Guid PropertyId, DateOnly BillingMonth, Guid UnitId, string UnitName, string? FloorName, Guid? OccupancyAssignmentId, Guid? TenantId, string? TenantName, string? TenantPhone, decimal BilledAmount, decimal ConfirmedCollectedAmount, decimal RemainingBalance, decimal AmountReceived, DateOnly ReceivedDate, PaymentMethod PaymentMethod, string? ReferenceNote, Guid ReporterUserId, string ReporterName, DateTimeOffset SubmittedAtUtc, CollectionReportStatus Status, long Version, string? ReturnReason, Guid? ApprovedByUserId, DateTimeOffset? ApprovedAtUtc, bool IsReversed, CollectionReversalStatus? ReversalStatus, Guid? ReversalId, long? ReversalVersion, string? ReversalReason, List<CollectionAuditResponse> Audit);

    private sealed record CollectionAuditResponse(Guid Id, Guid? CollectionReversalId, Guid ActorUserId, CollectionAuditAction Action, DateTimeOffset OccurredAtUtc, string? Reason, string? Before, string? After);

    private sealed record CollectionQueueResponse(List<CollectionReportResponse> Items, decimal BilledTotal, decimal PendingReportedTotal, decimal ConfirmedCollectedTotal, decimal OutstandingTotal);

    private sealed record FeedbackResponse(Guid Id, Guid PropertyId, Guid? UnitId, string UnitName, string? FloorName, Guid ReporterUserId, string ReporterName, RoomMeterIssueType? IssueType, string Description, DateOnly? ObservedDate, DateTimeOffset CreatedAtUtc, RoomMeterIssueStatus Status, long Version, string? ResolutionReason, List<RoomMeterIssueAuditResponse> Audit, RoomMeterIssueTarget Target, bool IsFeedback);

    private sealed record RoomMeterIssueAuditResponse(Guid Id, Guid ActorUserId, RoomMeterIssueAuditAction Action, DateTimeOffset OccurredAtUtc, string? Reason, string? Before, string? After);

    private sealed record NotificationResponse(Guid Id, Guid RoomMeterIssueId, Guid PropertyId, Guid? UnitId, string Title, string PropertyName, string UnitLabel, string TimeZoneId, DateTimeOffset CreatedAtUtc, DateTimeOffset? ReadAtUtc, bool IsRead);

    private sealed record BillingStatementResponse(string StatementType, Guid PropertyId, Guid BillingCycleId, string PropertyName, string PropertyAddress, DateOnly BillingMonth, DateTimeOffset ApprovalDate, decimal ApprovedTotal, Guid? TenantId, List<BillingStatementUnitResponse> Bills);

    private sealed record BillingStatementUnitResponse(string BillReference, Guid BillId, Guid UnitId, string UnitName, string? FloorName, Guid? OccupancyAssignmentId, Guid? TenantId, string? TenantName, string? TenantPhone, DateOnly? OccupancyStartDate, DateOnly? OccupancyEndDate, decimal Total, List<BillingStatementChargeResponse> Charges);

    private sealed record BillingStatementChargeResponse(Guid Id, ChargeType ChargeType, string ChargeName, ChargeSettingMode SettingMode, bool IsMetered, decimal? Rate, decimal? PreviousReading, decimal? CurrentReading, decimal? ConsumptionUnits, decimal Amount);

    private sealed record PropertySummaryResponse(Guid PropertyId, Guid BillingCycleId, string PropertyName, string PropertyAddress, DateOnly BillingMonth, DateTimeOffset ApprovalDate, decimal ApprovedTotal, List<PropertyFloorSummaryResponse> Floors);

    private sealed record PropertyFloorSummaryResponse(string FloorName, decimal TotalBilled, List<BillingStatementUnitResponse> Bills);

    private sealed record ComparisonResponse(Guid PropertyId, string PropertyName, string PropertyAddress, DateOnly BillingMonth, Guid BillingCycleId, decimal CurrentApprovedTotal, decimal? PreviousApprovedTotal, decimal? TotalDifference, decimal? TotalPercentageChange, bool TotalPercentageChangeAvailable, DateOnly PreviousMonth, string PreviousPeriodStatus, bool IsComplete, string? IncompleteReason, ComparisonConsumptionResponse Electricity, List<ComparisonMetricResponse> Charges, List<string> CoverageNotes);

    private sealed record ComparisonConsumptionResponse(decimal? CurrentUnits, decimal? PreviousUnits, decimal? Difference, decimal? PercentageChange, bool PercentageChangeAvailable, int CurrentUnitCount, int PreviousUnitCount, bool CoverageChanged);

    private sealed record ComparisonMetricResponse(string Key, ChargeType? ChargeType, string Label, decimal? CurrentAmount, decimal? PreviousAmount, decimal? Difference, decimal? PercentageChange, bool PercentageChangeAvailable, int CurrentUnitCount, int PreviousUnitCount, bool CoverageChanged);
    private sealed record PropertyDataCounts(long BillingCycles, long BillingUnitBills, long BillingChargeLines, long CollectionReports);
}
