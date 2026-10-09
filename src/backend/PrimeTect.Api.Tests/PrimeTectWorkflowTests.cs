using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrimeTect.Domain.Enums;
using Xunit;

namespace PrimeTect.Api.Tests;

public sealed class PrimeTectWorkflowTests : IClassFixture<PrimeTectApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PrimeTectApiFactory factory;

    public PrimeTectWorkflowTests(PrimeTectApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Owner_access_and_dynamic_property_workflow_is_scoped()
    {
        using var anonymous = factory.CreateClient();
        var unauthorized = await anonymous.GetAsync("/api/properties");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var ownerA = factory.CreateClient();
        var registrationA = await ownerA.PostAsJsonAsync("/api/auth/register-owner", new
        {
            organizationName = "Alpha Homes",
            fullName = "Alpha Admin",
            email = "admin-alpha@example.test",
            password = "StrongPass!123",
        });
        Assert.Equal(HttpStatusCode.OK, registrationA.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ownerA.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ownerA.GetAsync("/api/properties")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ownerA.PostAsJsonAsync("/api/auth/login", new { email = "admin-alpha@example.test", password = "StrongPass!123" })).StatusCode);

        var propertyResponse = await ownerA.PostAsJsonAsync("/api/properties", new
        {
            name = "Unequal Layout House",
            address = "1 Prime Street",
            propertyType = "ApartmentBuilding",
            floors = new[]
            {
                new { name = "Floor 1", unitCount = 2, unitNames = new[] { "1A", "1B" } },
                new { name = "Floor 2", unitCount = 2, unitNames = new[] { "2A", "2B" } },
                new { name = "Floor 3", unitCount = 2, unitNames = new[] { "3A", "3B" } },
                new { name = "Floor 4", unitCount = 1, unitNames = new[] { "4A" } },
                new { name = "Floor 5", unitCount = 1, unitNames = new[] { "5A" } },
                new { name = "Floor 6", unitCount = 1, unitNames = new[] { "6A" } },
            },
        });
        Assert.Equal(HttpStatusCode.Created, propertyResponse.StatusCode);
        var property = await ReadAsync<PropertyResponse>(propertyResponse);
        Assert.Equal(6, property.Floors.Count);
        Assert.Equal(new[] { 2, 2, 2, 1, 1, 1 }, property.Floors.Select(floor => floor.Units.Count).ToArray());

        var previewResponse = await ownerA.PostAsJsonAsync("/api/properties/preview", new
        {
            name = "Preview",
            address = "Preview",
            propertyType = "IndividualRoomBuilding",
            floors = new[] { new { name = "Level 1", unitCount = 2, unitNames = Array.Empty<string>() } },
        });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var previewJson = await previewResponse.Content.ReadAsStringAsync();
        Assert.Contains("Level 1-1", previewJson);
        Assert.Contains("Level 1-2", previewJson);

        using var ownerB = factory.CreateClient();
        var registrationB = await ownerB.PostAsJsonAsync("/api/auth/register-owner", new
        {
            organizationName = "Beta Homes",
            fullName = "Beta Admin",
            email = "admin-beta@example.test",
            password = "StrongPass!123",
        });
        Assert.Equal(HttpStatusCode.OK, registrationB.StatusCode);
        var crossOrganization = await ownerB.GetAsync($"/api/properties/{property.Id}");
        Assert.Equal(HttpStatusCode.NotFound, crossOrganization.StatusCode);
        var crossOrganizationUpdate = await ownerB.PutAsJsonAsync($"/api/properties/{property.Id}", new
        {
            name = "Should Stay Private",
            address = "No Access",
            propertyType = "Shop",
            isArchived = false,
        });
        Assert.Equal(HttpStatusCode.NotFound, crossOrganizationUpdate.StatusCode);
    }

    [Fact]
    public async Task Staff_assignment_tenant_history_and_charge_overrides_are_enforced()
    {
        using var admin = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/auth/register-owner", new
        {
            organizationName = "Workflow Homes",
            fullName = "Workflow Admin",
            email = "admin-workflow@example.test",
            password = "StrongPass!123",
        })).StatusCode);

        var propertyResponse = await admin.PostAsJsonAsync("/api/properties", new
        {
            name = "Workflow Building",
            address = "2 Prime Street",
            propertyType = "Shop",
            floors = new[] { new { name = "Ground", unitCount = 2, unitNames = new[] { "Shop A", "Shop B" } } },
        });
        var property = await ReadAsync<PropertyResponse>(propertyResponse);
        var unitA = property.Floors.Single().Units[0];
        var unitB = property.Floors.Single().Units[1];

        var propertyUpdate = await admin.PutAsJsonAsync($"/api/properties/{property.Id}", new
        {
            name = "Workflow Building Updated",
            address = "3 Prime Street",
            propertyType = "Shop",
            isArchived = false,
        });
        Assert.Equal(HttpStatusCode.OK, propertyUpdate.StatusCode);

        var floor = property.Floors.Single();
        var floorUpdate = await admin.PutAsJsonAsync($"/api/properties/{property.Id}/floors/{floor.Id}", new
        {
            name = "Ground Floor",
            sortOrder = 0,
            unitNames = new[] { "Shop A", "Shop B", "Shop C" },
            isArchived = false,
        });
        Assert.Equal(HttpStatusCode.OK, floorUpdate.StatusCode);
        var editedProperty = await ReadAsync<PropertyResponse>(floorUpdate);
        Assert.Equal("Workflow Building Updated", editedProperty.Name);
        Assert.Equal(new[] { "Shop A", "Shop B", "Shop C" }, editedProperty.Floors.Single().Units.Select(unit => unit.Name).ToArray());

        var invitationResponse = await admin.PostAsJsonAsync("/api/staff/invitations", new { email = "staff-workflow@example.test", role = "BuildingStaff" });
        Assert.Equal(HttpStatusCode.Created, invitationResponse.StatusCode);
        var invitation = await ReadAsync<InvitationResponse>(invitationResponse);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/staff/invitations", new { email = "second-admin@example.test", role = "Admin" })).StatusCode);

        using var staff = factory.CreateClient();
        var acceptResponse = await staff.PostAsJsonAsync("/api/auth/accept-invitation", new
        {
            token = invitation.InvitationToken,
            fullName = "Assigned Staff",
            password = "StrongPass!123",
        });
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        var staffUser = await ReadAsync<ApiUserResponse>(acceptResponse);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync("/api/auth/accept-invitation", new
        {
            token = invitation.InvitationToken,
            fullName = "Reused Invitation",
            password = "StrongPass!123",
        })).StatusCode);

        var beforeAssignment = await staff.GetAsync("/api/properties");
        Assert.Equal(HttpStatusCode.OK, beforeAssignment.StatusCode);
        Assert.Empty(await ReadAsync<List<PropertyResponse>>(beforeAssignment));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/properties/{property.Id}/staff/{staffUser.Id}", null)).StatusCode);
        var afterAssignment = await staff.GetAsync("/api/properties");
        var assignedProperties = await ReadAsync<List<PropertyResponse>>(afterAssignment);
        Assert.Single(assignedProperties);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsJsonAsync($"/api/properties/{property.Id}", new
        {
            name = "Staff Cannot Edit Structure",
            address = "2 Prime Street",
            propertyType = "Shop",
            isArchived = false,
        })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/tenants", new { name = (string?)null, phone = (string?)null })).StatusCode);
        var emptyTenantResponse = await admin.PostAsJsonAsync("/api/tenants", new { name = (string?)null, phone = (string?)null });
        Assert.Equal(HttpStatusCode.Created, emptyTenantResponse.StatusCode);
        var tenant = await ReadAsync<TenantResponse>(emptyTenantResponse);
        Assert.Null(tenant.Name);
        Assert.Null(tenant.Phone);

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/properties/{property.Id}/units/{unitA.Id}/occupancy", new
        {
            tenantId = tenant.Id,
            startDate = "2026-01-01",
            endDate = (string?)null,
        })).StatusCode);
        var firstOccupancyResponse = await admin.PostAsJsonAsync($"/api/properties/{property.Id}/units/{unitA.Id}/occupancy", new
        {
            tenantId = tenant.Id,
            startDate = "2026-01-01",
            endDate = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Created, firstOccupancyResponse.StatusCode);
        var firstOccupancy = await ReadAsync<OccupancyResponse>(firstOccupancyResponse);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/units/{unitB.Id}/occupancy", new
        {
            tenantId = tenant.Id,
            startDate = "2026-02-01",
            endDate = (string?)null,
        })).StatusCode);

        var secondTenantResponse = await admin.PostAsJsonAsync("/api/tenants", new { name = (string?)null, phone = "01700000000" });
        var secondTenant = await ReadAsync<TenantResponse>(secondTenantResponse);
        var overlap = await admin.PostAsJsonAsync($"/api/properties/{property.Id}/units/{unitA.Id}/occupancy", new
        {
            tenantId = secondTenant.Id,
            startDate = "2026-03-01",
            endDate = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);

        var closeOccupancy = await admin.PutAsJsonAsync($"/api/properties/{property.Id}/units/{unitA.Id}/occupancy/{firstOccupancy.Id}/close", new { endDate = "2026-06-01" });
        Assert.Equal(HttpStatusCode.OK, closeOccupancy.StatusCode);
        var history = await admin.GetAsync($"/api/properties/{property.Id}/units/{unitA.Id}/occupancy");
        Assert.Contains("2026-06-01", await history.Content.ReadAsStringAsync());

        var tenantUpdate = await admin.PutAsJsonAsync($"/api/tenants/{tenant.Id}", new { name = "Details Added Later", phone = (string?)null });
        var updatedTenant = await ReadAsync<TenantResponse>(tenantUpdate);
        Assert.Equal("Details Added Later", updatedTenant.Name);
        Assert.Null(updatedTenant.Phone);

        var statusResponse = await admin.PutAsJsonAsync($"/api/properties/{property.Id}/units/{unitA.Id}/occupancy-status", new { status = "Occupied" });
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var propertyChargeResponse = await admin.PutAsJsonAsync($"/api/properties/{property.Id}/charges", new
        {
            items = new[]
            {
                new { chargeType = "Gas", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)120.50m, customLabel = (string?)null },
                new { chargeType = "ServiceCharge", mode = "Disabled", billingMonth = "2026-01-01", amount = (decimal?)null, customLabel = (string?)null },
            },
        });
        Assert.Equal(HttpStatusCode.OK, propertyChargeResponse.StatusCode);
        var propertyCharges = await ReadAsync<List<ChargeResponse>>(propertyChargeResponse);
        Assert.Contains(propertyCharges, setting => setting.ChargeType == ChargeType.ServiceCharge && setting.Mode == ChargeSettingMode.Disabled && setting.Amount is null);

        var unitChargeResponse = await admin.PutAsJsonAsync($"/api/properties/{property.Id}/units/{unitA.Id}/charges", new
        {
            items = new[]
            {
                new { chargeType = "Gas", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)90.25m, customLabel = (string?)null },
                new { chargeType = "Water", mode = "Inherited", billingMonth = "2026-01-01", amount = (decimal?)null, customLabel = (string?)null },
                new { chargeType = "ServiceCharge", mode = "Disabled", billingMonth = "2026-01-01", amount = (decimal?)null, customLabel = (string?)null },
            },
        });
        Assert.Equal(HttpStatusCode.OK, unitChargeResponse.StatusCode);
        var chargeSettings = await ReadAsync<List<ChargeResponse>>(unitChargeResponse);
        Assert.Contains(chargeSettings, setting => setting.ChargeType == ChargeType.Gas && setting.Amount == 90.25m && setting.Mode == ChargeSettingMode.Custom);
        Assert.Contains(chargeSettings, setting => setting.ChargeType == ChargeType.Water && setting.Amount is null && setting.Mode == ChargeSettingMode.Inherited);
        Assert.Contains(chargeSettings, setting => setting.ChargeType == ChargeType.ServiceCharge && setting.Amount is null && setting.Mode == ChargeSettingMode.Disabled);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private sealed record PropertyResponse(Guid Id, string Name, string Address, PropertyType PropertyType, bool IsArchived, List<FloorResponse> Floors);

    private sealed record FloorResponse(Guid Id, string Name, int SortOrder, bool IsArchived, List<UnitResponse> Units);

    private sealed record UnitResponse(Guid Id, string Name, bool IsEnabled, bool IsArchived, OccupancyStatus OccupancyStatus);

    private sealed record ApiUserResponse(Guid Id, string Email, string FullName, UserRole Role, Guid OrganizationId);

    private sealed record InvitationResponse(Guid Id, string Email, UserRole Role, DateTimeOffset ExpiresAtUtc, string InvitationToken, string DeliveryStatus);

    private sealed record TenantResponse(Guid Id, string? Name, string? Phone, OnboardingStatus OnboardingStatus);

    private sealed record OccupancyResponse(Guid Id, Guid OrganizationId, Guid TenantId, Guid UnitId, DateOnly StartDate, DateOnly? EndDate, DateTimeOffset CreatedAtUtc);

    private sealed record ChargeResponse(Guid Id, Guid? UnitId, ChargeType ChargeType, ChargeSettingMode Mode, DateOnly BillingMonth, decimal? Amount, string? CustomLabel);
}
