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
    private string? adminConnectionString;
    private bool created;

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

            created = true;
            await using var context = new PropertyManagementDbContext(
                new DbContextOptionsBuilder<PropertyManagementDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options);
            await context.Database.MigrateAsync();

            var applied = await context.Database.GetAppliedMigrationsAsync();
            var pending = await context.Database.GetPendingMigrationsAsync();
            if (applied.Count() != 3 || pending.Any())
            {
                throw new InvalidOperationException($"Expected three applied migrations and no pending migrations, found {applied.Count()} applied and {pending.Count()} pending.");
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

        EnsureGeneratedDatabaseTarget(ConnectionString);
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public Task DisposeAsync() => DropCreatedDatabaseAsync();

    private string BuildConnectionString(string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = database,
            Pooling = false,
        };
        EnsureGeneratedDatabaseTarget(builder.ConnectionString);
        return builder.ConnectionString;
    }

    private void EnsureGeneratedDatabaseTarget(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.Database, databaseName, StringComparison.Ordinal) || !IsSafeGeneratedName(builder.Database ?? string.Empty))
        {
            throw new InvalidOperationException("The effective PostgreSQL verification connection must target only the generated disposable database.");
        }
    }

    private async Task DropCreatedDatabaseAsync()
    {
        if (!created || string.IsNullOrWhiteSpace(adminConnectionString) || !IsSafeGeneratedName(databaseName))
        {
            return;
        }

        try
        {
            await using var admin = new NpgsqlConnection(adminConnectionString);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE {QuoteIdentifier(databaseName)} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
            created = false;
        }
        catch
        {
            // Do not attempt a broader cleanup target. The blocker is reported by the test run.
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
        Assert.Equal(3L, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM \"__EFMigrationsHistory\""));

        Assert.Equal((18, 4), await NumericDefinitionAsync(connection, "charge_configurations", "Amount"));
        Assert.Equal((18, 4), await NumericDefinitionAsync(connection, "billing_charge_lines", "RateSnapshot"));
        Assert.Equal((18, 4), await NumericDefinitionAsync(connection, "billing_charge_lines", "ConsumptionUnits"));
        Assert.Equal((18, 2), await NumericDefinitionAsync(connection, "billing_charge_lines", "Amount"));
        Assert.Equal(1L, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM pg_indexes WHERE tablename = 'billing_cycles' AND indexname = 'IX_billing_cycles_OrganizationId_PropertyId_BillingMonth' AND indexdef LIKE '%UNIQUE%'"));
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

        var managerInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"billing-manager-{suffix}@example.test", role = "Manager" }));
        using var manager = api.CreateClient();
        await AcceptInvitationAsync(manager, managerInvitation.InvitationToken, "Billing Manager");

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
        var invitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"reports-manager-{suffix}@example.test", role = "Manager" }));
        using var manager = api.CreateClient();
        await AcceptInvitationAsync(manager, invitation.InvitationToken, "Reports Manager");

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

    private static async Task AcceptInvitationAsync(HttpClient client, string token, string fullName)
    {
        var response = await client.PostAsJsonAsync("/api/auth/accept-invitation", new { token, fullName, password = "StrongPass!123" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task PutChargesAsync(HttpClient client, Guid propertyId, object[] items)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/properties/{propertyId}/charges", new { items })).StatusCode);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
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

    private sealed record BillingCycleResponse(Guid Id, Guid PropertyId, DateOnly BillingMonth, BillingCycleStatus Status, long Version, Guid LastChangedByUserId, DateTimeOffset LastChangedAtUtc, string? ReturnReason, decimal DraftTotal, decimal? ApprovedTotal, int BlockingBillCount, List<BillingUnitBillResponse> Bills, List<BillingAuditResponse> Audit);

    private sealed record BillingUnitBillResponse(Guid Id, Guid UnitId, string UnitName, Guid? OccupancyAssignmentId, Guid? TenantId, string? TenantName, string? TenantPhone, DateOnly? OccupancyStartDate, DateOnly? OccupancyEndDate, string? BlockingReason, decimal Total, bool ElectricityWarning, List<BillingChargeLineResponse> Charges);

    private sealed record BillingChargeLineResponse(Guid Id, ChargeType ChargeType, string ChargeName, ChargeSettingMode SettingMode, bool IsMetered, decimal? Rate, decimal? PreviousReading, decimal? CurrentReading, decimal? ConsumptionUnits, MeterReadingSource PreviousReadingSource, decimal Amount);

    private sealed record BillingAuditResponse(Guid Id, Guid ActorUserId, BillingAuditAction Action, DateTimeOffset OccurredAtUtc, string? Reason, string? Before, string? After);

    private sealed record BillingStatementResponse(string StatementType, Guid PropertyId, Guid BillingCycleId, string PropertyName, string PropertyAddress, DateOnly BillingMonth, DateTimeOffset ApprovalDate, decimal ApprovedTotal, Guid? TenantId, List<BillingStatementUnitResponse> Bills);

    private sealed record BillingStatementUnitResponse(string BillReference, Guid BillId, Guid UnitId, string UnitName, string? FloorName, Guid? OccupancyAssignmentId, Guid? TenantId, string? TenantName, string? TenantPhone, DateOnly? OccupancyStartDate, DateOnly? OccupancyEndDate, decimal Total, List<BillingStatementChargeResponse> Charges);

    private sealed record BillingStatementChargeResponse(Guid Id, ChargeType ChargeType, string ChargeName, ChargeSettingMode SettingMode, bool IsMetered, decimal? Rate, decimal? PreviousReading, decimal? CurrentReading, decimal? ConsumptionUnits, decimal Amount);

    private sealed record PropertySummaryResponse(Guid PropertyId, Guid BillingCycleId, string PropertyName, string PropertyAddress, DateOnly BillingMonth, DateTimeOffset ApprovalDate, decimal ApprovedTotal, List<PropertyFloorSummaryResponse> Floors);

    private sealed record PropertyFloorSummaryResponse(string FloorName, decimal TotalBilled, List<BillingStatementUnitResponse> Bills);

    private sealed record ComparisonResponse(Guid PropertyId, string PropertyName, string PropertyAddress, DateOnly BillingMonth, Guid BillingCycleId, decimal CurrentApprovedTotal, decimal? PreviousApprovedTotal, decimal? TotalDifference, decimal? TotalPercentageChange, bool TotalPercentageChangeAvailable, DateOnly PreviousMonth, string PreviousPeriodStatus, bool IsComplete, string? IncompleteReason, ComparisonConsumptionResponse Electricity, List<ComparisonMetricResponse> Charges, List<string> CoverageNotes);

    private sealed record ComparisonConsumptionResponse(decimal? CurrentUnits, decimal? PreviousUnits, decimal? Difference, decimal? PercentageChange, bool PercentageChangeAvailable, int CurrentUnitCount, int PreviousUnitCount, bool CoverageChanged);

    private sealed record ComparisonMetricResponse(string Key, ChargeType? ChargeType, string Label, decimal? CurrentAmount, decimal? PreviousAmount, decimal? Difference, decimal? PercentageChange, bool PercentageChangeAvailable, int CurrentUnitCount, int PreviousUnitCount, bool CoverageChanged);
}
