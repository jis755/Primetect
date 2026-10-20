using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrimeTect.Api.Contracts;
using PrimeTect.Domain.Enums;
using PrimeTect.Infrastructure.OpeningReadings;
using Xunit;

namespace PrimeTect.Api.Tests;

public sealed class OpeningReadingImportTests : IClassFixture<PrimeTectApiFactory>
{
    private const string HeaderLine = "template_version,property_id,property_name,unit_id,unit_name,meter_installation_id,meter_type,opening_reading,reading_date,first_billing_month";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PrimeTectApiFactory factory;

    public OpeningReadingImportTests(PrimeTectApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Admin_imports_a_legacy_baseline_and_billing_uses_it_as_the_opening_reading()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"Import {suffix}", ["Room A"]);
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);
        await AssignTenantAsync(admin, property.Id, [unit.Id]);

        var csrf = await CsrfAsync(admin);
        var templateBytes = await (await admin.GetAsync($"/api/properties/{property.Id}/meter-setup/opening-readings/template.csv")).Content.ReadAsByteArrayAsync();
        var template = OpeningReadingFileParser.Parse(templateBytes, "opening-readings.csv");
        Assert.Null(template.FatalError);
        var templateRow = Assert.Single(template.Rows);
        Assert.Equal(unit.Id.ToString(), templateRow.UnitId);
        Assert.Equal(string.Empty, templateRow.MeterInstallationId);
        Assert.Equal(string.Empty, templateRow.OpeningReading);

        var content = CsvBytes(property.Id, (unit.Id, null, "1200", "2026-02-05", "2026-02"));
        var preview = await ReadAsync<OpeningReadingPreviewResponse>(await PreviewAsync(admin, csrf, property.Id, "import.csv", content));
        Assert.True(preview.CanCommit);
        Assert.Equal(1, preview.ReadyCount);
        Assert.NotEmpty(preview.PreviewToken);
        Assert.True(preview.Rows.Single().UsesLegacyAdoption);

        // Preview writes no application records.
        var beforeCommit = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        Assert.Empty(beforeCommit.Installations);

        var submissionKey = Guid.NewGuid();
        var commit = await ReadAsync<OpeningReadingCommitResponse>(await CommitAsync(admin, csrf, property.Id, preview.PreviewToken, submissionKey));
        Assert.False(commit.AlreadyImported);
        Assert.Equal(1, commit.ImportedCount);

        var setup = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        var installation = Assert.Single(setup.Installations);
        Assert.Equal(1200m, installation.OpeningReading);
        Assert.Equal(new DateOnly(2026, 2, 5), installation.OpeningReadingDate);
        Assert.False(installation.IdentityKnown);
        Assert.Null(installation.SerialNumber);
        Assert.Contains(setup.Audit, entry => entry.Action == MeterInstallationAuditAction.OpeningBaseline || entry.Action == MeterInstallationAuditAction.Adopted);

        // The stored baseline is the opening for the first billing period, not an approved monthly reading.
        var cycle = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));
        var electricity = cycle.Bills.Single().Charges.Single();
        Assert.Equal(MeterReadingSource.OpeningEntry, electricity.PreviousReadingSource);
        Assert.Equal(1200m, electricity.PreviousReading);
    }

    [Fact]
    public async Task Import_reports_a_conflict_when_a_baseline_already_exists()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"Existing {suffix}", ["Room A"]);
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);
        var installation = await InstallSubmeterAsync(admin, property.Id, unit.Id, openingReading: 500m);

        var csrf = await CsrfAsync(admin);
        var content = CsvBytes(property.Id, (unit.Id, installation.Id, "900", "2026-02-05", "2026-02"));
        var preview = await ReadAsync<OpeningReadingPreviewResponse>(await PreviewAsync(admin, csrf, property.Id, "import.csv", content));

        Assert.False(preview.CanCommit);
        Assert.Equal(0, preview.ReadyCount);
        Assert.True(preview.ErrorCount + preview.ConflictCount >= 1);
        Assert.Contains(preview.Rows.Single().Errors, message => message.Contains("already has an opening baseline", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Import_is_blocked_when_a_billing_period_already_exists()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"Billed {suffix}", ["Room A"]);
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);
        await AssignTenantAsync(admin, property.Id, [unit.Id]);
        await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));

        var csrf = await CsrfAsync(admin);
        var content = CsvBytes(property.Id, (unit.Id, null, "100", "2026-02-05", "2026-02"));
        var preview = await ReadAsync<OpeningReadingPreviewResponse>(await PreviewAsync(admin, csrf, property.Id, "import.csv", content));

        Assert.False(preview.CanCommit);
        Assert.Contains(preview.Rows.Single().Errors, message => message.Contains("billing period already exists", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Rows_for_another_property_or_organization_are_rejected()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var propertyA = await CreatePropertyAsync(admin, $"Alpha {suffix}", ["Room A"]);
        var propertyB = await CreatePropertyAsync(admin, $"Beta {suffix}", ["Room B"]);
        var unitB = propertyB.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, propertyA.Id, 10m);

        var csrf = await CsrfAsync(admin);
        var content = CsvBytes(propertyB.Id, (unitB.Id, null, "100", "2026-02-05", "2026-02"));
        var preview = await ReadAsync<OpeningReadingPreviewResponse>(await PreviewAsync(admin, csrf, propertyA.Id, "import.csv", content));
        Assert.False(preview.CanCommit);
        Assert.Contains(preview.Rows.Single().Errors, message => message.Contains("different property", StringComparison.OrdinalIgnoreCase));

        // A different organization cannot see the property at all.
        using var otherAdmin = factory.CreateClient();
        await RegisterOwnerAsync(otherAdmin, Guid.NewGuid().ToString("N"));
        var otherCsrf = await CsrfAsync(otherAdmin);
        Assert.Equal(HttpStatusCode.NotFound, (await PreviewAsync(otherAdmin, otherCsrf, propertyA.Id, "import.csv", content)).StatusCode);
    }

    [Fact]
    public async Task Duplicate_unit_rows_are_rejected()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"Duplicate {suffix}", ["Room A"]);
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);

        var csrf = await CsrfAsync(admin);
        var content = CsvBytes(property.Id, (unit.Id, null, "100", "2026-02-05", "2026-02"), (unit.Id, null, "200", "2026-02-06", "2026-02"));
        var preview = await ReadAsync<OpeningReadingPreviewResponse>(await PreviewAsync(admin, csrf, property.Id, "import.csv", content));

        Assert.False(preview.CanCommit);
        Assert.All(preview.Rows, row => Assert.Contains(row.Errors, message => message.Contains("more than once", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Zero_is_imported_while_blank_rows_are_skipped_as_missing()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"Zero {suffix}", ["Room A", "Room B"]);
        var units = property.Floors.Single().Units;
        await SaveElectricityChargeAsync(admin, property.Id, 10m);

        var csrf = await CsrfAsync(admin);
        var content = CsvBytes(property.Id, (units[0].Id, null, "0", "2026-02-05", "2026-02"), (units[1].Id, null, string.Empty, string.Empty, "2026-02"));
        var preview = await ReadAsync<OpeningReadingPreviewResponse>(await PreviewAsync(admin, csrf, property.Id, "import.csv", content));

        Assert.True(preview.CanCommit);
        Assert.Equal(1, preview.ReadyCount);
        Assert.Equal(1, preview.SkippedCount);

        await ReadAsync<OpeningReadingCommitResponse>(await CommitAsync(admin, csrf, property.Id, preview.PreviewToken, Guid.NewGuid()));

        var setup = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        var installation = Assert.Single(setup.Installations);
        Assert.Equal(units[0].Id, installation.UnitId);
        Assert.Equal(0m, installation.OpeningReading);
        Assert.DoesNotContain(setup.Installations, item => item.UnitId == units[1].Id);
    }

    [Fact]
    public async Task Bangla_digits_in_an_uploaded_reading_are_normalized()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"Bangla {suffix}", ["রুম ক"]);
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);

        var csrf = await CsrfAsync(admin);
        var content = CsvBytes(property.Id, (unit.Id, null, "১২৩.৫", "২০২৬-০২-০৫", "২০২৬-০২"));
        var preview = await ReadAsync<OpeningReadingPreviewResponse>(await PreviewAsync(admin, csrf, property.Id, "import.csv", content));
        Assert.True(preview.CanCommit, string.Join(" | ", preview.Rows.Single().Errors));

        await ReadAsync<OpeningReadingCommitResponse>(await CommitAsync(admin, csrf, property.Id, preview.PreviewToken, Guid.NewGuid()));

        var setup = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        var installation = Assert.Single(setup.Installations);
        Assert.Equal(123.5m, installation.OpeningReading);
        Assert.Equal(new DateOnly(2026, 2, 5), installation.OpeningReadingDate);
    }

    [Fact]
    public async Task Missing_rate_stores_the_baseline_without_creating_zero_rate_billing()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"NoRate {suffix}", ["Room A"]);
        var unit = property.Floors.Single().Units.Single();
        await AssignTenantAsync(admin, property.Id, [unit.Id]);
        // No electricity charge is configured; only an existing submeter makes the unit eligible.
        var installation = await InstallSubmeterAsync(admin, property.Id, unit.Id, openingReading: null);

        var csrf = await CsrfAsync(admin);
        var content = CsvBytes(property.Id, (unit.Id, installation.Id, "100", "2026-02-05", "2026-02"));
        var preview = await ReadAsync<OpeningReadingPreviewResponse>(await PreviewAsync(admin, csrf, property.Id, "import.csv", content));

        Assert.True(preview.CanCommit, string.Join(" | ", preview.Rows.Single().Errors));
        Assert.Contains(preview.Rows.Single().Notes, note => note.Contains("No electricity rate", StringComparison.OrdinalIgnoreCase));

        await ReadAsync<OpeningReadingCommitResponse>(await CommitAsync(admin, csrf, property.Id, preview.PreviewToken, Guid.NewGuid()));

        var cycle = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));
        Assert.DoesNotContain(cycle.Bills.SelectMany(bill => bill.Charges), charge => charge.ChargeType == ChargeType.Electricity);
    }

    [Fact]
    public async Task Staff_manager_finance_and_recovery_sessions_cannot_access_the_import()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var adminPassword = "StrongPass!123";
        await RegisterOwnerAsync(admin, suffix, adminPassword);
        var property = await CreatePropertyAsync(admin, $"Denied {suffix}", ["Room A"]);
        var csrf = await CsrfAsync(admin);

        foreach (var role in new[] { "Manager", "FinanceManager", "BuildingStaff" })
        {
            var (client, _, _, _) = await InviteAndAcceptAsync(admin, role, suffix, "StrongPass!456");
            using (client)
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/properties/{property.Id}/meter-setup/opening-readings/template.csv")).StatusCode);
                var denied = await PreviewAsync(client, csrf, property.Id, "import.csv", CsvBytes(property.Id));
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            }
        }

        // A restricted password-recovery session must also be denied.
        var (recoveryStaff, staffId, staffName, staffEmail) = await InviteAndAcceptAsync(admin, "BuildingStaff", $"{suffix}r", "StrongPass!456");
        recoveryStaff.Dispose();
        var temporary = await ResetAndReadTemporaryAsync(admin, staffId, staffName, adminPassword);
        using var recovery = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await recovery.PostAsJsonAsync("/api/auth/login", new { email = staffEmail, password = temporary })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recovery.GetAsync($"/api/properties/{property.Id}/meter-setup/opening-readings/template.csv")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PreviewAsync(recovery, csrf, property.Id, "import.csv", CsvBytes(property.Id))).StatusCode);
    }

    [Fact]
    public async Task Retrying_the_same_submission_is_idempotent()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"Idempotent {suffix}", ["Room A"]);
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);

        var csrf = await CsrfAsync(admin);
        var content = CsvBytes(property.Id, (unit.Id, null, "300", "2026-02-05", "2026-02"));
        var preview = await ReadAsync<OpeningReadingPreviewResponse>(await PreviewAsync(admin, csrf, property.Id, "import.csv", content));
        var submissionKey = Guid.NewGuid();

        var first = await ReadAsync<OpeningReadingCommitResponse>(await CommitAsync(admin, csrf, property.Id, preview.PreviewToken, submissionKey));
        Assert.False(first.AlreadyImported);
        Assert.Equal(1, first.ImportedCount);

        var second = await ReadAsync<OpeningReadingCommitResponse>(await CommitAsync(admin, csrf, property.Id, preview.PreviewToken, submissionKey));
        Assert.True(second.AlreadyImported);
        Assert.Equal(1, second.ImportedCount);

        var setup = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        Assert.Single(setup.Installations);
        Assert.Equal(300m, setup.Installations.Single().OpeningReading);
    }

    [Fact]
    public async Task Persistence_failure_rolls_back_the_whole_import_and_a_retry_succeeds()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"Rollback {suffix}", ["Room A", "Room B"]);
        var units = property.Floors.Single().Units;
        await SaveElectricityChargeAsync(admin, property.Id, 10m);

        var csrf = await CsrfAsync(admin);
        var content = CsvBytes(property.Id, (units[0].Id, null, "10", "2026-02-05", "2026-02"), (units[1].Id, null, "20", "2026-02-05", "2026-02"));
        var preview = await ReadAsync<OpeningReadingPreviewResponse>(await PreviewAsync(admin, csrf, property.Id, "import.csv", content));
        Assert.True(preview.CanCommit);

        factory.FailNextSave = true;
        var failed = await CommitAsync(admin, csrf, property.Id, preview.PreviewToken, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);

        var afterFailure = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        Assert.Empty(afterFailure.Installations);

        var retry = await ReadAsync<OpeningReadingCommitResponse>(await CommitAsync(admin, csrf, property.Id, preview.PreviewToken, Guid.NewGuid()));
        Assert.Equal(2, retry.ImportedCount);

        var setup = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        Assert.Equal(2, setup.Installations.Count);
    }

    [Fact]
    public async Task Stale_preview_after_a_new_billing_period_or_replacement_is_rejected()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"Stale {suffix}", ["Room A"]);
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);
        await AssignTenantAsync(admin, property.Id, [unit.Id]);

        var csrf = await CsrfAsync(admin);
        var content = CsvBytes(property.Id, (unit.Id, null, "100", "2026-02-05", "2026-02"));
        var preview = await ReadAsync<OpeningReadingPreviewResponse>(await PreviewAsync(admin, csrf, property.Id, "import.csv", content));
        Assert.True(preview.CanCommit);

        // A reading for the same period is generated after the preview.
        await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));

        var stale = await CommitAsync(admin, csrf, property.Id, preview.PreviewToken, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var setup = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        Assert.Empty(setup.Installations);
    }

    [Fact]
    public async Task Opening_reading_dated_on_the_preceding_period_boundary_starts_the_first_billing_month()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"Boundary {suffix}", ["Room A"]);
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);
        await AssignTenantAsync(admin, property.Id, [unit.Id]);

        var csrf = await CsrfAsync(admin);
        // A 30 September reading is the baseline that starts the October-issued bill, exactly like a manual opening entry.
        var content = CsvBytes(property.Id, (unit.Id, null, "10020", "2026-09-30", "2026-10"));
        var preview = await ReadAsync<OpeningReadingPreviewResponse>(await PreviewAsync(admin, csrf, property.Id, "import.csv", content));
        Assert.True(preview.CanCommit, string.Join(" | ", preview.Rows.Single().Errors));
        Assert.Equal(1, preview.ReadyCount);

        await ReadAsync<OpeningReadingCommitResponse>(await CommitAsync(admin, csrf, property.Id, preview.PreviewToken, Guid.NewGuid()));

        var setup = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        var installation = Assert.Single(setup.Installations);
        Assert.Equal(10020m, installation.OpeningReading);
        Assert.Equal(new DateOnly(2026, 9, 30), installation.OpeningReadingDate);

        var cycle = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-10-01" }));
        var electricity = cycle.Bills.Single().Charges.Single();
        Assert.Equal(MeterReadingSource.OpeningEntry, electricity.PreviousReadingSource);
        Assert.Equal(10020m, electricity.PreviousReading);
        Assert.Equal(new DateOnly(2026, 9, 30), electricity.PreviousReadingDate);
    }

    [Fact]
    public async Task Opening_reading_dated_in_the_month_of_the_first_billing_month_is_accepted()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"FirstOfMonth {suffix}", ["Room A"]);
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);

        var csrf = await CsrfAsync(admin);
        // A 1 January reading starts the February bill (the documented meter-reading convention).
        var content = CsvBytes(property.Id, (unit.Id, null, "10020", "2026-01-01", "2026-02"));
        var preview = await ReadAsync<OpeningReadingPreviewResponse>(await PreviewAsync(admin, csrf, property.Id, "import.csv", content));
        Assert.True(preview.CanCommit, string.Join(" | ", preview.Rows.Single().Errors));
        Assert.Equal(1, preview.ReadyCount);
    }

    [Fact]
    public async Task Opening_reading_dated_after_the_first_billing_month_is_rejected_as_a_missing_period()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"After {suffix}", ["Room A"]);
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);

        var csrf = await CsrfAsync(admin);
        // A 1 March reading for a February first billing month would span the whole missing February period.
        var content = CsvBytes(property.Id, (unit.Id, null, "10020", "2026-03-01", "2026-02"));
        var preview = await ReadAsync<OpeningReadingPreviewResponse>(await PreviewAsync(admin, csrf, property.Id, "import.csv", content));
        Assert.False(preview.CanCommit);
        Assert.Contains(preview.Rows.Single().Errors, message => message.Contains("immediately before", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<HttpResponseMessage> PreviewAsync(HttpClient client, string csrf, Guid propertyId, string fileName, byte[] content)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/properties/{propertyId}/meter-setup/opening-readings/preview") { Content = form };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> CommitAsync(HttpClient client, string csrf, Guid propertyId, string previewToken, Guid submissionKey)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(previewToken), "previewToken");
        form.Add(new StringContent(submissionKey.ToString()), "submissionKey");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/properties/{propertyId}/meter-setup/opening-readings/commit") { Content = form };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    private static byte[] CsvBytes(Guid propertyId, params (Guid UnitId, Guid? InstallationId, string Reading, string Date, string Month)[] rows)
    {
        var builder = new StringBuilder();
        builder.Append(HeaderLine).Append("\r\n");
        foreach (var row in rows)
        {
            builder.Append("1,").Append(propertyId).Append(",Property,").Append(row.UnitId).Append(',').Append(row.UnitId).Append(',')
                .Append(row.InstallationId?.ToString() ?? string.Empty).Append(",Electricity,")
                .Append(row.Reading).Append(',').Append(row.Date).Append(',').Append(row.Month).Append("\r\n");
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
    }

    private static async Task<string> CsrfAsync(HttpClient client) => (await ReadAsync<CsrfResponse>(await client.GetAsync("/api/auth/csrf"))).Token;

    private static async Task RegisterOwnerAsync(HttpClient client, string suffix, string password = "StrongPass!123") =>
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/register-owner", new
        {
            organizationName = $"Import Org {suffix}",
            fullName = "Import Admin",
            email = $"import-admin-{suffix}@example.test",
            password,
        })).StatusCode);

    private static async Task<PropertyResponse> CreatePropertyAsync(HttpClient client, string name, IReadOnlyList<string> unitNames)
    {
        var response = await client.PostAsJsonAsync("/api/properties", new
        {
            name,
            address = $"{name} address",
            propertyType = "ApartmentBuilding",
            floors = new[] { new { name = "Ground", unitCount = unitNames.Count, unitNames } },
        });
        return await ReadAsync<PropertyResponse>(response);
    }

    private static async Task SaveElectricityChargeAsync(HttpClient client, Guid propertyId, decimal rate)
    {
        var response = await client.PutAsJsonAsync($"/api/properties/{propertyId}/charges", new
        {
            items = new[] { new { chargeType = "Electricity", mode = "Custom", billingMonth = "2025-12-01", amount = (decimal?)rate, customLabel = (string?)null } },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task AssignTenantAsync(HttpClient client, Guid propertyId, IReadOnlyList<Guid> unitIds)
    {
        var tenant = await ReadAsync<CreatedTenant>(await client.PostAsJsonAsync("/api/tenants", new { name = "Import Tenant", phone = (string?)null }));
        foreach (var unitId in unitIds)
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/properties/{propertyId}/units/{unitId}/occupancy", new { tenantId = tenant.Id, startDate = "2025-12-01", endDate = (string?)null })).StatusCode);
        }
    }

    private static async Task<MeterInstallationResponse> InstallSubmeterAsync(HttpClient client, Guid propertyId, Guid unitId, decimal? openingReading)
    {
        var response = await client.PostAsJsonAsync($"/api/properties/{propertyId}/meter-installations", new
        {
            unitId,
            mainMeterId = (Guid?)null,
            serialNumber = "SUB",
            displayLabel = (string?)null,
            installationDate = "2025-12-01",
            openingReading,
            openingReadingDate = openingReading is null ? (string?)null : "2025-12-01",
            identityKnown = true,
            reason = "Adopted existing submeter",
        });
        return await ReadAsync<MeterInstallationResponse>(response);
    }

    private async Task<(HttpClient Client, Guid UserId, string FullName, string Email)> InviteAndAcceptAsync(HttpClient admin, string role, string suffix, string password)
    {
        var email = $"{role.ToLowerInvariant()}-{suffix}@example.test";
        using var invitation = JsonDocument.Parse(await (await admin.PostAsJsonAsync("/api/staff/invitations", new { email, role })).Content.ReadAsStringAsync());
        var token = invitation.RootElement.GetProperty("invitationToken").GetString();
        var fullName = $"{role} {suffix}";
        var client = factory.CreateClient();
        var accepted = await client.PostAsJsonAsync("/api/auth/accept-invitation", new { token, fullName, password });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var acceptedJson = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
        return (client, acceptedJson.RootElement.GetProperty("id").GetGuid(), fullName, email);
    }

    private static async Task<string> ResetAndReadTemporaryAsync(HttpClient admin, Guid staffId, string staffName, string adminPassword)
    {
        var reset = await admin.PostAsJsonAsync($"/api/staff/{staffId}/reset-password", new { actingPassword = adminPassword, confirmFullName = staffName, reason = "Reset for test", submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        using var json = JsonDocument.Parse(await reset.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("temporaryPassword").GetString()!;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private sealed record CsrfResponse(string Token);

    private sealed record CreatedTenant(Guid Id);
}
