using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrimeTect.Domain.Enums;
using Xunit;

namespace PrimeTect.Api.Tests;

public sealed class BillingWorkflowTests : IClassFixture<PrimeTectApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PrimeTectApiFactory factory;

    public BillingWorkflowTests(PrimeTectApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Monthly_billing_captures_metered_and_fixed_charges_through_approval()
    {
        using var admin = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/auth/register-owner", new
        {
            organizationName = "Billing Homes",
            fullName = "Billing Admin",
            email = "billing-admin@example.test",
            password = "StrongPass!123",
        })).StatusCode);

        var property = await CreatePropertyAsync(admin, "Billing Property", new[] { "A", "B", "C" });
        var units = property.Floors.Single().Units;
        var tenantResponse = await admin.PostAsJsonAsync("/api/tenants", new { name = (string?)null, phone = (string?)null });
        var tenant = await ReadAsync<TenantResponse>(tenantResponse);
        foreach (var unit in units)
        {
            Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/units/{unit.Id}/occupancy", new
            {
                tenantId = tenant.Id,
                startDate = "2025-12-01",
                endDate = (string?)null,
            })).StatusCode);
        }

        await SavePropertyChargesAsync(admin, property.Id, new[]
        {
            new { chargeType = "Electricity", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)19.9000m, customLabel = (string?)null },
            new { chargeType = "ServiceCharge", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)12.3456m, customLabel = (string?)null },
        });
        await SaveUnitChargesAsync(admin, property.Id, units[0].Id, new[]
        {
            new { chargeType = "Electricity", mode = "Inherited", billingMonth = "2026-01-01", amount = (decimal?)null, customLabel = (string?)null },
            new { chargeType = "ServiceCharge", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)12.3456m, customLabel = (string?)null },
        });
        await SaveUnitChargesAsync(admin, property.Id, units[1].Id, new[]
        {
            new { chargeType = "Electricity", mode = "Disabled", billingMonth = "2026-01-01", amount = (decimal?)null, customLabel = (string?)null },
            new { chargeType = "ServiceCharge", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)30m, customLabel = (string?)null },
        });
        await SaveUnitChargesAsync(admin, property.Id, units[2].Id, new[]
        {
            new { chargeType = "Electricity", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)20m, customLabel = (string?)null },
            new { chargeType = "ServiceCharge", mode = "Inherited", billingMonth = "2026-01-01", amount = (decimal?)null, customLabel = (string?)null },
        });

        var invitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = "billing-staff@example.test", role = "BuildingStaff" }));
        using var staff = factory.CreateClient();
        var staffUser = await ReadAsync<UserResponse>(await staff.PostAsJsonAsync("/api/auth/accept-invitation", new
        {
            token = invitation.InvitationToken,
            fullName = "Billing Staff",
            password = "StrongPass!123",
        }));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/properties/{property.Id}/staff/{staffUser.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" })).StatusCode);

        var generated = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        Assert.Equal(BillingCycleStatus.Draft, generated.Status);
        Assert.Equal(2, generated.BlockingBillCount);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{generated.Id}/submit", new { expectedVersion = generated.Version })).StatusCode);

        var electricityA = generated.Bills.Single(bill => bill.UnitName == "A").Charges.Single(charge => charge.ChargeType == ChargeType.Electricity);
        Assert.Equal(MeterReadingSource.Missing, electricityA.PreviousReadingSource);
        Assert.Null(electricityA.PreviousReading);
        var serviceOnly = generated.Bills.Single(bill => bill.UnitName == "B");
        Assert.DoesNotContain(serviceOnly.Charges, charge => charge.ChargeType == ChargeType.Electricity);
        Assert.Equal(30m, serviceOnly.Total);
        Assert.Null(generated.Bills.Single(bill => bill.UnitName == "A").TenantName);
        Assert.Null(generated.Bills.Single(bill => bill.UnitName == "A").TenantPhone);

        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PutAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{generated.Id}/meter-readings", new
        {
            expectedVersion = generated.Version,
            reason = "Rejected negative usage",
            entries = new[] { new { unitBillId = generated.Bills.Single(bill => bill.UnitName == "A").Id, previousReading = 100m, currentReading = 99m } },
        })).StatusCode);

        var saved = await ReadAsync<BillingCycleResponse>(await staff.PutAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{generated.Id}/meter-readings", new
        {
            expectedVersion = generated.Version,
            reason = "Opening readings entered from the documented meter sheet",
            entries = new[]
            {
                new { unitBillId = generated.Bills.Single(bill => bill.UnitName == "A").Id, previousReading = 100m, currentReading = 110m },
                new { unitBillId = generated.Bills.Single(bill => bill.UnitName == "C").Id, previousReading = 100m, currentReading = 110m },
            },
        }));
        Assert.Equal(0, saved.BlockingBillCount);
        Assert.True(saved.Bills.Single(bill => bill.UnitName == "A").ElectricityWarning);
        Assert.Equal(199m, saved.Bills.Single(bill => bill.UnitName == "A").Charges.Single(charge => charge.ChargeType == ChargeType.Electricity).Amount);
        Assert.False(saved.Bills.Single(bill => bill.UnitName == "C").ElectricityWarning);
        Assert.Equal(200m, saved.Bills.Single(bill => bill.UnitName == "C").Charges.Single(charge => charge.ChargeType == ChargeType.Electricity).Amount);

        Assert.Equal(HttpStatusCode.Conflict, (await staff.PutAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{generated.Id}/meter-readings", new
        {
            expectedVersion = generated.Version,
            entries = new[] { new { unitBillId = generated.Bills.Single(bill => bill.UnitName == "A").Id, previousReading = 100m, currentReading = 110m } },
        })).StatusCode);

        var submitted = await ReadAsync<BillingCycleResponse>(await staff.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{generated.Id}/submit", new { expectedVersion = saved.Version, reason = "Staff submitted readings for review" }));
        Assert.Equal(BillingCycleStatus.Submitted, submitted.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PutAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{generated.Id}/meter-readings", new
        {
            expectedVersion = submitted.Version,
            entries = Array.Empty<object>(),
        })).StatusCode);

        var returned = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{generated.Id}/return", new { expectedVersion = submitted.Version, reason = "Please confirm the current readings." }));
        Assert.Equal(BillingCycleStatus.Returned, returned.Status);
        var resubmittedDraft = await ReadAsync<BillingCycleResponse>(await staff.PutAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{generated.Id}/meter-readings", new
        {
            expectedVersion = returned.Version,
            reason = "Confirmed after review",
            entries = new[] { new { unitBillId = returned.Bills.Single(bill => bill.UnitName == "A").Id, previousReading = 100m, currentReading = 111m } },
        }));
        var resubmitted = await ReadAsync<BillingCycleResponse>(await staff.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{generated.Id}/submit", new { expectedVersion = resubmittedDraft.Version }));
        var approved = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{generated.Id}/approve", new { expectedVersion = resubmitted.Version }));
        Assert.Equal(BillingCycleStatus.Approved, approved.Status);
        Assert.Equal(approved.DraftTotal, approved.ApprovedTotal);
        Assert.Equal(111m, approved.Bills.Single(bill => bill.UnitName == "A").Charges.Single(charge => charge.ChargeType == ChargeType.Electricity).CurrentReading);

        Assert.Equal(HttpStatusCode.Conflict, (await staff.PutAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{generated.Id}/meter-readings", new
        {
            expectedVersion = approved.Version,
            entries = Array.Empty<object>(),
        })).StatusCode);

        await SavePropertyChargesAsync(admin, property.Id, new[]
        {
            new { chargeType = "Electricity", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)99m, customLabel = (string?)null },
        });
        var unchangedApproved = await ReadAsync<BillingCycleResponse>(await admin.GetAsync($"/api/properties/{property.Id}/billing-cycles/2026-01"));
        Assert.Equal(19.9000m, unchangedApproved.Bills.Single(bill => bill.UnitName == "A").Charges.Single(charge => charge.ChargeType == ChargeType.Electricity).Rate);

        var february = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" }));
        Assert.Equal(MeterReadingSource.ApprovedPeriod, february.Bills.Single(bill => bill.UnitName == "A").Charges.Single(charge => charge.ChargeType == ChargeType.Electricity).PreviousReadingSource);
        Assert.Equal(111m, february.Bills.Single(bill => bill.UnitName == "A").Charges.Single(charge => charge.ChargeType == ChargeType.Electricity).PreviousReading);
        Assert.Equal(99m, february.Bills.Single(bill => bill.UnitName == "A").Charges.Single(charge => charge.ChargeType == ChargeType.Electricity).Rate);

        var march = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-03-01" }));
        var marchElectricity = march.Bills.Single(bill => bill.UnitName == "A").Charges.Single(charge => charge.ChargeType == ChargeType.Electricity);
        Assert.Equal(MeterReadingSource.Missing, marchElectricity.PreviousReadingSource);
        Assert.Null(marchElectricity.PreviousReading);
        Assert.Contains("immediately preceding approved period", march.Bills.Single(bill => bill.UnitName == "A").BlockingReason);

        using var otherOrganization = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await otherOrganization.PostAsJsonAsync("/api/auth/register-owner", new
        {
            organizationName = "Other Billing Homes",
            fullName = "Other Admin",
            email = "other-billing-admin@example.test",
            password = "StrongPass!123",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherOrganization.GetAsync($"/api/properties/{property.Id}/billing-cycles/2026-01")).StatusCode);
    }

    [Fact]
    public async Task Policy_gaps_self_approval_and_atomic_approval_failure_are_visible()
    {
        using var admin = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/auth/register-owner", new
        {
            organizationName = "Policy Homes",
            fullName = "Policy Admin",
            email = "policy-admin@example.test",
            password = "StrongPass!123",
        })).StatusCode);

        var managerInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = "policy-manager@example.test", role = "Manager" }));
        using var manager = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsJsonAsync("/api/auth/accept-invitation", new
        {
            token = managerInvitation.InvitationToken,
            fullName = "Policy Manager",
            password = "StrongPass!123",
        })).StatusCode);

        var policyProperty = await CreatePropertyAsync(admin, "Policy Property", new[] { "Partial", "Vacant" });
        await SavePropertyChargesAsync(admin, policyProperty.Id, new[] { new { chargeType = "ServiceCharge", mode = "Custom", billingMonth = "2026-04-01", amount = (decimal?)50m, customLabel = (string?)null } });
        var tenantResponse = await admin.PostAsJsonAsync("/api/tenants", new { name = "Partial tenant", phone = (string?)null });
        var tenant = await ReadAsync<TenantResponse>(tenantResponse);
        var partialUnit = policyProperty.Floors.Single().Units[0];
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{policyProperty.Id}/units/{partialUnit.Id}/occupancy", new { tenantId = tenant.Id, startDate = "2026-04-15", endDate = (string?)null })).StatusCode);
        var policyDraft = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{policyProperty.Id}/billing-cycles", new { billingMonth = "2026-04-01" }));
        Assert.Equal(2, policyDraft.BlockingBillCount);
        Assert.All(policyDraft.Bills, bill => Assert.NotNull(bill.BlockingReason));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/properties/{policyProperty.Id}/billing-cycles/{policyDraft.Id}/submit", new { expectedVersion = policyDraft.Version })).StatusCode);

        var fixedProperty = await CreatePropertyAsync(admin, "Fixed Property", new[] { "Only" });
        await SavePropertyChargesAsync(admin, fixedProperty.Id, new[] { new { chargeType = "ServiceCharge", mode = "Custom", billingMonth = "2026-04-01", amount = (decimal?)50m, customLabel = (string?)null } });
        var fixedUnit = fixedProperty.Floors.Single().Units.Single();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{fixedProperty.Id}/units/{fixedUnit.Id}/occupancy", new { tenantId = tenant.Id, startDate = "2026-03-01", endDate = (string?)null })).StatusCode);
        var fixedDraft = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{fixedProperty.Id}/billing-cycles", new { billingMonth = "2026-04-01" }));
        var fixedSubmitted = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{fixedProperty.Id}/billing-cycles/{fixedDraft.Id}/submit", new { expectedVersion = fixedDraft.Version }));
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/properties/{fixedProperty.Id}/billing-cycles/{fixedDraft.Id}/approve", new { expectedVersion = fixedSubmitted.Version })).StatusCode);

        factory.FailNextSave = true;
        var failedApproval = await manager.PostAsJsonAsync($"/api/properties/{fixedProperty.Id}/billing-cycles/{fixedDraft.Id}/approve", new { expectedVersion = fixedSubmitted.Version });
        Assert.Equal(HttpStatusCode.InternalServerError, failedApproval.StatusCode);
        var stillSubmitted = await ReadAsync<BillingCycleResponse>(await admin.GetAsync($"/api/properties/{fixedProperty.Id}/billing-cycles/2026-04"));
        Assert.Equal(BillingCycleStatus.Submitted, stillSubmitted.Status);
        Assert.DoesNotContain(stillSubmitted.Audit, item => item.Action == BillingAuditAction.Approved);
        var finallyApproved = await ReadAsync<BillingCycleResponse>(await manager.PostAsJsonAsync($"/api/properties/{fixedProperty.Id}/billing-cycles/{fixedDraft.Id}/approve", new { expectedVersion = stillSubmitted.Version }));
        Assert.Equal(BillingCycleStatus.Approved, finallyApproved.Status);
    }

    private async Task<PropertyResponse> CreatePropertyAsync(HttpClient client, string name, IReadOnlyList<string> unitNames)
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

    private static async Task SavePropertyChargesAsync(HttpClient client, Guid propertyId, object[] items)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/properties/{propertyId}/charges", new { items })).StatusCode);
    }

    private static async Task SaveUnitChargesAsync(HttpClient client, Guid propertyId, Guid unitId, object[] items)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/properties/{propertyId}/units/{unitId}/charges", new { items })).StatusCode);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private sealed record PropertyResponse(Guid Id, string Name, string Address, PropertyType PropertyType, bool IsArchived, List<FloorResponse> Floors);

    private sealed record FloorResponse(Guid Id, string Name, int SortOrder, bool IsArchived, List<UnitResponse> Units);

    private sealed record UnitResponse(Guid Id, string Name, bool IsEnabled, bool IsArchived, OccupancyStatus OccupancyStatus);

    private sealed record TenantResponse(Guid Id, string? Name, string? Phone, OnboardingStatus OnboardingStatus);

    private sealed record UserResponse(Guid Id, string Email, string FullName, UserRole Role, Guid OrganizationId);

    private sealed record InvitationResponse(Guid Id, string Email, UserRole Role, DateTimeOffset ExpiresAtUtc, string InvitationToken, string DeliveryStatus);

    private sealed record BillingCycleResponse(Guid Id, Guid PropertyId, DateOnly BillingMonth, BillingCycleStatus Status, long Version, Guid LastChangedByUserId, DateTimeOffset LastChangedAtUtc, string? ReturnReason, decimal DraftTotal, decimal? ApprovedTotal, int BlockingBillCount, List<BillingUnitBillResponse> Bills, List<BillingAuditResponse> Audit);

    private sealed record BillingUnitBillResponse(Guid Id, Guid UnitId, string UnitName, Guid? OccupancyAssignmentId, Guid? TenantId, string? TenantName, string? TenantPhone, DateOnly? OccupancyStartDate, DateOnly? OccupancyEndDate, string? BlockingReason, decimal Total, bool ElectricityWarning, List<BillingChargeLineResponse> Charges);

    private sealed record BillingChargeLineResponse(Guid Id, ChargeType ChargeType, string ChargeName, ChargeSettingMode SettingMode, bool IsMetered, decimal? Rate, decimal? PreviousReading, decimal? CurrentReading, decimal? ConsumptionUnits, MeterReadingSource PreviousReadingSource, decimal Amount);

    private sealed record BillingAuditResponse(Guid Id, Guid ActorUserId, BillingAuditAction Action, DateTimeOffset OccurredAtUtc, string? Reason, string? Before, string? After);
}
