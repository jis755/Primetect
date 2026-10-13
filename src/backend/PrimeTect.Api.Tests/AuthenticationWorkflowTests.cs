using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PrimeTect.Infrastructure.Identity;
using PrimeTect.Infrastructure.Persistence;
using Xunit;

namespace PrimeTect.Api.Tests;

public sealed class AuthenticationWorkflowTests : IClassFixture<PrimeTectApiFactory>
{
    private readonly PrimeTectApiFactory factory;

    public AuthenticationWorkflowTests(PrimeTectApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Admin_assigns_a_phone_that_logs_in_with_local_international_and_bangla_forms()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var adminPassword = "StrongPass!123";
        await RegisterOwnerAsync(admin, suffix, adminPassword);
        var (_, managerId, managerName, managerEmail) = await InviteAndAcceptAsync(admin, "Manager", suffix, "StrongPass!456");

        var phone = UniquePhone();
        var canonical = "+880" + phone[1..];
        var assigned = await AssignPhoneAsync(admin, managerId, managerName, adminPassword, phone, "BD");
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        using var phoneJson = JsonDocument.Parse(await assigned.Content.ReadAsStringAsync());
        Assert.Equal(canonical, phoneJson.RootElement.GetProperty("phoneNumber").GetString());
        Assert.False(phoneJson.RootElement.GetProperty("phoneNumberConfirmed").GetBoolean());
        Assert.True(phoneJson.RootElement.GetProperty("adminAssigned").GetBoolean());

        using var emailClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await emailClient.PostAsJsonAsync("/api/auth/login", new { email = managerEmail, password = "StrongPass!456" })).StatusCode);
        using var localClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await localClient.PostAsJsonAsync("/api/auth/login", new { email = phone, password = "StrongPass!456", countryCode = "BD" })).StatusCode);
        using var internationalClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await internationalClient.PostAsJsonAsync("/api/auth/login", new { email = canonical, password = "StrongPass!456" })).StatusCode);
        using var banglaClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await banglaClient.PostAsJsonAsync("/api/auth/login", new { email = ToBanglaDigits(phone), password = "StrongPass!456", countryCode = "BD" })).StatusCode);
    }

    [Fact]
    public async Task Invalid_or_unassigned_phone_logins_fail_generically()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = UniquePhone(), password = "Whatever!123", countryCode = "BD" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "not-a-phone", password = "Whatever!123", countryCode = "BD" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "017123", password = "Whatever!123", countryCode = "BD" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "01712345678", password = "Whatever!123" })).StatusCode);
    }

    [Fact]
    public async Task Duplicate_phone_assignment_is_rejected()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var adminPassword = "StrongPass!123";
        await RegisterOwnerAsync(admin, suffix, adminPassword);
        var (_, firstId, firstName, _) = await InviteAndAcceptAsync(admin, "Manager", suffix + "a", "StrongPass!456");
        var (_, secondId, secondName, _) = await InviteAndAcceptAsync(admin, "BuildingStaff", suffix + "b", "StrongPass!456");

        var phone = UniquePhone();
        Assert.Equal(HttpStatusCode.OK, (await AssignPhoneAsync(admin, firstId, firstName, adminPassword, phone, "BD")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await AssignPhoneAsync(admin, secondId, secondName, adminPassword, phone, "BD")).StatusCode);
    }

    [Fact]
    public async Task Phone_assignment_requires_admin_current_password_and_org_scope()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var adminPassword = "StrongPass!123";
        await RegisterOwnerAsync(admin, suffix, adminPassword);
        var (manager, managerId, managerName, _) = await InviteAndAcceptAsync(admin, "Manager", suffix, "StrongPass!456");

        Assert.Equal(HttpStatusCode.Forbidden, (await AssignPhoneAsync(manager, managerId, managerName, "StrongPass!456", UniquePhone(), "BD")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AssignPhoneAsync(admin, managerId, managerName, "WrongPass!123", UniquePhone(), "BD")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/staff/{managerId}/phone", new { actingPassword = adminPassword, phoneNumber = UniquePhone(), countryCode = "BD", confirmFullName = "Someone Else", reason = "x" })).StatusCode);

        using var otherAdmin = factory.CreateClient();
        await RegisterOwnerAsync(otherAdmin, "other" + suffix, adminPassword);
        Assert.Equal(HttpStatusCode.NotFound, (await AssignPhoneAsync(otherAdmin, managerId, managerName, adminPassword, UniquePhone(), "BD")).StatusCode);
    }

    [Fact]
    public async Task Phone_change_invalidates_the_targets_previous_sessions_immediately()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var adminPassword = "StrongPass!123";
        await RegisterOwnerAsync(admin, suffix, adminPassword);
        var (staff, staffId, staffName, _) = await InviteAndAcceptAsync(admin, "BuildingStaff", suffix, "StrongPass!456");
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/auth/me")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await AssignPhoneAsync(admin, staffId, staffName, adminPassword, UniquePhone(), "BD")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Password_reset_returns_a_one_time_temporary_password_without_persisting_it()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var adminPassword = "StrongPass!123";
        await RegisterOwnerAsync(admin, suffix, adminPassword);
        var (_, staffId, staffName, _) = await InviteAndAcceptAsync(admin, "BuildingStaff", suffix, "StrongPass!456");

        var reset = await ResetPasswordAsync(admin, staffId, staffName, adminPassword, "Forgot password", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.True(reset.Headers.CacheControl?.NoStore, "reset response must be no-store");
        using var resetJson = JsonDocument.Parse(await reset.Content.ReadAsStringAsync());
        var temporary = resetJson.RootElement.GetProperty("temporaryPassword").GetString();
        Assert.False(string.IsNullOrWhiteSpace(temporary));
        var expires = resetJson.RootElement.GetProperty("expiresAtUtc").GetDateTimeOffset();
        Assert.InRange(expires, DateTimeOffset.UtcNow.AddHours(23), DateTimeOffset.UtcNow.AddHours(25));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropertyManagementDbContext>();
        var leaked = await db.StaffCredentialAuditEntries.Where(entry => (entry.BeforeJson ?? "").Contains(temporary!) || (entry.AfterJson ?? "").Contains(temporary!)).ToListAsync();
        Assert.Empty(leaked);
    }

    [Fact]
    public async Task Temporary_login_is_restricted_and_a_valid_change_restores_normal_login()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var adminPassword = "StrongPass!123";
        await RegisterOwnerAsync(admin, suffix, adminPassword);
        var (_, staffId, staffName, staffEmail) = await InviteAndAcceptAsync(admin, "BuildingStaff", suffix, "StrongPass!456");
        var temporary = await ResetAndReadTemporaryAsync(admin, staffId, staffName, adminPassword);

        using var staff = factory.CreateClient();
        var login = await staff.PostAsJsonAsync("/api/auth/login", new { email = staffEmail, password = temporary });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        Assert.True(loginJson.RootElement.GetProperty("recoveryOnly").GetBoolean());

        using var me = JsonDocument.Parse(await (await staff.GetAsync("/api/auth/me")).Content.ReadAsStringAsync());
        Assert.True(me.RootElement.GetProperty("passwordResetRequired").GetBoolean());

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/properties")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync($"/api/meter-photos/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/staff")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync("/api/auth/change-temporary-password", new { currentPassword = temporary, newPassword = "short" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync("/api/auth/change-temporary-password", new { currentPassword = temporary, newPassword = temporary })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync("/api/auth/change-temporary-password", new { currentPassword = temporary, newPassword = "FreshPass!987" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/auth/me")).StatusCode);

        using var relogin = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await relogin.PostAsJsonAsync("/api/auth/login", new { email = staffEmail, password = "FreshPass!987" })).StatusCode);
        using var oldTemporary = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldTemporary.PostAsJsonAsync("/api/auth/login", new { email = staffEmail, password = temporary })).StatusCode);
    }

    [Fact]
    public async Task Expired_temporary_password_is_rejected()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var adminPassword = "StrongPass!123";
        await RegisterOwnerAsync(admin, suffix, adminPassword);
        var (_, staffId, staffName, staffEmail) = await InviteAndAcceptAsync(admin, "BuildingStaff", suffix, "StrongPass!456");
        var temporary = await ResetAndReadTemporaryAsync(admin, staffId, staffName, adminPassword);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await userManager.FindByIdAsync(staffId.ToString());
            user!.TemporaryPasswordExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
            await userManager.UpdateAsync(user);
        }

        using var expired = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await expired.PostAsJsonAsync("/api/auth/login", new { email = staffEmail, password = temporary })).StatusCode);
    }

    [Fact]
    public async Task Reissue_invalidates_the_earlier_temporary_password_and_duplicate_keys_are_rejected()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var adminPassword = "StrongPass!123";
        await RegisterOwnerAsync(admin, suffix, adminPassword);
        var (_, staffId, staffName, staffEmail) = await InviteAndAcceptAsync(admin, "BuildingStaff", suffix, "StrongPass!456");

        var firstKey = Guid.NewGuid();
        var temporary1 = await ResetAndReadTemporaryAsync(admin, staffId, staffName, adminPassword, firstKey);
        var temporary2 = await ResetAndReadTemporaryAsync(admin, staffId, staffName, adminPassword, Guid.NewGuid());
        Assert.NotEqual(temporary1, temporary2);

        using var firstClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await firstClient.PostAsJsonAsync("/api/auth/login", new { email = staffEmail, password = temporary1 })).StatusCode);
        using var secondClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await secondClient.PostAsJsonAsync("/api/auth/login", new { email = staffEmail, password = temporary2 })).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await ResetPasswordAsync(admin, staffId, staffName, adminPassword, "again", firstKey)).StatusCode);
    }

    [Fact]
    public async Task Password_reset_invalidates_previous_sessions()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var adminPassword = "StrongPass!123";
        await RegisterOwnerAsync(admin, suffix, adminPassword);
        var (staff, staffId, staffName, _) = await InviteAndAcceptAsync(admin, "BuildingStaff", suffix, "StrongPass!456");
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/auth/me")).StatusCode);

        await ResetAndReadTemporaryAsync(admin, staffId, staffName, adminPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Reset_atomic_failure_records_no_change()
    {
        using var admin = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var adminPassword = "StrongPass!123";
        await RegisterOwnerAsync(admin, suffix, adminPassword);
        var (_, staffId, staffName, staffEmail) = await InviteAndAcceptAsync(admin, "BuildingStaff", suffix, "StrongPass!456");
        var submissionKey = Guid.NewGuid();

        factory.FailNextSave = true;
        var failed = await ResetPasswordAsync(admin, staffId, staffName, adminPassword, "atomic", submissionKey);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);

        using var stillValid = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await stillValid.PostAsJsonAsync("/api/auth/login", new { email = staffEmail, password = "StrongPass!456" })).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PropertyManagementDbContext>();
        Assert.False(await db.StaffCredentialAuditEntries.AnyAsync(entry => entry.SubmissionKey == submissionKey));
    }

    private async Task RegisterOwnerAsync(HttpClient client, string suffix, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register-owner", new
        {
            organizationName = $"Auth Org {suffix}",
            fullName = "Auth Admin",
            email = $"admin-{suffix}@example.test",
            password,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

    private static Task<HttpResponseMessage> AssignPhoneAsync(HttpClient client, Guid userId, string confirmFullName, string actingPassword, string phone, string countryCode) =>
        client.PostAsJsonAsync($"/api/staff/{userId}/phone", new { actingPassword, phoneNumber = phone, countryCode, confirmFullName, reason = "Assign login phone" });

    private static Task<HttpResponseMessage> ResetPasswordAsync(HttpClient client, Guid userId, string confirmFullName, string actingPassword, string reason, Guid submissionKey) =>
        client.PostAsJsonAsync($"/api/staff/{userId}/reset-password", new { actingPassword, confirmFullName, reason, submissionKey });

    private async Task<string> ResetAndReadTemporaryAsync(HttpClient admin, Guid staffId, string staffName, string adminPassword, Guid? submissionKey = null)
    {
        var reset = await ResetPasswordAsync(admin, staffId, staffName, adminPassword, "Reset for test", submissionKey ?? Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        using var json = JsonDocument.Parse(await reset.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("temporaryPassword").GetString()!;
    }

    private static string UniquePhone() => "017" + Random.Shared.Next(10_000_000, 99_999_999);

    private static string ToBanglaDigits(string value) => new(value.Select(character => character is >= '0' and <= '9' ? (char)('০' + (character - '0')) : character).ToArray());
}
