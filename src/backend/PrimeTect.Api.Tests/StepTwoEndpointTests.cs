using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PrimeTect.Api.Tests;

public sealed class StepTwoEndpointTests : IClassFixture<PrimeTectApiFactory>
{
    private readonly PrimeTectApiFactory factory;

    public StepTwoEndpointTests(PrimeTectApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Authorized_qr_links_keep_stable_unit_mapping_and_revoke_previous_links()
    {
        using var admin = factory.CreateClient();
        var email = $"step-two-qr-{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/auth/register-owner", new
        {
            organizationName = "Step Two QR Organization",
            fullName = "Step Two Admin",
            email,
            password = "StrongPass!123",
        })).StatusCode);

        using var propertyResponse = await admin.PostAsJsonAsync("/api/properties", new
        {
            name = "QR Property",
            address = "1 QR Street",
            propertyType = "ApartmentBuilding",
            latitude = 23.8103m,
            longitude = 90.4125m,
            floors = new[] { new { name = "Ground", unitCount = 1, unitNames = new[] { "6B" } } },
        });
        Assert.Equal(HttpStatusCode.Created, propertyResponse.StatusCode);
        using var propertyJson = JsonDocument.Parse(await propertyResponse.Content.ReadAsStringAsync());
        var propertyId = propertyJson.RootElement.GetProperty("id").GetGuid();
        var unitId = propertyJson.RootElement.GetProperty("floors")[0].GetProperty("units")[0].GetProperty("id").GetGuid();

        var firstResponse = await admin.PostAsJsonAsync($"/api/properties/{propertyId}/units/{unitId}/meter-qr", new { placement = "ElectricityMeter" });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        using var firstJson = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var firstLinkId = firstJson.RootElement.GetProperty("id").GetGuid();
        var firstUrl = firstJson.RootElement.GetProperty("url").GetString()!;
        Assert.Contains("<svg", firstJson.RootElement.GetProperty("svg").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(propertyId, firstJson.RootElement.GetProperty("propertyId").GetGuid());
        Assert.Equal(unitId, firstJson.RootElement.GetProperty("unitId").GetGuid());
        var firstToken = ExtractMeterToken(firstUrl);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/meter-links/{firstToken}")).StatusCode);
        var resolved = await admin.GetFromJsonAsync<JsonDocument>($"/api/meter-links/{firstToken}?billingMonth=2026-01");
        Assert.Equal(propertyId, resolved!.RootElement.GetProperty("propertyId").GetGuid());
        Assert.Equal(unitId, resolved.RootElement.GetProperty("unitId").GetGuid());
        Assert.Equal("2026-01", resolved.RootElement.GetProperty("billingMonth").GetString());

        var secondResponse = await admin.PostAsJsonAsync($"/api/properties/{propertyId}/units/{unitId}/meter-qr", new { placement = "RoomDoor" });
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        using var secondJson = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        var secondLinkId = secondJson.RootElement.GetProperty("id").GetGuid();
        var secondToken = ExtractMeterToken(secondJson.RootElement.GetProperty("url").GetString()!);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/meter-links/{firstToken}?billingMonth=2026-01")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/meter-qr-links/{secondLinkId}/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/meter-links/{secondToken}?billingMonth=2026-01")).StatusCode);

        using var history = await admin.GetAsync($"/api/properties/{propertyId}/units/{unitId}/meter-qr");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        using var historyJson = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Equal(2, historyJson.RootElement.GetArrayLength());
        Assert.Contains(historyJson.RootElement.EnumerateArray(), item => item.GetProperty("id").GetGuid() == firstLinkId && !item.GetProperty("isActive").GetBoolean());

        using var otherOrganization = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await otherOrganization.PostAsJsonAsync("/api/auth/register-owner", new
        {
            organizationName = "Other QR Organization",
            fullName = "Other Admin",
            email = $"step-two-other-{Guid.NewGuid():N}@example.test",
            password = "StrongPass!123",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherOrganization.GetAsync($"/api/meter-links/{firstToken}?billingMonth=2026-01")).StatusCode);

        using var invalidLocation = await admin.PostAsJsonAsync("/api/properties", new
        {
            name = "Invalid Location",
            address = "2 QR Street",
            propertyType = "ApartmentBuilding",
            latitude = 23.8m,
            longitude = (decimal?)null,
            floors = new[] { new { name = "Ground", unitCount = 1, unitNames = new[] { "1A" } } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidLocation.StatusCode);
    }

    private static string ExtractMeterToken(string url)
    {
        var marker = "meterLink=";
        var index = url.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(index >= 0);
        return Uri.UnescapeDataString(url[(index + marker.Length)..]);
    }
}
