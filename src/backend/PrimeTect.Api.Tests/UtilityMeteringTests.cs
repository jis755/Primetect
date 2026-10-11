using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrimeTect.Domain.Enums;
using Xunit;

namespace PrimeTect.Api.Tests;

public sealed class UtilityMeteringTests : IClassFixture<PrimeTectApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PrimeTectApiFactory factory;

    public UtilityMeteringTests(PrimeTectApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Utility_account_and_one_main_meter_can_supply_many_rooms_with_text_identifiers()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"Many Rooms {suffix}", Enumerable.Range(1, 8).Select(index => $"R{index}").ToArray());
        var units = property.Floors.Single().Units;

        var account = await ReadAsync<UtilityAccountResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/utility-accounts", new
        {
            providerName = "Dhaka Electric Supply",
            customerNumber = "0012345",
            displayLabel = (string?)null,
        }));
        Assert.Equal("0012345", account.CustomerNumber);

        var mainMeter = await ReadAsync<UtilityMainMeterResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/utility-main-meters", new
        {
            utilityAccountId = account.Id,
            meterNumber = "00998877",
            displayLabel = "Roof main",
            installedOn = "2025-01-01",
        }));
        Assert.Equal("00998877", mainMeter.MeterNumber);
        Assert.True(mainMeter.IsActive);

        foreach (var unit in units)
        {
            var installed = await admin.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations", new
            {
                unitId = unit.Id,
                mainMeterId = mainMeter.Id,
                serialNumber = $"SUB-{unit.Name}",
                displayLabel = (string?)null,
                installationDate = "2025-01-01",
                openingReading = (decimal?)0m,
                openingReadingDate = "2025-01-01",
                identityKnown = true,
                reason = "Adopted the existing room submeter",
            });
            Assert.Equal(HttpStatusCode.Created, installed.StatusCode);
        }

        var setup = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        Assert.Single(setup.Accounts);
        Assert.Single(setup.MainMeters);
        Assert.Equal(8, setup.Installations.Count);
        Assert.All(setup.Installations, installation => Assert.Equal(mainMeter.Id, installation.MainMeterId));
        Assert.All(setup.Installations, installation => Assert.True(installation.IsActive));
    }

    [Fact]
    public async Task Legacy_units_without_installations_keep_unknown_meter_identity()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"Legacy {suffix}", ["Only"]);
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);
        await AssignTenantAsync(admin, property.Id, [unit.Id]);

        var draft = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        var electricity = draft.Bills.Single().Charges.Single();
        Assert.Null(electricity.MeterInstallationId);
        Assert.True(electricity.Segments is null || electricity.Segments.Count == 0);

        var setup = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        Assert.Empty(setup.Installations);

        var overview = await ReadAsync<MeterOverviewResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-overview/2026-01"));
        Assert.Null(overview.Units.Single().SubmeterLabel);
        Assert.Null(overview.Units.Single().MeterInstallationId);
    }

    [Fact]
    public async Task Initial_baseline_and_ordinary_carry_forward_use_the_installation_opening_reading()
    {
        using var admin = factory.CreateClient();
        var (approver, property) = await SetupOrganizationWithApproverAsync(admin, "Carry Forward");
        using var approverClient = approver;
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);
        await AssignTenantAsync(admin, property.Id, [unit.Id]);
        var installation = await InstallSubmeterAsync(admin, property.Id, unit.Id, openingReading: 10000m, installationDate: "2025-12-01");

        var january = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        var januaryElectricity = january.Bills.Single().Charges.Single();
        Assert.Equal(MeterReadingSource.OpeningEntry, januaryElectricity.PreviousReadingSource);
        Assert.Equal(10000m, januaryElectricity.PreviousReading);
        Assert.Equal(installation.Id, januaryElectricity.MeterInstallationId);

        var janSaved = await SaveReadingAsync(admin, property.Id, january, january.Bills.Single().Id, 10020m);
        Assert.Equal(20m, janSaved.Bills.Single().Charges.Single().ConsumptionUnits);
        await ApproveAsync(admin, approverClient, property.Id, janSaved);

        var february = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));
        var februaryElectricity = february.Bills.Single().Charges.Single();
        Assert.Equal(MeterReadingSource.ApprovedPeriod, februaryElectricity.PreviousReadingSource);
        Assert.Equal(10020m, februaryElectricity.PreviousReading);
        Assert.Equal(installation.Id, februaryElectricity.MeterInstallationId);

        var febSaved = await SaveReadingAsync(admin, property.Id, february, february.Bills.Single().Id, 10040m);
        Assert.Equal(20m, febSaved.Bills.Single().Charges.Single().ConsumptionUnits);
        Assert.Equal(200m, febSaved.Bills.Single().Charges.Single().Amount);
    }

    [Fact]
    public async Task Replacement_segments_sum_independently_without_cross_meter_subtraction()
    {
        using var admin = factory.CreateClient();
        var (approver, property) = await SetupOrganizationWithApproverAsync(admin, "Two Segment");
        using var approverClient = approver;
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);
        await AssignTenantAsync(admin, property.Id, [unit.Id]);
        var installation = await InstallSubmeterAsync(admin, property.Id, unit.Id, openingReading: 10000m, installationDate: "2025-12-01");

        var january = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        await ApproveAsync(admin, approverClient, property.Id, await SaveReadingAsync(admin, property.Id, january, january.Bills.Single().Id, 10020m));

        var replacement = await ReadAsync<MeterReplacementResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations/{installation.Id}/replace", new
        {
            expectedInstallationVersion = installation.Version,
            replacementOccurredAtUtc = "2026-02-10T03:00:00+00:00",
            reason = "Display failed, meter swapped",
            oldFinalReading = (decimal?)10040m,
            oldFinalReadingUnavailable = false,
            newSerialNumber = "NEW-1",
            newDisplayLabel = "Replacement A",
            newOpeningReading = 35m,
            newMainMeterId = (Guid?)null,
            submissionKey = Guid.NewGuid(),
        }));
        Assert.Equal("2026-02-10", replacement.PropertyLocalDate);
        Assert.Equal(35m, replacement.NewInstallation.OpeningReading);

        var february = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));
        var draftElectricity = february.Bills.Single().Charges.Single();
        Assert.Equal(35m, draftElectricity.PreviousReading);
        Assert.Equal(MeterReadingSource.OpeningEntry, draftElectricity.PreviousReadingSource);
        Assert.Equal(2, draftElectricity.Segments!.Count);
        Assert.Equal(20m, draftElectricity.Segments!.Single(segment => segment.SortOrder == 0).ConsumptionUnits);
        Assert.Null(draftElectricity.Segments!.Single(segment => segment.SortOrder == 1).ConsumptionUnits);
        Assert.NotNull(february.Bills.Single().BlockingReason);

        var febSaved = await SaveReadingAsync(admin, property.Id, february, february.Bills.Single().Id, 50m);
        var savedElectricity = febSaved.Bills.Single().Charges.Single();
        Assert.Equal(35m, savedElectricity.ConsumptionUnits);
        Assert.Equal(350m, savedElectricity.Amount);
        Assert.Equal(20m, savedElectricity.Segments!.Single(segment => segment.SortOrder == 0).ConsumptionUnits);
        Assert.Equal(15m, savedElectricity.Segments!.Single(segment => segment.SortOrder == 1).ConsumptionUnits);

        await ApproveAsync(admin, approverClient, property.Id, febSaved);
        var approvedFebruary = await ReadAsync<BillingCycleResponse>(await admin.GetAsync($"/api/properties/{property.Id}/billing-cycles/2026-02"));
        Assert.Equal(BillingCycleStatus.Approved, approvedFebruary.Status);
        Assert.Equal(35m, approvedFebruary.Bills.Single().Charges.Single().ConsumptionUnits);
    }

    [Fact]
    public async Task Replacement_may_start_at_zero_and_zero_is_not_missing()
    {
        using var admin = factory.CreateClient();
        var (approver, property) = await SetupOrganizationWithApproverAsync(admin, "Zero Start");
        using var approverClient = approver;
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);
        await AssignTenantAsync(admin, property.Id, [unit.Id]);
        var installation = await InstallSubmeterAsync(admin, property.Id, unit.Id, openingReading: 10000m, installationDate: "2025-12-01");
        var january = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        await ApproveAsync(admin, approverClient, property.Id, await SaveReadingAsync(admin, property.Id, january, january.Bills.Single().Id, 10020m));

        var replacement = await ReadAsync<MeterReplacementResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations/{installation.Id}/replace", new
        {
            expectedInstallationVersion = installation.Version,
            replacementOccurredAtUtc = "2026-02-10T03:00:00+00:00",
            reason = "Brand new meter",
            oldFinalReading = (decimal?)10040m,
            oldFinalReadingUnavailable = false,
            newSerialNumber = (string?)null,
            newDisplayLabel = (string?)null,
            newOpeningReading = 0m,
            newMainMeterId = (Guid?)null,
            submissionKey = Guid.NewGuid(),
        }));
        Assert.Equal(0m, replacement.NewInstallation.OpeningReading);

        var february = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));
        var saved = await SaveReadingAsync(admin, property.Id, february, february.Bills.Single().Id, 15m);
        var electricity = saved.Bills.Single().Charges.Single();
        Assert.Equal(35m, electricity.ConsumptionUnits);
        Assert.Equal(0m, electricity.Segments!.Single(segment => segment.SortOrder == 1).PreviousReading);
        Assert.Equal(15m, electricity.Segments!.Single(segment => segment.SortOrder == 1).ConsumptionUnits);
    }

    [Fact]
    public async Task Multiple_replacements_keep_ordered_history_without_double_counting()
    {
        using var admin = factory.CreateClient();
        var (approver, property) = await SetupOrganizationWithApproverAsync(admin, "Repeat Replace");
        using var approverClient = approver;
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);
        await AssignTenantAsync(admin, property.Id, [unit.Id]);
        var first = await InstallSubmeterAsync(admin, property.Id, unit.Id, openingReading: 10000m, installationDate: "2025-12-01");
        var january = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        await ApproveAsync(admin, approverClient, property.Id, await SaveReadingAsync(admin, property.Id, january, january.Bills.Single().Id, 10020m));

        var second = await ReadAsync<MeterReplacementResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations/{first.Id}/replace", new
        {
            expectedInstallationVersion = first.Version,
            replacementOccurredAtUtc = "2026-02-05T03:00:00+00:00",
            reason = "First swap",
            oldFinalReading = (decimal?)10040m,
            oldFinalReadingUnavailable = false,
            newSerialNumber = "MID",
            newDisplayLabel = (string?)null,
            newOpeningReading = 0m,
            newMainMeterId = (Guid?)null,
            submissionKey = Guid.NewGuid(),
        }));

        var third = await ReadAsync<MeterReplacementResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations/{second.NewInstallation.Id}/replace", new
        {
            expectedInstallationVersion = second.NewInstallation.Version,
            replacementOccurredAtUtc = "2026-02-20T03:00:00+00:00",
            reason = "Second swap",
            oldFinalReading = (decimal?)10m,
            oldFinalReadingUnavailable = false,
            newSerialNumber = "FINAL",
            newDisplayLabel = (string?)null,
            newOpeningReading = 100m,
            newMainMeterId = (Guid?)null,
            submissionKey = Guid.NewGuid(),
        }));

        var february = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));
        var saved = await SaveReadingAsync(admin, property.Id, february, february.Bills.Single().Id, 130m);
        var electricity = saved.Bills.Single().Charges.Single();
        Assert.Equal(3, electricity.Segments!.Count);
        Assert.Equal(20m, electricity.Segments!.Single(segment => segment.SortOrder == 0).ConsumptionUnits);
        Assert.Equal(10m, electricity.Segments!.Single(segment => segment.SortOrder == 1).ConsumptionUnits);
        Assert.Equal(30m, electricity.Segments!.Single(segment => segment.SortOrder == 2).ConsumptionUnits);
        Assert.Equal(60m, electricity.ConsumptionUnits);

        var setup = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        var chain = setup.Installations.Single(item => item.Id == third.NewInstallation.Id);
        Assert.Equal(second.NewInstallation.Id, chain.ReplacedInstallationId);
        Assert.Single(setup.Installations, item => item.IsActive);
    }

    [Fact]
    public async Task Missing_old_final_reading_blocks_the_period_but_preserves_the_replacement()
    {
        using var admin = factory.CreateClient();
        var (approver, property) = await SetupOrganizationWithApproverAsync(admin, "Missing Final");
        using var approverClient = approver;
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);
        await AssignTenantAsync(admin, property.Id, [unit.Id]);
        var first = await InstallSubmeterAsync(admin, property.Id, unit.Id, openingReading: 10000m, installationDate: "2025-12-01");
        var january = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        await ApproveAsync(admin, approverClient, property.Id, await SaveReadingAsync(admin, property.Id, january, january.Bills.Single().Id, 10020m));

        var replacement = await ReadAsync<MeterReplacementResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations/{first.Id}/replace", new
        {
            expectedInstallationVersion = first.Version,
            replacementOccurredAtUtc = "2026-02-10T03:00:00+00:00",
            reason = "Old final not readable",
            oldFinalReading = (decimal?)null,
            oldFinalReadingUnavailable = true,
            newSerialNumber = "NEW",
            newDisplayLabel = (string?)null,
            newOpeningReading = 35m,
            newMainMeterId = (Guid?)null,
            submissionKey = Guid.NewGuid(),
        }));
        Assert.Equal(35m, replacement.NewInstallation.OpeningReading);

        var february = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));
        Assert.Contains("final reading is unavailable", february.Bills.Single().BlockingReason);
        Assert.Null(february.Bills.Single().Charges.Single().ConsumptionUnits);

        var saved = await SaveReadingAsync(admin, property.Id, february, february.Bills.Single().Id, 50m);
        Assert.Contains("final reading is unavailable", saved.Bills.Single().BlockingReason);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{saved.Id}/submit", new { expectedVersion = saved.Version })).StatusCode);

        var setup = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        var ended = setup.Installations.Single(item => item.Id == first.Id);
        Assert.True(ended.FinalReadingUnavailable);
        Assert.Equal("2026-02-10", ended.EndedOn!.Value.ToString("yyyy-MM-dd"));
        Assert.Single(setup.Installations, item => item.IsActive);
    }

    [Fact]
    public async Task Invalid_chronology_approved_history_and_submitted_months_are_rejected()
    {
        using var admin = factory.CreateClient();
        var (approver, property) = await SetupOrganizationWithApproverAsync(admin, "Guardrails");
        using var approverClient = approver;
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);
        await AssignTenantAsync(admin, property.Id, [unit.Id]);
        var first = await InstallSubmeterAsync(admin, property.Id, unit.Id, openingReading: 10000m, installationDate: "2026-01-05");
        var january = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        await ApproveAsync(admin, approverClient, property.Id, await SaveReadingAsync(admin, property.Id, january, january.Bills.Single().Id, 10020m));

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations/{first.Id}/replace", new
        {
            expectedInstallationVersion = first.Version,
            replacementOccurredAtUtc = "2026-01-20T03:00:00+00:00",
            reason = "Backdated",
            oldFinalReading = (decimal?)10020m,
            oldFinalReadingUnavailable = false,
            newOpeningReading = 0m,
            submissionKey = Guid.NewGuid(),
        })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations/{first.Id}/replace", new
        {
            expectedInstallationVersion = first.Version,
            replacementOccurredAtUtc = "2026-01-01T03:00:00+00:00",
            reason = "Before install",
            oldFinalReading = (decimal?)10020m,
            oldFinalReadingUnavailable = false,
            newOpeningReading = 0m,
            submissionKey = Guid.NewGuid(),
        })).StatusCode);

        var february = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));
        var febSaved = await SaveReadingAsync(admin, property.Id, february, february.Bills.Single().Id, 10040m);
        var submitted = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{febSaved.Id}/submit", new { expectedVersion = febSaved.Version }));
        Assert.Equal(BillingCycleStatus.Submitted, submitted.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations/{first.Id}/replace", new
        {
            expectedInstallationVersion = first.Version,
            replacementOccurredAtUtc = "2026-02-25T03:00:00+00:00",
            reason = "During review",
            oldFinalReading = (decimal?)10040m,
            oldFinalReadingUnavailable = false,
            newOpeningReading = 0m,
            submissionKey = Guid.NewGuid(),
        })).StatusCode);
    }

    [Fact]
    public async Task Replacement_is_concurrency_guarded_idempotent_and_atomic()
    {
        using var admin = factory.CreateClient();
        await SetupOrganizationWithApproverAsync(admin, "Idempotent");
        var property = await ReadLatestPropertyAsync(admin);
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);
        await AssignTenantAsync(admin, property.Id, [unit.Id]);
        var first = await InstallSubmeterAsync(admin, property.Id, unit.Id, openingReading: 100m, installationDate: "2025-12-01");

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations/{first.Id}/replace", new
        {
            expectedInstallationVersion = first.Version + 5,
            replacementOccurredAtUtc = "2026-02-10T03:00:00+00:00",
            reason = "Swap",
            oldFinalReading = (decimal?)120m,
            oldFinalReadingUnavailable = false,
            newSerialNumber = "S1",
            newDisplayLabel = (string?)null,
            newOpeningReading = 5m,
            newMainMeterId = (Guid?)null,
            submissionKey = Guid.NewGuid(),
        })).StatusCode);

        var submissionKey = Guid.NewGuid();
        var applied = await ReadAsync<MeterReplacementResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations/{first.Id}/replace", new
        {
            expectedInstallationVersion = first.Version,
            replacementOccurredAtUtc = "2026-02-10T03:00:00+00:00",
            reason = "Swap",
            oldFinalReading = (decimal?)120m,
            oldFinalReadingUnavailable = false,
            newSerialNumber = "S1",
            newDisplayLabel = (string?)null,
            newOpeningReading = 5m,
            newMainMeterId = (Guid?)null,
            submissionKey,
        }));
        var repeated = await ReadAsync<MeterReplacementResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations/{first.Id}/replace", new
        {
            expectedInstallationVersion = first.Version,
            replacementOccurredAtUtc = "2026-02-10T03:00:00+00:00",
            reason = "Swap",
            oldFinalReading = (decimal?)120m,
            oldFinalReadingUnavailable = false,
            newSerialNumber = "S1",
            newDisplayLabel = (string?)null,
            newOpeningReading = 5m,
            newMainMeterId = (Guid?)null,
            submissionKey,
        }));
        Assert.Equal(applied.NewInstallation.Id, repeated.NewInstallation.Id);

        var setup = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        Assert.Equal(2, setup.Installations.Count);
        Assert.Single(setup.Audit, audit => audit.Action == MeterInstallationAuditAction.Replaced && audit.PreviousInstallationId == first.Id);

        factory.FailNextSave = true;
        var failed = await admin.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations/{applied.NewInstallation.Id}/replace", new
        {
            expectedInstallationVersion = applied.NewInstallation.Version,
            replacementOccurredAtUtc = "2026-03-10T03:00:00+00:00",
            reason = "Should roll back",
            oldFinalReading = (decimal?)9m,
            oldFinalReadingUnavailable = false,
            newOpeningReading = 1m,
            submissionKey = Guid.NewGuid(),
        });
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        var afterFailure = await ReadAsync<MeterSetupResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-setup"));
        Assert.Equal(2, afterFailure.Installations.Count);
        Assert.Single(afterFailure.Installations, item => item.IsActive);
    }

    [Fact]
    public async Task Replacement_requires_admin_and_respects_organization_isolation()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, suffix);
        var property = await CreatePropertyAsync(admin, $"Isolation {suffix}", ["Only"]);
        var unit = property.Floors.Single().Units.Single();

        var managerInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"metering-manager-{suffix}@example.test", role = "Manager" }));
        using var manager = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsJsonAsync("/api/auth/accept-invitation", new { token = managerInvitation.InvitationToken, fullName = "Metering Manager", password = "StrongPass!123" })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync($"/api/properties/{property.Id}/utility-accounts", new { providerName = "X", customerNumber = "1", displayLabel = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync($"/api/properties/{property.Id}/meter-setup")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations", new { unitId = unit.Id, openingReading = (decimal?)0m })).StatusCode);

        using var otherOrganization = factory.CreateClient();
        await RegisterOwnerAsync(otherOrganization, $"other-{suffix}");
        Assert.Equal(HttpStatusCode.NotFound, (await otherOrganization.GetAsync($"/api/properties/{property.Id}/meter-setup")).StatusCode);
    }

    [Fact]
    public async Task Approved_statements_keep_their_original_readings_after_a_later_replacement()
    {
        using var admin = factory.CreateClient();
        var (approver, property) = await SetupOrganizationWithApproverAsync(admin, "History");
        using var approverClient = approver;
        var unit = property.Floors.Single().Units.Single();
        await SaveElectricityChargeAsync(admin, property.Id, 10m);
        await AssignTenantAsync(admin, property.Id, [unit.Id]);
        var first = await InstallSubmeterAsync(admin, property.Id, unit.Id, openingReading: 10000m, installationDate: "2025-12-01");
        var january = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        await ApproveAsync(admin, approverClient, property.Id, await SaveReadingAsync(admin, property.Id, january, january.Bills.Single().Id, 10020m));

        var januaryBefore = await ReadAsync<BillingStatementResponse>(await admin.GetAsync($"/api/properties/{property.Id}/billing-reports/2026-01/units/{unit.Id}"));
        var beforeCharge = januaryBefore.Bills.Single().Charges.Single();
        Assert.Equal(10020m, beforeCharge.CurrentReading);
        Assert.Equal(20m, beforeCharge.ConsumptionUnits);

        await ReadAsync<MeterReplacementResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/meter-installations/{first.Id}/replace", new
        {
            expectedInstallationVersion = first.Version,
            replacementOccurredAtUtc = "2026-02-10T03:00:00+00:00",
            reason = "Swap",
            oldFinalReading = (decimal?)10040m,
            oldFinalReadingUnavailable = false,
            newOpeningReading = 35m,
            submissionKey = Guid.NewGuid(),
        }));

        var januaryAfter = await ReadAsync<BillingStatementResponse>(await admin.GetAsync($"/api/properties/{property.Id}/billing-reports/2026-01/units/{unit.Id}"));
        var afterCharge = januaryAfter.Bills.Single().Charges.Single();
        Assert.Equal(10020m, afterCharge.CurrentReading);
        Assert.Equal(20m, afterCharge.ConsumptionUnits);
        Assert.Equal(beforeCharge.Amount, afterCharge.Amount);
    }

    // ----- helpers -----

    private async Task<(HttpClient Approver, PropertyResponse Property)> SetupOrganizationWithApproverAsync(HttpClient admin, string label)
    {
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterOwnerAsync(admin, $"{label.Replace(" ", "")}-{suffix}");
        var property = await CreatePropertyAsync(admin, $"{label} Property {suffix}", ["Only"]);
        var invitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"{label.Replace(" ", "").ToLowerInvariant()}-approver-{suffix}@example.test", role = "Admin" }));
        var approver = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await approver.PostAsJsonAsync("/api/auth/accept-invitation", new { token = invitation.InvitationToken, fullName = $"{label} Approver", password = "StrongPass!123" })).StatusCode);
        return (approver, property);
    }

    private static async Task<PropertyResponse> ReadLatestPropertyAsync(HttpClient admin)
    {
        var properties = await ReadAsync<List<PropertyResponse>>(await admin.GetAsync("/api/properties"));
        return properties.Single();
    }

    private static async Task RegisterOwnerAsync(HttpClient client, string suffix) =>
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/register-owner", new
        {
            organizationName = $"Utility Org {suffix}",
            fullName = "Utility Admin",
            email = $"utility-admin-{suffix}@example.test",
            password = "StrongPass!123",
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
        var tenant = await ReadAsync<TenantResponse>(await client.PostAsJsonAsync("/api/tenants", new { name = "Meter Tenant", phone = (string?)null }));
        foreach (var unitId in unitIds)
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/properties/{propertyId}/units/{unitId}/occupancy", new { tenantId = tenant.Id, startDate = "2025-12-01", endDate = (string?)null })).StatusCode);
        }
    }

    private static async Task<MeterInstallationResponse> InstallSubmeterAsync(HttpClient client, Guid propertyId, Guid unitId, decimal? openingReading, string installationDate)
    {
        var response = await client.PostAsJsonAsync($"/api/properties/{propertyId}/meter-installations", new
        {
            unitId,
            mainMeterId = (Guid?)null,
            serialNumber = "SUB",
            displayLabel = (string?)null,
            installationDate,
            openingReading,
            openingReadingDate = installationDate,
            identityKnown = true,
            reason = "Adopted existing submeter",
        });
        return await ReadAsync<MeterInstallationResponse>(response);
    }

    private static async Task<BillingCycleResponse> SaveReadingAsync(HttpClient client, Guid propertyId, BillingCycleResponse cycle, Guid unitBillId, decimal current)
    {
        var response = await client.PutAsJsonAsync($"/api/properties/{propertyId}/billing-cycles/{cycle.Id}/meter-readings", new
        {
            expectedVersion = cycle.Version,
            reason = "Meter reading",
            entries = new[] { new { unitBillId, previousReading = (decimal?)null, currentReading = (decimal?)current, previousReadingDate = (string?)null, currentReadingDate = "2026-01-31" } },
        });
        return await ReadAsync<BillingCycleResponse>(response);
    }

    private static async Task ApproveAsync(HttpClient admin, HttpClient approver, Guid propertyId, BillingCycleResponse cycle)
    {
        var submitted = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{propertyId}/billing-cycles/{cycle.Id}/submit", new { expectedVersion = cycle.Version }));
        var approved = await ReadAsync<BillingCycleResponse>(await approver.PostAsJsonAsync($"/api/properties/{propertyId}/billing-cycles/{cycle.Id}/approve", new { expectedVersion = submitted.Version }));
        Assert.Equal(BillingCycleStatus.Approved, approved.Status);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private sealed record PropertyResponse(Guid Id, string Name, string Address, List<FloorResponse> Floors);

    private sealed record FloorResponse(Guid Id, string Name, int SortOrder, bool IsArchived, List<UnitResponse> Units);

    private sealed record UnitResponse(Guid Id, string Name, bool IsEnabled, bool IsArchived, OccupancyStatus OccupancyStatus);

    private sealed record TenantResponse(Guid Id, string? Name, string? Phone, OnboardingStatus OnboardingStatus);

    private sealed record InvitationResponse(Guid Id, string Email, UserRole Role, DateTimeOffset ExpiresAtUtc, string InvitationToken, string DeliveryStatus);

    private sealed record UtilityAccountResponse(Guid Id, Guid PropertyId, string ProviderName, string CustomerNumber, string? DisplayLabel, DateTimeOffset CreatedAtUtc);

    private sealed record UtilityMainMeterResponse(Guid Id, Guid PropertyId, Guid UtilityAccountId, string MeterNumber, string? DisplayLabel, DateOnly? InstalledOn, DateOnly? EndedOn, bool IsActive);

    private sealed record MeterInstallationResponse(Guid Id, Guid PropertyId, Guid UnitId, string UnitName, Guid? MainMeterId, string? SerialNumber, string? DisplayLabel, DateOnly? InstallationDate, decimal? OpeningReading, DateOnly? OpeningReadingDate, Guid? ReplacedInstallationId, bool IdentityKnown, DateOnly? EndedOn, string? EndReason, decimal? FinalReading, bool FinalReadingUnavailable, bool IsActive, long Version, DateTimeOffset CreatedAtUtc);

    private sealed record MeterInstallationAuditResponse(Guid Id, Guid InstallationId, Guid? PreviousInstallationId, Guid ActorUserId, MeterInstallationAuditAction Action, DateOnly? ReplacementDate, DateTimeOffset OccurredAtUtc, string? Reason, string? Before, string? After);

    private sealed record MeterSetupResponse(Guid PropertyId, List<UtilityAccountResponse> Accounts, List<UtilityMainMeterResponse> MainMeters, List<MeterInstallationResponse> Installations, List<MeterInstallationAuditResponse> Audit);

    private sealed record MeterReplacementResponse(Guid PreviousInstallationId, MeterInstallationResponse NewInstallation, DateTimeOffset ReplacementOccurredAtUtc, string PropertyLocalDate, bool AffectedDraftRecalculated, string? BlockingMessage);

    private sealed record BillingMeterSegmentResponse(Guid? MeterInstallationId, int SortOrder, decimal? PreviousReading, MeterReadingSource PreviousReadingSource, decimal? CurrentReading, decimal? ConsumptionUnits, bool FinalReadingUnavailable);

    private sealed record BillingChargeLineResponse(Guid Id, ChargeType ChargeType, string ChargeName, ChargeSettingMode SettingMode, bool IsMetered, decimal? Rate, decimal? PreviousReading, DateOnly? PreviousReadingDate, decimal? CurrentReading, DateOnly? CurrentReadingDate, decimal? ConsumptionUnits, MeterReadingSource PreviousReadingSource, bool WarningEnabled, decimal? WarningThreshold, decimal Amount, Guid? MeterPhotoId, Guid? MeterInstallationId, List<BillingMeterSegmentResponse>? Segments);

    private sealed record BillingUnitBillResponse(Guid Id, Guid UnitId, string UnitName, string? BlockingReason, decimal Total, bool ElectricityWarning, List<BillingChargeLineResponse> Charges);

    private sealed record BillingCycleResponse(Guid Id, Guid PropertyId, DateOnly BillingMonth, BillingCycleStatus Status, long Version, decimal DraftTotal, decimal? ApprovedTotal, int BlockingBillCount, List<BillingUnitBillResponse> Bills);

    private sealed record MeterRoomOverviewResponse(Guid UnitId, string UnitName, bool ElectricityApplicable, string ReadingStatus, string? BlockingReason, Guid? MeterInstallationId, string? SubmeterLabel, bool? SubmeterIdentityKnown, bool? SubmeterFinalReadingUnavailable);

    private sealed record MeterOverviewResponse(Guid PropertyId, DateOnly BillingMonth, List<MeterRoomOverviewResponse> Units);

    private sealed record BillingStatementChargeResponse(Guid Id, ChargeType ChargeType, decimal? PreviousReading, decimal? CurrentReading, decimal? ConsumptionUnits, decimal Amount, Guid? MeterInstallationId, List<BillingMeterSegmentResponse>? Segments);

    private sealed record BillingStatementUnitResponse(string BillReference, Guid BillId, Guid UnitId, string UnitName, decimal Total, List<BillingStatementChargeResponse> Charges);

    private sealed record BillingStatementResponse(string StatementType, Guid PropertyId, Guid BillingCycleId, DateOnly BillingMonth, decimal ApprovedTotal, List<BillingStatementUnitResponse> Bills);
}
