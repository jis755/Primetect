using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using PrimeTect.Domain.Enums;
using Xunit;

namespace PrimeTect.Api.Tests;

public sealed class CollectionWorkflowTests : IClassFixture<PrimeTectApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PrimeTectApiFactory factory;

    public CollectionWorkflowTests(PrimeTectApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Collections_have_role_boundaries_balances_corrections_and_reversals()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        await RegisterAsync(admin, $"collections-admin-{suffix}@example.test", "Collections Admin", $"Collections Organization {suffix}");

        var property = await ReadAsync<PropertyResponse>(await admin.PostAsJsonAsync("/api/properties", new
        {
            name = $"Collections Property {suffix}",
            address = "8 Collection Street",
            propertyType = "ApartmentBuilding",
            floors = new[] { new { name = "Ground", unitCount = 1, unitNames = new[] { "C-1" } } },
        }));
        var unit = property.Floors.Single().Units.Single();
        var tenant = await ReadAsync<TenantResponse>(await admin.PostAsJsonAsync("/api/tenants", new { name = (string?)null, phone = (string?)null }));
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/units/{unit.Id}/occupancy", new
        {
            tenantId = tenant.Id,
            startDate = "2026-01-01",
            endDate = (string?)null,
        })).StatusCode);
        await SaveChargeAsync(admin, property.Id, 300m);

        var draft = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-01-01" }));
        var submitted = await ReadAsync<BillingCycleResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{draft.Id}/submit", new { expectedVersion = draft.Version }));
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{draft.Id}/approve", new { expectedVersion = submitted.Version })).StatusCode);

        using var secondAdmin = factory.CreateClient();
        var secondAdminInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"second-admin-{suffix}@example.test", role = "Admin" }));
        await AcceptInvitationAsync(secondAdmin, secondAdminInvitation.InvitationToken, "Second Admin");
        var approved = await ReadAsync<BillingCycleResponse>(await secondAdmin.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles/{draft.Id}/approve", new { expectedVersion = submitted.Version }));
        Assert.Equal(BillingCycleStatus.Approved, approved.Status);

        using var issueReporter = factory.CreateClient();
        var issueReporterInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"issue-reporter-{suffix}@example.test", role = "Manager" }));
        var issueReporterUser = await AcceptInvitationAsync(issueReporter, issueReporterInvitation.InvitationToken, "Issue Reporter");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/properties/{property.Id}/staff/{issueReporterUser.Id}", null)).StatusCode);
        var issue = await ReadAsync<RoomMeterIssueResponse>(await issueReporter.PostAsJsonAsync($"/api/properties/{property.Id}/units/{unit.Id}/room-meter-issues", new
        {
            issueType = "DamagedMeter",
            description = "Synthetic test issue; no reading supplied.",
            observedDate = "2026-01-10",
        }));
        Assert.Equal(RoomMeterIssueStatus.Open, issue.Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await financeGetIssueClientAsync()).StatusCode);
        var meterOverview = await ReadAsync<MeterOverviewResponse>(await admin.GetAsync($"/api/properties/{property.Id}/meter-overview/2026-01"));
        Assert.Equal(1, meterOverview.Units.Single().ReportedIssueCount);
        var collectionOverview = await ReadAsync<CollectionOverviewResponse>(await admin.GetAsync($"/api/properties/{property.Id}/collection-overview/2026-01"));
        Assert.Equal("Approved", collectionOverview.BillingCoverage);
        Assert.Equal(300m, collectionOverview.Units.Single().RemainingBalance);

        async Task<HttpResponseMessage> financeGetIssueClientAsync()
        {
            using var financeWithoutIssuePermission = factory.CreateClient();
            var invitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"issue-finance-{suffix}@example.test", role = "FinanceManager" }));
            var user = await AcceptInvitationAsync(financeWithoutIssuePermission, invitation.InvitationToken, "Issue Finance");
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/properties/{property.Id}/staff/{user.Id}", null)).StatusCode);
            return await financeWithoutIssuePermission.GetAsync($"/api/properties/{property.Id}/room-meter-issues");
        }

        using var manager = factory.CreateClient();
        var managerInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"building-manager-{suffix}@example.test", role = "Manager" }));
        var managerUser = await AcceptInvitationAsync(manager, managerInvitation.InvitationToken, "Building Manager");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/properties/{property.Id}/staff/{managerUser.Id}", null)).StatusCode);

        using var finance = factory.CreateClient();
        var financeInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"finance-manager-{suffix}@example.test", role = "FinanceManager" }));
        var financeUser = await AcceptInvitationAsync(finance, financeInvitation.InvitationToken, "Finance Manager");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/properties/{property.Id}/staff/{financeUser.Id}", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync("/api/properties", new
        {
            name = "Not allowed",
            address = "No access",
            propertyType = "Shop",
            floors = new[] { new { name = "Ground", unitCount = 1, unitNames = new[] { "X-1" } } },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"/api/properties/{property.Id}/charges", new { items = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.PostAsJsonAsync($"/api/properties/{property.Id}/billing-cycles", new { billingMonth = "2026-02-01" })).StatusCode);

        var firstKey = Guid.NewGuid();
        var first = await ReadAsync<CollectionReportResponse>(await manager.PostAsJsonAsync($"/api/properties/{property.Id}/collection-reports", new
        {
            billingUnitBillId = approved.Bills.Single().Id,
            amountReceived = 50m,
            receivedDate = "2026-01-10",
            paymentMethod = "Cash",
            referenceNote = "Cash receipt book 1",
            submissionKey = firstKey,
        }));
        Assert.Equal(CollectionReportStatus.Pending, first.Status);
        Assert.Null(first.TenantName);
        Assert.Equal(0m, first.ConfirmedCollectedAmount);
        Assert.Equal(300m, first.RemainingBalance);

        var duplicate = await ReadAsync<CollectionReportResponse>(await manager.PostAsJsonAsync($"/api/properties/{property.Id}/collection-reports", new
        {
            billingUnitBillId = approved.Bills.Single().Id,
            amountReceived = 50m,
            receivedDate = "2026-01-10",
            paymentMethod = "Cash",
            referenceNote = "Retry should be idempotent",
            submissionKey = firstKey,
        }));
        Assert.Equal(first.Id, duplicate.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync($"/api/collection-reports/{first.Id}/approve", new { expectedVersion = first.Version })).StatusCode);
        var returned = await ReadAsync<CollectionReportResponse>(await finance.PostAsJsonAsync($"/api/collection-reports/{first.Id}/return", new { expectedVersion = first.Version, reason = "Attach the signed receipt reference." }));
        Assert.Equal(CollectionReportStatus.Returned, returned.Status);
        var corrected = await ReadAsync<CollectionReportResponse>(await manager.PutAsJsonAsync($"/api/collection-reports/{first.Id}", new
        {
            expectedVersion = returned.Version,
            amountReceived = 40m,
            receivedDate = "2026-01-11",
            paymentMethod = "BankTransfer",
            referenceNote = "Bank slip 2",
            reason = "Corrected receipt amount",
        }));
        Assert.Equal(CollectionReportStatus.Pending, corrected.Status);
        Assert.Equal(40m, corrected.AmountReceived);
        var firstApproved = await ReadAsync<CollectionReportResponse>(await finance.PostAsJsonAsync($"/api/collection-reports/{first.Id}/approve", new { expectedVersion = corrected.Version }));
        Assert.Equal(CollectionReportStatus.Approved, firstApproved.Status);
        Assert.Equal(40m, firstApproved.ConfirmedCollectedAmount);
        Assert.Equal(260m, firstApproved.RemainingBalance);

        var pendingLarge = await CreateReportAsync(manager, property.Id, approved.Bills.Single().Id, 170m);
        var pendingSecond = await CreateReportAsync(manager, property.Id, approved.Bills.Single().Id, 100m);
        var queueWhilePending = await ReadAsync<CollectionQueueResponse>(await finance.GetAsync($"/api/properties/{property.Id}/collection-reports/2026-01"));
        Assert.Equal(40m, queueWhilePending.ConfirmedCollectedTotal);
        Assert.Equal(270m, queueWhilePending.PendingReportedTotal);
        Assert.Equal(260m, queueWhilePending.OutstandingTotal);

        using var secondFinance = factory.CreateClient();
        var secondFinanceInvitation = await ReadAsync<InvitationResponse>(await admin.PostAsJsonAsync("/api/staff/invitations", new { email = $"finance-2-{suffix}@example.test", role = "FinanceManager" }));
        var secondFinanceUser = await AcceptInvitationAsync(secondFinance, secondFinanceInvitation.InvitationToken, "Second Finance Manager");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/properties/{property.Id}/staff/{secondFinanceUser.Id}", null)).StatusCode);
        var largeApproved = await ReadAsync<CollectionReportResponse>(await finance.PostAsJsonAsync($"/api/collection-reports/{pendingLarge.Id}/approve", new { expectedVersion = pendingLarge.Version }));
        Assert.Equal(210m, largeApproved.ConfirmedCollectedAmount);
        Assert.Equal(HttpStatusCode.Conflict, (await secondFinance.PostAsJsonAsync($"/api/collection-reports/{pendingLarge.Id}/approve", new { expectedVersion = pendingLarge.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await finance.PostAsJsonAsync($"/api/collection-reports/{pendingSecond.Id}/approve", new { expectedVersion = pendingSecond.Version })).StatusCode);

        var adminReport = await ReadAsync<CollectionReportResponse>(await admin.PostAsJsonAsync($"/api/properties/{property.Id}/collection-reports", new
        {
            billingUnitBillId = approved.Bills.Single().Id,
            amountReceived = 90m,
            receivedDate = "2026-01-20",
            paymentMethod = "MobilePayment",
            referenceNote = "Admin-entered receipt",
            submissionKey = Guid.NewGuid(),
        }));
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/collection-reports/{adminReport.Id}/approve", new { expectedVersion = adminReport.Version })).StatusCode);
        factory.FailNextSave = true;
        Assert.Equal(HttpStatusCode.InternalServerError, (await finance.PostAsJsonAsync($"/api/collection-reports/{adminReport.Id}/approve", new { expectedVersion = adminReport.Version })).StatusCode);
        var afterFailedApproval = await ReadAsync<CollectionQueueResponse>(await finance.GetAsync($"/api/properties/{property.Id}/collection-reports/2026-01"));
        var stillPending = afterFailedApproval.Items.Single(item => item.Id == adminReport.Id);
        Assert.Equal(CollectionReportStatus.Pending, stillPending.Status);
        Assert.Equal(210m, afterFailedApproval.ConfirmedCollectedTotal);
        Assert.DoesNotContain(stillPending.Audit, audit => audit.Action == CollectionAuditAction.Approved);
        var finalAdminApproval = await ReadAsync<CollectionReportResponse>(await finance.PostAsJsonAsync($"/api/collection-reports/{adminReport.Id}/approve", new { expectedVersion = stillPending.Version }));
        Assert.Equal(CollectionReportStatus.Approved, finalAdminApproval.Status);
        Assert.Equal(300m, finalAdminApproval.ConfirmedCollectedAmount);
        Assert.Equal(0m, finalAdminApproval.RemainingBalance);

        var reversalRequested = await ReadAsync<CollectionReportResponse>(await manager.PostAsJsonAsync($"/api/collection-reports/{first.Id}/reversal", new { expectedVersion = firstApproved.Version, reason = "Reverse the duplicate cash receipt entry." }));
        Assert.Equal(CollectionReversalStatus.Pending, reversalRequested.ReversalStatus);
        var reversalReturned = await ReadAsync<CollectionReportResponse>(await finance.PostAsJsonAsync($"/api/collection-reversals/{reversalRequested.ReversalId}/return", new { expectedVersion = reversalRequested.ReversalVersion, reason = "Explain the reversal reference." }));
        Assert.Equal(CollectionReversalStatus.Returned, reversalReturned.ReversalStatus);
        var reversalResubmitted = await ReadAsync<CollectionReportResponse>(await manager.PostAsJsonAsync($"/api/collection-reversals/{reversalReturned.ReversalId}/resubmit", new { expectedVersion = reversalReturned.ReversalVersion, reason = "Reversal reference documented." }));
        var reversalApproved = await ReadAsync<CollectionReportResponse>(await finance.PostAsJsonAsync($"/api/collection-reversals/{reversalResubmitted.ReversalId}/approve", new { expectedVersion = reversalResubmitted.ReversalVersion }));
        Assert.True(reversalApproved.IsReversed);
        Assert.Equal(260m, reversalApproved.ConfirmedCollectedAmount);
        Assert.Equal(40m, reversalApproved.RemainingBalance);
        Assert.Contains(reversalApproved.Audit, audit => audit.Action == CollectionAuditAction.ReversalApproved);

        using var otherOrganization = factory.CreateClient();
        await RegisterAsync(otherOrganization, $"other-collections-{suffix}@example.test", "Other Admin", $"Other Organization {suffix}");
        Assert.Equal(HttpStatusCode.NotFound, (await otherOrganization.GetAsync($"/api/properties/{property.Id}/collection-reports/2026-01")).StatusCode);
    }

    private async Task<CollectionReportResponse> CreateReportAsync(HttpClient client, Guid propertyId, Guid billId, decimal amount)
    {
        return await ReadAsync<CollectionReportResponse>(await client.PostAsJsonAsync($"/api/properties/{propertyId}/collection-reports", new
        {
            billingUnitBillId = billId,
            amountReceived = amount,
            receivedDate = "2026-01-15",
            paymentMethod = "Other",
            referenceNote = "Pending manual report",
            submissionKey = Guid.NewGuid(),
        }));
    }

    private static async Task RegisterAsync(HttpClient client, string email, string fullName, string organizationName)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/register-owner", new
        {
            organizationName,
            fullName,
            email,
            password = "StrongPass!123",
        })).StatusCode);
    }

    private static async Task<UserResponse> AcceptInvitationAsync(HttpClient client, string token, string fullName)
    {
        return await ReadAsync<UserResponse>(await client.PostAsJsonAsync("/api/auth/accept-invitation", new
        {
            token,
            fullName,
            password = "StrongPass!123",
        }));
    }

    private static async Task SaveChargeAsync(HttpClient client, Guid propertyId, decimal amount)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/properties/{propertyId}/charges", new
        {
            items = new[] { new { chargeType = "ServiceCharge", mode = "Custom", billingMonth = "2026-01-01", amount = (decimal?)amount, customLabel = (string?)null } },
        })).StatusCode);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private sealed record UserResponse(Guid Id, string Email, string FullName, UserRole Role, Guid OrganizationId);
    private sealed record InvitationResponse(Guid Id, string Email, UserRole Role, DateTimeOffset ExpiresAtUtc, string InvitationToken, string DeliveryStatus);
    private sealed record PropertyResponse(Guid Id, string Name, string Address, PropertyType PropertyType, bool IsArchived, List<FloorResponse> Floors);
    private sealed record FloorResponse(Guid Id, string Name, int SortOrder, bool IsArchived, List<UnitResponse> Units);
    private sealed record UnitResponse(Guid Id, string Name, bool IsEnabled, bool IsArchived, OccupancyStatus OccupancyStatus);
    private sealed record TenantResponse(Guid Id, string? Name, string? Phone, OnboardingStatus OnboardingStatus);
    private sealed record BillingCycleResponse(Guid Id, Guid PropertyId, DateOnly BillingMonth, BillingCycleStatus Status, long Version, Guid LastChangedByUserId, DateTimeOffset LastChangedAtUtc, string? ReturnReason, decimal DraftTotal, decimal? ApprovedTotal, int BlockingBillCount, List<BillingUnitBillResponse> Bills, List<BillingAuditResponse> Audit);
    private sealed record BillingUnitBillResponse(Guid Id, Guid UnitId, string UnitName, Guid? OccupancyAssignmentId, Guid? TenantId, string? TenantName, string? TenantPhone, DateOnly? OccupancyStartDate, DateOnly? OccupancyEndDate, string? BlockingReason, decimal Total, bool ElectricityWarning, List<object> Charges);
    private sealed record BillingAuditResponse(Guid Id, Guid ActorUserId, BillingAuditAction Action, DateTimeOffset OccurredAtUtc, string? Reason, string? Before, string? After);
    private sealed record CollectionReportResponse(Guid Id, Guid BillingUnitBillId, Guid BillingCycleId, Guid PropertyId, DateOnly BillingMonth, Guid UnitId, string UnitName, string? FloorName, Guid? OccupancyAssignmentId, Guid? TenantId, string? TenantName, string? TenantPhone, decimal BilledAmount, decimal ConfirmedCollectedAmount, decimal RemainingBalance, decimal AmountReceived, DateOnly ReceivedDate, PaymentMethod PaymentMethod, string? ReferenceNote, Guid ReporterUserId, string ReporterName, DateTimeOffset SubmittedAtUtc, CollectionReportStatus Status, long Version, string? ReturnReason, Guid? ApprovedByUserId, DateTimeOffset? ApprovedAtUtc, bool IsReversed, CollectionReversalStatus? ReversalStatus, Guid? ReversalId, long? ReversalVersion, string? ReversalReason, List<CollectionAuditResponse> Audit);
    private sealed record CollectionAuditResponse(Guid Id, Guid? CollectionReversalId, Guid ActorUserId, CollectionAuditAction Action, DateTimeOffset OccurredAtUtc, string? Reason, string? Before, string? After);
    private sealed record CollectionQueueResponse(List<CollectionReportResponse> Items, decimal BilledTotal, decimal PendingReportedTotal, decimal ConfirmedCollectedTotal, decimal OutstandingTotal);
    private sealed record MeterOverviewResponse(Guid PropertyId, DateOnly BillingMonth, Guid? BillingCycleId, BillingCycleStatus? BillingCycleStatus, List<MeterRoomOverviewResponse> Units);
    private sealed record MeterRoomOverviewResponse(Guid UnitId, string UnitName, string? FloorName, bool ElectricityApplicable, decimal? PreviousReading, DateOnly? PreviousReadingDate, decimal? CurrentReading, DateOnly? CurrentReadingDate, decimal? ConsumptionUnits, decimal? ElectricityAmount, decimal? ElectricityRate, string ReadingStatus, string? BlockingReason, int ReportedIssueCount, int OpenIssueCount);
    private sealed record CollectionOverviewResponse(Guid PropertyId, DateOnly BillingMonth, Guid? BillingCycleId, string BillingCoverage, List<CollectionUnitOverviewResponse> Units, decimal BilledTotal, decimal PendingReportedTotal, decimal ConfirmedCollectedTotal, decimal OutstandingTotal, decimal PendingReversalTotal);
    private sealed record CollectionUnitOverviewResponse(Guid UnitId, string UnitName, string? FloorName, Guid? BillingUnitBillId, Guid? OccupancyAssignmentId, Guid? TenantId, string? TenantName, decimal? BilledAmount, decimal ConfirmedCollectedAmount, decimal PendingReportedAmount, decimal PendingReversalAmount, decimal? RemainingBalance, string Status);
    private sealed record RoomMeterIssueResponse(Guid Id, Guid PropertyId, Guid UnitId, string UnitName, string? FloorName, Guid ReporterUserId, string ReporterName, RoomMeterIssueType IssueType, string Description, DateOnly ObservedDate, DateTimeOffset CreatedAtUtc, RoomMeterIssueStatus Status, long Version, string? ResolutionReason, List<RoomMeterIssueAuditResponse> Audit);
    private sealed record RoomMeterIssueAuditResponse(Guid Id, Guid ActorUserId, RoomMeterIssueAuditAction Action, DateTimeOffset OccurredAtUtc, string? Reason, string? Before, string? After);
}
