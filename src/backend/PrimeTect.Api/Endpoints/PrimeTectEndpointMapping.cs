using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrimeTect.Api.Contracts;
using PrimeTect.Api.Security;
using PrimeTect.Domain.Entities;
using PrimeTect.Domain.Enums;
using PrimeTect.Infrastructure.Identity;
using PrimeTect.Infrastructure.Persistence;

namespace PrimeTect.Api.Endpoints;

public static class PrimeTectEndpointMapping
{
    public static void MapPrimeTectEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/auth/register-owner", (Delegate)RegisterOwnerAsync);
        api.MapPost("/auth/login", (Delegate)LoginAsync);
        api.MapPost("/auth/accept-invitation", (Delegate)AcceptInvitationAsync);

        var secure = api.MapGroup(string.Empty).RequireAuthorization();
        secure.MapGet("/auth/me", (Delegate)GetCurrentUserAsync);
        secure.MapPost("/auth/logout", (Delegate)LogoutAsync);
        secure.MapGet("/properties", (Delegate)GetPropertiesAsync);
        secure.MapGet("/properties/{propertyId:guid}", (Delegate)GetPropertyAsync);
        secure.MapPost("/properties/preview", (Delegate)PreviewProperty).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
        secure.MapPost("/properties", (Delegate)CreatePropertyAsync);
        secure.MapPut("/properties/{propertyId:guid}", (Delegate)UpdatePropertyAsync);
        secure.MapPost("/properties/{propertyId:guid}/floors", (Delegate)AddFloorAsync);
        secure.MapPut("/properties/{propertyId:guid}/floors/{floorId:guid}", (Delegate)UpdateFloorAsync);
        secure.MapPut("/properties/{propertyId:guid}/units/{unitId:guid}", (Delegate)UpdateUnitAsync);
        secure.MapPut("/properties/{propertyId:guid}/units/{unitId:guid}/occupancy-status", (Delegate)UpdateOccupancyStatusAsync);
        secure.MapGet("/staff", (Delegate)GetStaffAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
        secure.MapPost("/staff/invitations", (Delegate)CreateInvitationAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
        secure.MapPost("/properties/{propertyId:guid}/staff/{userId:guid}", (Delegate)AssignStaffAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
        secure.MapDelete("/properties/{propertyId:guid}/staff/{userId:guid}", (Delegate)RemoveStaffAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
        secure.MapGet("/tenants", (Delegate)GetTenantsAsync);
        secure.MapPost("/tenants", (Delegate)CreateTenantAsync);
        secure.MapPut("/tenants/{tenantId:guid}", (Delegate)UpdateTenantAsync);
        secure.MapGet("/properties/{propertyId:guid}/units/{unitId:guid}/occupancy", (Delegate)GetOccupancyHistoryAsync);
        secure.MapPost("/properties/{propertyId:guid}/units/{unitId:guid}/occupancy", (Delegate)AddOccupancyAsync);
        secure.MapPut("/properties/{propertyId:guid}/units/{unitId:guid}/occupancy/{assignmentId:guid}/close", (Delegate)CloseOccupancyAsync);
        secure.MapGet("/properties/{propertyId:guid}/charges", (Delegate)GetPropertyChargesAsync);
        secure.MapPut("/properties/{propertyId:guid}/charges", (Delegate)PutPropertyChargesAsync);
        secure.MapPost("/properties/{propertyId:guid}/charges/{chargeType}/end", (Delegate)EndPropertyChargeAsync);
        secure.MapGet("/properties/{propertyId:guid}/units/{unitId:guid}/charges", (Delegate)GetUnitChargesAsync);
        secure.MapPut("/properties/{propertyId:guid}/units/{unitId:guid}/charges", (Delegate)PutUnitChargesAsync);
        secure.MapPost("/properties/{propertyId:guid}/units/{unitId:guid}/charges/{chargeType}/end", (Delegate)EndUnitChargeAsync);
        secure.MapPost("/properties/{propertyId:guid}/billing-cycles", (Delegate)GenerateBillingCycleAsync);
        secure.MapGet("/properties/{propertyId:guid}/billing-cycles/{billingMonth}", (Delegate)GetBillingCycleAsync);
        secure.MapGet("/properties/{propertyId:guid}/billing-reports/{billingMonth}/units/{unitId:guid}", (Delegate)GetUnitBillingStatementAsync);
        secure.MapGet("/properties/{propertyId:guid}/billing-reports/{billingMonth}/tenants/{tenantId:guid}", (Delegate)GetTenantBillingStatementAsync);
        secure.MapGet("/properties/{propertyId:guid}/billing-reports/{billingMonth}/property-summary", (Delegate)GetPropertyBillingSummaryAsync);
        secure.MapGet("/properties/{propertyId:guid}/billing-reports/{billingMonth}/comparison", (Delegate)GetMonthlyBillingComparisonAsync);
        secure.MapPut("/properties/{propertyId:guid}/billing-cycles/{billingCycleId:guid}/meter-readings", (Delegate)SaveBillingReadingsAsync);
        secure.MapPost("/properties/{propertyId:guid}/billing-cycles/{billingCycleId:guid}/submit", (Delegate)SubmitBillingCycleAsync);
        secure.MapPost("/properties/{propertyId:guid}/billing-cycles/{billingCycleId:guid}/return", (Delegate)ReturnBillingCycleAsync);
        secure.MapPost("/properties/{propertyId:guid}/billing-cycles/{billingCycleId:guid}/approve", (Delegate)ApproveBillingCycleAsync);
        secure.MapGet("/properties/{propertyId:guid}/meter-overview/{billingMonth}", (Delegate)GetMeterOverviewAsync);
        secure.MapGet("/properties/{propertyId:guid}/room-meter-issues", (Delegate)GetRoomMeterIssuesAsync);
        secure.MapPost("/properties/{propertyId:guid}/units/{unitId:guid}/room-meter-issues", (Delegate)CreateRoomMeterIssueAsync);
        secure.MapPost("/properties/{propertyId:guid}/feedback-reports", (Delegate)CreateFeedbackReportAsync);
        secure.MapPost("/room-meter-issues/{issueId:guid}/acknowledge", (Delegate)AcknowledgeRoomMeterIssueAsync);
        secure.MapPost("/room-meter-issues/{issueId:guid}/resolve", (Delegate)ResolveRoomMeterIssueAsync);
        secure.MapGet("/notifications", (Delegate)GetNotificationsAsync);
        secure.MapPost("/notifications/{notificationId:guid}/read", (Delegate)MarkNotificationReadAsync);
        secure.MapGet("/properties/{propertyId:guid}/collection-reports/{billingMonth}", (Delegate)GetCollectionReportsAsync);
        secure.MapGet("/properties/{propertyId:guid}/collection-overview/{billingMonth}", (Delegate)GetCollectionOverviewAsync);
        secure.MapPost("/properties/{propertyId:guid}/collection-reports", (Delegate)CreateCollectionReportAsync);
        secure.MapPut("/collection-reports/{reportId:guid}", (Delegate)CorrectCollectionReportAsync);
        secure.MapPost("/collection-reports/{reportId:guid}/return", (Delegate)ReturnCollectionReportAsync);
        secure.MapPost("/collection-reports/{reportId:guid}/approve", (Delegate)ApproveCollectionReportAsync);
        secure.MapPost("/collection-reports/{reportId:guid}/reversal", (Delegate)RequestCollectionReversalAsync);
        secure.MapPost("/collection-reversals/{reversalId:guid}/resubmit", (Delegate)ResubmitCollectionReversalAsync);
        secure.MapPost("/collection-reversals/{reversalId:guid}/return", (Delegate)ReturnCollectionReversalAsync);
        secure.MapPost("/collection-reversals/{reversalId:guid}/approve", (Delegate)ApproveCollectionReversalAsync);
        secure.MapGet("/role-permissions", (Delegate)GetRolePermissionsAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
        secure.MapPut("/role-permissions", (Delegate)UpdateRolePermissionsAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
    }

    private static async Task<IResult> RegisterOwnerAsync(
        OwnerRegistrationRequest request,
        PropertyManagementDbContext db,
        UserManager<AppUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationName) || string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest("Organization name and full name are required.");
        }

        if (!new EmailAddressAttribute().IsValid(request.Email))
        {
            return BadRequest("A valid email address is required.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var organization = new Organization(request.OrganizationName);
        db.Organizations.Add(organization);
        await OrganizationPermissionAuthorization.EnsureDefaultsAsync(db, organization.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var user = NewUser(organization.Id, request.Email, request.FullName, UserRole.Admin);
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.ValidationProblem(IdentityErrors(createResult));
        }

        var claimResult = await AddUserClaimsAsync(userManager, user);
        if (!claimResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.ValidationProblem(IdentityErrors(claimResult));
        }

        await transaction.CommitAsync(cancellationToken);
        await AuthenticationCookie.SignInAsync(httpContext, user);

        return Results.Ok(new
        {
            user = ToUserResponse(user),
            organization = new { organization.Id, organization.Name },
        });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email and password are required.");
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return Results.Unauthorized();
        }

        var result = await signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return Results.Unauthorized();
        }

        await AuthenticationCookie.SignInAsync(httpContext, user, request.RememberMe);
        return Results.Ok(ToUserResponse(user));
    }

    private static async Task<IResult> AcceptInvitationAsync(
        AcceptInvitationRequest request,
        PropertyManagementDbContext db,
        UserManager<AppUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest("Invitation token and full name are required.");
        }

        var tokenHash = SecurityTokens.Hash(request.Token);
        var invitation = await db.UserInvitations.FirstOrDefaultAsync(item =>
            item.TokenHash == tokenHash && item.AcceptedAtUtc == null && item.ExpiresAtUtc > DateTimeOffset.UtcNow,
            cancellationToken);
        if (invitation is null)
        {
            return Results.BadRequest(new { message = "The invitation is invalid or expired." });
        }

        if (invitation.Role is not (UserRole.Admin or UserRole.Manager or UserRole.BuildingStaff or UserRole.FinanceManager))
        {
            return Results.BadRequest(new { message = "This invitation role is not allowed." });
        }

        if (await userManager.FindByEmailAsync(invitation.Email) is not null)
        {
            return Results.Conflict(new { message = "An account already exists for this invitation." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = NewUser(invitation.OrganizationId, invitation.Email, request.FullName, invitation.Role);
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.ValidationProblem(IdentityErrors(createResult));
        }

        var claimResult = await AddUserClaimsAsync(userManager, user);
        if (!claimResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.ValidationProblem(IdentityErrors(claimResult));
        }

        invitation.MarkAccepted();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await AuthenticationCookie.SignInAsync(httpContext, user);

        return Results.Ok(ToUserResponse(user));
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext httpContext,
        UserManager<AppUser> userManager,
        PropertyManagementDbContext db)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(actor.UserId.ToString());
        var organization = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(item => item.Id == actor.OrganizationId);
        return user is null || organization is null
            ? Results.Unauthorized()
            : Results.Ok(new
            {
                user = ToUserResponse(user),
                organization = new { organization.Id, organization.Name },
                permissions = (await OrganizationPermissionAuthorization.GetAsync(db, actor, CancellationToken.None))
                    .ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
            });
    }

    private static async Task<IResult> GetRolePermissionsAsync(
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        await OrganizationPermissionAuthorization.EnsureDefaultsAsync(db, actor.OrganizationId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        var rows = await db.OrganizationRolePermissions.AsNoTracking()
            .Where(item => item.OrganizationId == actor.OrganizationId)
            .ToListAsync(cancellationToken);
        return Results.Ok(new[] { UserRole.Manager, UserRole.BuildingStaff, UserRole.FinanceManager }
            .Select(role => new RolePermissionConfigurationResponse(
                role,
                Enum.GetValues<OrganizationPermission>().ToDictionary(
                    permission => permission.ToString(),
                    permission => rows.FirstOrDefault(item => item.Role == role && item.Permission == permission)?.IsEnabled
                        ?? OrganizationPermissionDefaults.ForRole(role)[permission])))
            .ToList());
    }

    private static async Task<IResult> UpdateRolePermissionsAsync(
        RolePermissionUpdateRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (request.Role == UserRole.Admin)
        {
            return BadRequest("Admin access is always protected and cannot be disabled here.");
        }

        if (request.Role is not (UserRole.Manager or UserRole.BuildingStaff or UserRole.FinanceManager))
        {
            return BadRequest("Only Building Manager, Building Staff, and Finance Manager permissions can be configured.");
        }

        var parsed = new Dictionary<OrganizationPermission, bool>();
        foreach (var pair in request.Permissions ?? new Dictionary<string, bool>())
        {
            if (!Enum.TryParse<OrganizationPermission>(pair.Key, ignoreCase: true, out var permission))
            {
                return BadRequest($"Unknown permission '{pair.Key}'.");
            }

            parsed[permission] = pair.Value;
        }

        await OrganizationPermissionAuthorization.EnsureDefaultsAsync(db, actor.OrganizationId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        var current = await db.OrganizationRolePermissions
            .Where(item => item.OrganizationId == actor.OrganizationId && item.Role == request.Role)
            .ToListAsync(cancellationToken);
        var before = current.ToDictionary(item => item.Permission.ToString(), item => item.IsEnabled);
        foreach (var permission in Enum.GetValues<OrganizationPermission>())
        {
            var row = current.First(item => item.Permission == permission);
            if (parsed.TryGetValue(permission, out var isEnabled))
            {
                row.SetEnabled(isEnabled);
            }
        }

        var effective = current.ToDictionary(item => item.Permission.ToString(), item => item.IsEnabled);
        var dependencyError = ValidatePermissionDependencies(effective);
        if (dependencyError is not null)
        {
            return BadRequest(dependencyError);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.OrganizationAuditEntries.Add(new OrganizationAuditEntry(
                actor.OrganizationId,
                actor.UserId,
                "RolePermissionsChanged",
                request.Reason,
                JsonSerializer.Serialize(before),
                JsonSerializer.Serialize(effective)));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new RolePermissionConfigurationResponse(request.Role, effective));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { message = "Role permissions changed concurrently. Refresh and try again." });
        }
    }

    private static string? ValidatePermissionDependencies(IReadOnlyDictionary<string, bool> permissions)
    {
        bool Enabled(OrganizationPermission permission) => permissions.GetValueOrDefault(permission.ToString());
        if (Enabled(OrganizationPermission.EditMeterReadings) && !Enabled(OrganizationPermission.ViewMeterReadings))
        {
            return "Entering readings requires permission to view meter readings.";
        }

        if (Enabled(OrganizationPermission.SubmitMeterReadings) && (!Enabled(OrganizationPermission.EditMeterReadings) || !Enabled(OrganizationPermission.ViewMeterReadings)))
        {
            return "Submitting readings requires both edit and view meter-reading permissions.";
        }

        if (Enabled(OrganizationPermission.ReportCollections) && !Enabled(OrganizationPermission.ViewApprovedBills))
        {
            return "Reporting collections requires permission to view approved bills.";
        }

        if ((Enabled(OrganizationPermission.ReviewCollections)
                || Enabled(OrganizationPermission.RequestCollectionReversals)
                || Enabled(OrganizationPermission.ReviewCollectionReversals)
                || Enabled(OrganizationPermission.ReviewBills))
            && !Enabled(OrganizationPermission.ViewApprovedBills))
        {
            return "Collection and bill review actions require permission to view approved bills.";
        }

        if (Enabled(OrganizationPermission.ReportRoomMeterIssues) && !Enabled(OrganizationPermission.ViewAssignedProperties))
        {
            return "Reporting room or meter issues requires permission to view assigned properties.";
        }

        if (Enabled(OrganizationPermission.ReviewRoomMeterIssues) && !Enabled(OrganizationPermission.ViewAssignedProperties))
        {
            return "Reviewing room or meter issues requires permission to view assigned properties.";
        }

        return null;
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> GetPropertiesAsync(
        bool? includeArchived,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ViewAssignedProperties, cancellationToken))
        {
            return Results.Forbid();
        }

        var include = includeArchived == true && actor.IsAdmin;
        var properties = await AccessibleProperties(db, actor, include).OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var responses = new List<PropertyResponse>();
        foreach (var property in properties)
        {
            responses.Add(await ToPropertyResponseAsync(db, property, include, cancellationToken));
        }

        return Results.Ok(responses);
    }

    private static async Task<IResult> GetPropertyAsync(
        Guid propertyId,
        bool? includeArchived,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ViewAssignedProperties, cancellationToken))
        {
            return Results.Forbid();
        }

        var include = includeArchived == true && actor.IsAdmin;
        var property = await AccessibleProperties(db, actor, include).FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        return property is null
            ? Results.NotFound()
            : Results.Ok(await ToPropertyResponseAsync(db, property, include, cancellationToken));
    }

    private static IResult PreviewProperty(PropertySetupRequest request)
    {
        var error = ValidatePropertyDraft(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        var floors = request.Floors.Select((floor, index) => new
        {
            name = floor.Name.Trim(),
            sortOrder = index,
            units = GetUnitNames(floor).Select((name, unitIndex) => new { name, sortOrder = unitIndex }).ToList(),
        }).ToList();
        return Results.Ok(new { request.Name, request.Address, propertyType = request.PropertyType, floors });
    }

    private static async Task<IResult> CreatePropertyAsync(
        PropertySetupRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageProperties, cancellationToken))
        {
            return Results.Forbid();
        }

        var error = ValidatePropertyDraft(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        var property = new Property(actor.OrganizationId, request.Name, request.Address, request.PropertyType, request.TimeZoneId);
        db.Properties.Add(property);
        for (var index = 0; index < request.Floors.Count; index++)
        {
            var floorDraft = request.Floors[index];
            var floor = new Floor(actor.OrganizationId, property.Id, floorDraft.Name, index);
            db.Floors.Add(floor);
            foreach (var unitName in GetUnitNames(floorDraft))
            {
                db.Units.Add(new Unit(actor.OrganizationId, property.Id, floor.Id, unitName));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/properties/{property.Id}", await ToPropertyResponseAsync(db, property, false, cancellationToken));
    }

    private static async Task<IResult> UpdatePropertyAsync(
        Guid propertyId,
        PropertyUpdateRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageProperties, cancellationToken))
        {
            return Results.Forbid();
        }

        var property = await AccessibleProperties(db, actor, includeArchived: true).FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return Results.NotFound();
        }

        try
        {
            property.Update(request.Name, request.Address, request.PropertyType, request.TimeZoneId);
            property.SetArchived(request.IsArchived);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await ToPropertyResponseAsync(db, property, true, cancellationToken));
    }

    private static async Task<IResult> AddFloorAsync(
        Guid propertyId,
        FloorDraft request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageProperties, cancellationToken))
        {
            return Results.Forbid();
        }

        var property = await AccessibleProperties(db, actor, includeArchived: false).FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return Results.NotFound();
        }

        var draftError = ValidateFloorDraft(request);
        if (draftError is not null)
        {
            return BadRequest(draftError);
        }

        var sortOrder = await db.Floors.Where(floor => floor.PropertyId == propertyId).Select(floor => (int?)floor.SortOrder).MaxAsync(cancellationToken) ?? -1;
        var floor = new Floor(actor.OrganizationId, propertyId, request.Name, sortOrder + 1);
        db.Floors.Add(floor);
        db.Units.AddRange(GetUnitNames(request).Select(name => new Unit(actor.OrganizationId, propertyId, floor.Id, name)));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/properties/{propertyId}/floors/{floor.Id}", new { floor.Id, floor.Name });
    }

    private static async Task<IResult> UpdateFloorAsync(
        Guid propertyId,
        Guid floorId,
        FloorUpdateRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageProperties, cancellationToken))
        {
            return Results.Forbid();
        }

        var property = await AccessibleProperties(db, actor, includeArchived: true).FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        var floor = await db.Floors.FirstOrDefaultAsync(item => item.Id == floorId && item.PropertyId == propertyId && item.OrganizationId == actor.OrganizationId, cancellationToken);
        if (property is null || floor is null)
        {
            return Results.NotFound();
        }

        var names = CleanNames(request.UnitNames);
        if (string.IsNullOrWhiteSpace(request.Name) || names.Count != names.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return BadRequest("Floor name and unique unit names are required.");
        }

        floor.Update(request.Name, request.SortOrder);
        floor.SetArchived(request.IsArchived);
        var units = await db.Units.Where(unit => unit.FloorId == floorId && unit.OrganizationId == actor.OrganizationId).OrderBy(unit => unit.Name).ToListAsync(cancellationToken);
        for (var index = 0; index < names.Count; index++)
        {
            if (index < units.Count)
            {
                units[index].Update(names[index], isEnabled: true, isArchived: false);
            }
            else
            {
                db.Units.Add(new Unit(actor.OrganizationId, propertyId, floorId, names[index]));
            }
        }

        foreach (var removedUnit in units.Skip(names.Count))
        {
            removedUnit.Update(removedUnit.Name, isEnabled: false, isArchived: true);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await ToPropertyResponseAsync(db, property!, includeArchived: true, cancellationToken));
    }

    private static async Task<IResult> UpdateUnitAsync(
        Guid propertyId,
        Guid unitId,
        UnitUpdateRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageProperties, cancellationToken))
        {
            return Results.Forbid();
        }

        var property = await AccessibleProperties(db, actor, includeArchived: true).FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        var unit = await db.Units.FirstOrDefaultAsync(item => item.Id == unitId && item.PropertyId == propertyId && item.OrganizationId == actor.OrganizationId, cancellationToken);
        if (property is null || unit is null)
        {
            return Results.NotFound();
        }

        try
        {
            unit.Update(request.Name, request.IsEnabled, request.IsArchived);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await ToPropertyResponseAsync(db, property, includeArchived: true, cancellationToken));
    }

    private static async Task<IResult> UpdateOccupancyStatusAsync(
        Guid propertyId,
        Guid unitId,
        OccupancyStatusRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageTenants, cancellationToken))
        {
            return Results.Forbid();
        }

        var unit = await AccessibleUnitAsync(db, actor, propertyId, unitId, cancellationToken);
        if (unit is null)
        {
            return Results.NotFound();
        }

        unit.SetOccupancyStatus(request.Status);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { unit.Id, occupancyStatus = unit.OccupancyStatus });
    }

    private static async Task<IResult> GetStaffAsync(
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var users = await db.Users.AsNoTracking()
            .Where(user => user.OrganizationId == actor.OrganizationId && user.Role != UserRole.Admin && user.IsActive)
            .OrderBy(user => user.FullName)
            .ToListAsync(cancellationToken);
        var assignments = await db.StaffPropertyAssignments.AsNoTracking()
            .Where(assignment => assignment.OrganizationId == actor.OrganizationId)
            .ToListAsync(cancellationToken);

        return Results.Ok(users.Select(user => new
        {
            user = ToUserResponse(user),
            propertyIds = assignments.Where(item => item.UserId == user.Id).Select(item => item.PropertyId).ToArray(),
        }));
    }

    private static async Task<IResult> CreateInvitationAsync(
        InvitationRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        UserManager<AppUser> userManager,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (request.Role is not (UserRole.Admin or UserRole.Manager or UserRole.BuildingStaff or UserRole.FinanceManager))
        {
            return BadRequest("This invitation role is not allowed.");
        }

        if (!new EmailAddressAttribute().IsValid(request.Email))
        {
            return BadRequest("A valid email address is required.");
        }

        if (await userManager.FindByEmailAsync(request.Email.Trim()) is not null)
        {
            return Results.Conflict(new { message = "An account already exists for this email." });
        }

        var (plainText, hash) = SecurityTokens.CreateInvitationToken();
        var invitation = new UserInvitation(actor.OrganizationId, actor.UserId, request.Email, request.Role, hash, DateTimeOffset.UtcNow.AddHours(24));
        db.UserInvitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/staff/invitations/{invitation.Id}", new
        {
            invitation.Id,
            invitation.Email,
            invitation.Role,
            invitation.ExpiresAtUtc,
            invitationToken = plainText,
            deliveryStatus = "Not connected",
        });
    }

    private static async Task<IResult> AssignStaffAsync(
        Guid propertyId,
        Guid userId,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var property = await db.Properties.FirstOrDefaultAsync(item => item.Id == propertyId && item.OrganizationId == actor.OrganizationId, cancellationToken);
        var staff = await db.Users.FirstOrDefaultAsync(user => user.Id == userId && user.OrganizationId == actor.OrganizationId && user.Role != UserRole.Admin && user.IsActive, cancellationToken);
        if (property is null || staff is null)
        {
            return Results.NotFound();
        }

        var assignmentExists = await db.StaffPropertyAssignments.AnyAsync(item => item.UserId == userId && item.PropertyId == propertyId, cancellationToken);
        if (!assignmentExists)
        {
            db.StaffPropertyAssignments.Add(new StaffPropertyAssignment(actor.OrganizationId, userId, propertyId));
            db.OrganizationAuditEntries.Add(new OrganizationAuditEntry(
                actor.OrganizationId,
                actor.UserId,
                "StaffPropertyAssigned",
                "Property assignment changed.",
                null,
                JsonSerializer.Serialize(new { userId, propertyId })));
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> RemoveStaffAsync(
        Guid propertyId,
        Guid userId,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var assignment = await db.StaffPropertyAssignments.FirstOrDefaultAsync(item => item.UserId == userId && item.PropertyId == propertyId && item.OrganizationId == actor.OrganizationId, cancellationToken);
        if (assignment is null)
        {
            return Results.NotFound();
        }

        var before = JsonSerializer.Serialize(new { assignment.UserId, assignment.PropertyId });
        db.StaffPropertyAssignments.Remove(assignment);
        db.OrganizationAuditEntries.Add(new OrganizationAuditEntry(
            actor.OrganizationId,
            actor.UserId,
            "StaffPropertyUnassigned",
            "Property assignment changed.",
            before,
            null));
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetTenantsAsync(
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ViewAssignedProperties, cancellationToken))
        {
            return Results.Forbid();
        }

        var query = db.Tenants.AsNoTracking().Where(tenant => tenant.OrganizationId == actor.OrganizationId);
        if (actor.IsStaff)
        {
            query = query.Where(tenant => db.OccupancyAssignments.Any(assignment => assignment.TenantId == tenant.Id && db.Units.Any(unit => unit.Id == assignment.UnitId && db.StaffPropertyAssignments.Any(staff => staff.UserId == actor.UserId && staff.PropertyId == unit.PropertyId))));
        }

        var tenants = await query.OrderBy(tenant => tenant.Name).ToListAsync(cancellationToken);
        var tenantIds = tenants.Select(tenant => tenant.Id).ToArray();
        var currentAssignments = await (
            from assignment in db.OccupancyAssignments.AsNoTracking()
            join unit in db.Units.AsNoTracking() on assignment.UnitId equals unit.Id
            join property in db.Properties.AsNoTracking() on unit.PropertyId equals property.Id
            where tenantIds.Contains(assignment.TenantId)
                && assignment.OrganizationId == actor.OrganizationId
                && assignment.EndDate == null
                && property.OrganizationId == actor.OrganizationId
                && (actor.IsAdmin || db.StaffPropertyAssignments.Any(staff => staff.UserId == actor.UserId && staff.PropertyId == property.Id))
            select new
            {
                assignment.TenantId,
                PropertyId = property.Id,
                PropertyName = property.Name,
                UnitId = unit.Id,
                UnitName = unit.Name,
                assignment.StartDate,
            }).ToListAsync(cancellationToken);

        return Results.Ok(tenants.Select(tenant => new TenantResponse(
            tenant.Id,
            tenant.Name,
            tenant.Phone,
            tenant.OnboardingStatus,
            currentAssignments
                .Where(assignment => assignment.TenantId == tenant.Id)
                .OrderBy(assignment => assignment.PropertyName)
                .ThenBy(assignment => assignment.UnitName)
                .Select(assignment => new TenantCurrentUnitResponse(assignment.PropertyId, assignment.PropertyName, assignment.UnitId, assignment.UnitName, assignment.StartDate))
                .ToList())));
    }

    private static async Task<IResult> CreateTenantAsync(
        TenantCreateRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageTenants, cancellationToken))
        {
            return Results.Forbid();
        }

        var tenant = new Tenant(actor.OrganizationId, request.Name, request.Phone);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/tenants/{tenant.Id}", new TenantResponse(tenant.Id, tenant.Name, tenant.Phone, tenant.OnboardingStatus));
    }

    private static async Task<IResult> UpdateTenantAsync(
        Guid tenantId,
        TenantUpdateRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageTenants, cancellationToken))
        {
            return Results.Forbid();
        }

        var tenant = await AccessibleTenants(db, actor).FirstOrDefaultAsync(item => item.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Results.NotFound();
        }

        tenant.UpdateDetails(request.Name, request.Phone);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new TenantResponse(tenant.Id, tenant.Name, tenant.Phone, tenant.OnboardingStatus));
    }

    private static async Task<IResult> GetOccupancyHistoryAsync(
        Guid propertyId,
        Guid unitId,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ViewAssignedProperties, cancellationToken))
        {
            return Results.Forbid();
        }

        var unit = await AccessibleUnitAsync(db, actor, propertyId, unitId, cancellationToken);
        if (unit is null)
        {
            return Results.NotFound();
        }

        var history = await db.OccupancyAssignments.AsNoTracking()
            .Where(assignment => assignment.UnitId == unitId && assignment.OrganizationId == actor.OrganizationId)
            .Join(db.Tenants, assignment => assignment.TenantId, tenant => tenant.Id, (assignment, tenant) => new
            {
                assignment.Id,
                assignment.TenantId,
                tenant.Name,
                tenant.Phone,
                assignment.StartDate,
                assignment.EndDate,
            })
            .OrderByDescending(assignment => assignment.StartDate)
            .ToListAsync(cancellationToken);
        return Results.Ok(history);
    }

    private static async Task<IResult> AddOccupancyAsync(
        Guid propertyId,
        Guid unitId,
        OccupancyAssignmentRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageTenants, cancellationToken))
        {
            return Results.Forbid();
        }

        if (request.EndDate is not null && request.EndDate < request.StartDate)
        {
            return BadRequest("Occupancy end date cannot be before the start date.");
        }

        var unit = await AccessibleUnitAsync(db, actor, propertyId, unitId, cancellationToken);
        var tenant = await db.Tenants.FirstOrDefaultAsync(item => item.Id == request.TenantId && item.OrganizationId == actor.OrganizationId, cancellationToken);
        if (unit is null || tenant is null)
        {
            return Results.NotFound();
        }

        var requestedEnd = request.EndDate ?? DateOnly.MaxValue;
        var overlaps = await db.OccupancyAssignments.AnyAsync(assignment =>
            assignment.UnitId == unitId
            && assignment.StartDate < requestedEnd
            && (assignment.EndDate == null || assignment.EndDate > request.StartDate), cancellationToken);
        if (overlaps)
        {
            return Results.Conflict(new { message = "The unit already has an overlapping occupancy assignment." });
        }

        var occupancy = new OccupancyAssignment(actor.OrganizationId, tenant.Id, unit.Id, request.StartDate, request.EndDate);
        db.OccupancyAssignments.Add(occupancy);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/properties/{propertyId}/units/{unitId}/occupancy", occupancy);
    }

    private static async Task<IResult> CloseOccupancyAsync(
        Guid propertyId,
        Guid unitId,
        Guid assignmentId,
        CloseOccupancyRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageTenants, cancellationToken))
        {
            return Results.Forbid();
        }

        if (await AccessibleUnitAsync(db, actor, propertyId, unitId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var assignment = await db.OccupancyAssignments.FirstOrDefaultAsync(item =>
            item.Id == assignmentId && item.UnitId == unitId && item.OrganizationId == actor.OrganizationId && item.EndDate == null,
            cancellationToken);
        if (assignment is null)
        {
            return Results.NotFound();
        }

        try
        {
            assignment.Close(request.EndDate);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(assignment);
    }

    private static async Task<IResult> GetPropertyChargesAsync(
        Guid propertyId,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageCharges, cancellationToken))
        {
            return Results.Forbid();
        }

        var property = await AccessibleProperties(db, actor, false).FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return Results.NotFound();
        }

        var settings = await db.ChargeConfigurations.AsNoTracking()
            .Where(item => item.OrganizationId == actor.OrganizationId && item.PropertyId == propertyId && item.UnitId == null)
            .OrderBy(item => item.BillingMonth).ThenBy(item => item.ChargeType)
            .Select(item => ToChargeResponse(item))
            .ToListAsync(cancellationToken);
        return Results.Ok(settings);
    }

    private static async Task<IResult> PutPropertyChargesAsync(
        Guid propertyId,
        ChargeSettingsRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageCharges, cancellationToken))
        {
            return Results.Forbid();
        }

        var property = await AccessibleProperties(db, actor, false).FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return Results.NotFound();
        }

        var error = ValidateChargeSettings(request.Items, propertyDefaults: true);
        if (error is not null)
        {
            return BadRequest(error);
        }

        foreach (var setting in request.Items)
        {
            var existing = await db.ChargeConfigurations.FirstOrDefaultAsync(item =>
                item.OrganizationId == actor.OrganizationId && item.PropertyId == propertyId && item.UnitId == null
                && item.ChargeType == setting.ChargeType && item.BillingMonth == setting.BillingMonth, cancellationToken);
            if (existing is null)
            {
                db.ChargeConfigurations.Add(new ChargeConfiguration(actor.OrganizationId, propertyId, null, setting.ChargeType, setting.Mode, setting.BillingMonth, setting.Amount, setting.CustomLabel, setting.WarningMode, setting.WarningThreshold));
            }
            else
            {
                existing.Update(setting.Mode, setting.BillingMonth, setting.Amount, setting.CustomLabel, setting.WarningMode, setting.WarningThreshold);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await db.ChargeConfigurations.AsNoTracking().Where(item => item.OrganizationId == actor.OrganizationId && item.PropertyId == propertyId && item.UnitId == null).Select(item => ToChargeResponse(item)).ToListAsync(cancellationToken));
    }

    private static async Task<IResult> GetUnitChargesAsync(
        Guid propertyId,
        Guid unitId,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageCharges, cancellationToken))
        {
            return Results.Forbid();
        }

        var unit = await AccessibleUnitAsync(db, actor, propertyId, unitId, cancellationToken);
        if (unit is null)
        {
            return Results.NotFound();
        }

        var settings = await db.ChargeConfigurations.AsNoTracking()
            .Where(item => item.OrganizationId == actor.OrganizationId && item.PropertyId == propertyId && item.UnitId == unitId)
            .OrderBy(item => item.BillingMonth).ThenBy(item => item.ChargeType)
            .Select(item => ToChargeResponse(item))
            .ToListAsync(cancellationToken);
        return Results.Ok(settings);
    }

    private static async Task<IResult> PutUnitChargesAsync(
        Guid propertyId,
        Guid unitId,
        ChargeSettingsRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageCharges, cancellationToken))
        {
            return Results.Forbid();
        }

        var unit = await AccessibleUnitAsync(db, actor, propertyId, unitId, cancellationToken);
        if (unit is null)
        {
            return Results.NotFound();
        }

        var error = ValidateChargeSettings(request.Items, propertyDefaults: false);
        if (error is not null)
        {
            return BadRequest(error);
        }

        foreach (var setting in request.Items)
        {
            var existing = await db.ChargeConfigurations.FirstOrDefaultAsync(item =>
                item.OrganizationId == actor.OrganizationId && item.PropertyId == propertyId && item.UnitId == unitId
                && item.ChargeType == setting.ChargeType && item.BillingMonth == setting.BillingMonth, cancellationToken);
            if (existing is null)
            {
                db.ChargeConfigurations.Add(new ChargeConfiguration(actor.OrganizationId, propertyId, unitId, setting.ChargeType, setting.Mode, setting.BillingMonth, setting.Amount, setting.CustomLabel, setting.WarningMode, setting.WarningThreshold));
            }
            else
            {
                existing.Update(setting.Mode, setting.BillingMonth, setting.Amount, setting.CustomLabel, setting.WarningMode, setting.WarningThreshold);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await db.ChargeConfigurations.AsNoTracking().Where(item => item.OrganizationId == actor.OrganizationId && item.PropertyId == propertyId && item.UnitId == unitId).Select(item => ToChargeResponse(item)).ToListAsync(cancellationToken));
    }

    private static async Task<IResult> EndPropertyChargeAsync(
        Guid propertyId,
        string chargeType,
        EndChargeRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        return await EndChargeAsync(propertyId, null, chargeType, request, httpContext, db, cancellationToken);
    }

    private static async Task<IResult> EndUnitChargeAsync(
        Guid propertyId,
        Guid unitId,
        string chargeType,
        EndChargeRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        return await EndChargeAsync(propertyId, unitId, chargeType, request, httpContext, db, cancellationToken);
    }

    private static async Task<IResult> EndChargeAsync(
        Guid propertyId,
        Guid? unitId,
        string chargeType,
        EndChargeRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ManageCharges, cancellationToken))
        {
            return Results.Forbid();
        }

        if (!Enum.TryParse<ChargeType>(chargeType, true, out var parsedChargeType))
        {
            return BadRequest("The charge type is not recognized.");
        }

        if (request.BillingMonth.Day != 1)
        {
            return BadRequest("The effective billing month must be the first day of a month.");
        }

        if (unitId is null)
        {
            if (await AccessibleProperties(db, actor, false).FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken) is null)
            {
                return Results.NotFound();
            }
        }
        else if (await AccessibleUnitAsync(db, actor, propertyId, unitId.Value, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var existing = await db.ChargeConfigurations.FirstOrDefaultAsync(item =>
            item.OrganizationId == actor.OrganizationId
            && item.PropertyId == propertyId
            && item.UnitId == unitId
            && item.ChargeType == parsedChargeType
            && item.BillingMonth == request.BillingMonth, cancellationToken);
        if (existing is null)
        {
            db.ChargeConfigurations.Add(new ChargeConfiguration(
                actor.OrganizationId,
                propertyId,
                unitId,
                parsedChargeType,
                ChargeSettingMode.Disabled,
                request.BillingMonth,
                null,
                null,
                ChargeSettingMode.Disabled,
                null));
        }
        else
        {
            existing.Update(ChargeSettingMode.Disabled, request.BillingMonth, null, null, ChargeSettingMode.Disabled, null);
        }

        await db.SaveChangesAsync(cancellationToken);
        var settings = db.ChargeConfigurations.AsNoTracking().Where(item =>
            item.OrganizationId == actor.OrganizationId
            && item.PropertyId == propertyId
            && item.UnitId == unitId);
        return Results.Ok(await settings.OrderBy(item => item.BillingMonth).ThenBy(item => item.ChargeType).Select(item => ToChargeResponse(item)).ToListAsync(cancellationToken));
    }

    private static async Task<IResult> GenerateBillingCycleAsync(
        Guid propertyId,
        BillingMonthRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if ((actor.Role == UserRole.BuildingStaff && !actor.IsAdmin)
            || !await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.EditMeterReadings, cancellationToken))
        {
            return Results.Forbid();
        }

        if (request.BillingMonth.Day != 1)
        {
            return BadRequest("Billing month must be the first day of a month.");
        }

        var property = await AccessibleProperties(db, actor, includeArchived: false).FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return Results.NotFound();
        }

        if (await db.BillingCycles.AnyAsync(cycle => cycle.OrganizationId == actor.OrganizationId && cycle.PropertyId == propertyId && cycle.BillingMonth == request.BillingMonth, cancellationToken))
        {
            return Results.Conflict(new { message = "A billing cycle already exists for this property and month." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var cycle = new BillingCycle(actor.OrganizationId, propertyId, property.Name, property.Address, request.BillingMonth, actor.UserId);
            db.BillingCycles.Add(cycle);

            var monthEnd = request.BillingMonth.AddMonths(1).AddDays(-1);
            var units = await db.Units.AsNoTracking()
                .Where(unit => unit.OrganizationId == actor.OrganizationId && unit.PropertyId == propertyId && unit.IsEnabled && !unit.IsArchived)
                .OrderBy(unit => unit.FloorId).ThenBy(unit => unit.Name)
                .ToListAsync(cancellationToken);
            var floorNames = await db.Floors.AsNoTracking()
                .Where(floor => floor.PropertyId == propertyId && floor.OrganizationId == actor.OrganizationId)
                .ToDictionaryAsync(floor => floor.Id, floor => floor.Name, cancellationToken);
            var unitIds = units.Select(unit => unit.Id).ToArray();
            var occupancy = await db.OccupancyAssignments.AsNoTracking()
                .Where(item => item.OrganizationId == actor.OrganizationId && unitIds.Contains(item.UnitId) && item.StartDate <= monthEnd && (item.EndDate == null || item.EndDate >= request.BillingMonth))
                .Join(db.Tenants.AsNoTracking(), item => item.TenantId, tenant => tenant.Id, (item, tenant) => new OccupancySnapshot(item, tenant.Name, tenant.Phone))
                .ToListAsync(cancellationToken);
            var configurations = await db.ChargeConfigurations.AsNoTracking()
                .Where(item => item.OrganizationId == actor.OrganizationId && item.PropertyId == propertyId && item.BillingMonth <= request.BillingMonth && (item.UnitId == null || unitIds.Contains(item.UnitId.Value)))
                .ToListAsync(cancellationToken);

            var previousCycle = await db.BillingCycles.AsNoTracking().FirstOrDefaultAsync(item =>
                item.OrganizationId == actor.OrganizationId
                && item.PropertyId == propertyId
                && item.BillingMonth == request.BillingMonth.AddMonths(-1), cancellationToken);
            var previousElectricity = new Dictionary<Guid, BillingChargeLine>();
            if (previousCycle?.Status == BillingCycleStatus.Approved)
            {
                var previousBillIds = await db.BillingUnitBills.AsNoTracking()
                    .Where(bill => bill.BillingCycleId == previousCycle.Id && bill.OrganizationId == actor.OrganizationId)
                    .ToDictionaryAsync(bill => bill.Id, bill => bill.UnitId, cancellationToken);
                var previousLines = await db.BillingChargeLines.AsNoTracking()
                    .Where(line => line.OrganizationId == actor.OrganizationId && line.ChargeType == ChargeType.Electricity && previousBillIds.Keys.Contains(line.BillingUnitBillId))
                    .ToListAsync(cancellationToken);
                foreach (var previousLine in previousLines)
                {
                    if (previousBillIds.TryGetValue(previousLine.BillingUnitBillId, out var unitId))
                    {
                        previousElectricity[unitId] = previousLine;
                    }
                }
            }

            foreach (var unit in units)
            {
                var unitOccupancies = occupancy.Where(item => item.Assignment.UnitId == unit.Id).OrderBy(item => item.Assignment.StartDate).ToList();
                var fullMonthOccupancy = unitOccupancies.Count == 1
                    && unitOccupancies[0].Assignment.StartDate <= request.BillingMonth
                    && (unitOccupancies[0].Assignment.EndDate is null || unitOccupancies[0].Assignment.EndDate >= monthEnd);
                var blockingReason = unitOccupancies.Count == 0
                    ? "Vacant-unit charge policy is not selected; this draft is blocked for review."
                    : !fullMonthOccupancy
                        ? "Mid-month tenant change or partial-month allocation policy is not selected; this draft is blocked for review."
                        : null;
                var selectedOccupancy = fullMonthOccupancy ? unitOccupancies[0] : null;
                var bill = new BillingUnitBill(
                    actor.OrganizationId,
                    cycle.Id,
                    unit.Id,
                    unit.Name,
                    floorNames.GetValueOrDefault(unit.FloorId),
                    selectedOccupancy?.Assignment.Id,
                    selectedOccupancy?.Assignment.TenantId,
                    selectedOccupancy?.TenantName,
                    selectedOccupancy?.TenantPhone,
                    selectedOccupancy?.Assignment.StartDate,
                    selectedOccupancy?.Assignment.EndDate,
                    blockingReason);
                db.BillingUnitBills.Add(bill);

                if (blockingReason is not null)
                {
                    continue;
                }

                var billLines = new List<BillingChargeLine>();
                foreach (var effective in ResolveEffectiveCharges(configurations, unit.Id, request.BillingMonth))
                {
                    var isMetered = effective.ChargeType == ChargeType.Electricity;
                    var previousLine = isMetered && previousElectricity.TryGetValue(unit.Id, out var line) ? line : null;
                    var previousReading = previousLine?.CurrentReading;
                    var previousReadingDate = previousLine?.CurrentReadingDate;
                    var source = previousReading is null ? MeterReadingSource.Missing : MeterReadingSource.ApprovedPeriod;
                    var amount = isMetered ? 0m : RoundMoney(effective.Amount);
                    var chargeLine = new BillingChargeLine(
                        actor.OrganizationId,
                        bill.Id,
                        effective.ChargeType,
                        effective.ChargeName,
                        effective.Mode,
                        isMetered,
                        effective.Amount,
                        amount,
                        previousReading,
                        source,
                        previousReadingDate,
                        effective.WarningMode == ChargeSettingMode.Custom,
                        effective.WarningThreshold);
                    db.BillingChargeLines.Add(chargeLine);
                    billLines.Add(chargeLine);
                }

                RecalculateBillState(bill, billLines);
            }

            var snapshot = new { status = cycle.Status.ToString(), month = cycle.BillingMonth, units = units.Count };
            db.BillingAuditEntries.Add(new BillingAuditEntry(actor.OrganizationId, cycle.Id, actor.UserId, BillingAuditAction.Generated, "Initial monthly draft generated.", null, JsonSerializer.Serialize(snapshot)));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Created($"/api/properties/{propertyId}/billing-cycles/{request.BillingMonth:yyyy-MM}", await ToBillingCycleResponseAsync(db, cycle.Id, cancellationToken));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { message = "The billing cycle was created by another request. Refresh before continuing." });
        }
    }

    private static async Task<IResult> GetBillingCycleAsync(
        Guid propertyId,
        string billingMonth,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ViewMeterReadings, cancellationToken))
        {
            return Results.Forbid();
        }

        if (!DateOnly.TryParseExact(billingMonth, "yyyy-MM", out var month))
        {
            return BadRequest("Billing month must use the yyyy-MM format.");
        }

        if (await AccessibleProperties(db, actor, includeArchived: false).FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        var cycle = await db.BillingCycles.AsNoTracking().FirstOrDefaultAsync(item => item.OrganizationId == actor.OrganizationId && item.PropertyId == propertyId && item.BillingMonth == month, cancellationToken);
        return cycle is null
            ? Results.NotFound()
            : Results.Ok(await ToBillingCycleResponseAsync(db, cycle.Id, cancellationToken));
    }

    private static async Task<IResult> GetUnitBillingStatementAsync(
        Guid propertyId,
        string billingMonth,
        Guid unitId,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ViewApprovedBills, cancellationToken))
        {
            return Results.Forbid();
        }

        if (!DateOnly.TryParseExact(billingMonth, "yyyy-MM", out var month))
        {
            return BadRequest("Billing month must use the yyyy-MM format.");
        }

        var loaded = await LoadApprovedReportCycleAsync(db, actor, propertyId, month, cancellationToken);
        if (loaded.Property is null || loaded.Cycle is null)
        {
            return Results.NotFound();
        }

        var metadataError = ApprovedReportMetadataError(loaded.Cycle);
        if (metadataError is not null)
        {
            return Results.Conflict(new { message = metadataError });
        }

        var bill = await db.BillingUnitBills.AsNoTracking().FirstOrDefaultAsync(item =>
            item.OrganizationId == actor.OrganizationId
            && item.BillingCycleId == loaded.Cycle.Id
            && item.UnitId == unitId, cancellationToken);
        if (bill is null)
        {
            return Results.NotFound();
        }

        var lines = await db.BillingChargeLines.AsNoTracking()
            .Where(item => item.BillingUnitBillId == bill.Id)
            .OrderBy(item => item.ChargeType)
            .ToListAsync(cancellationToken);
        return Results.Ok(ToBillingStatementResponse(loaded.Cycle, "Unit", null, [ToBillingStatementUnitResponse(bill, lines)]));
    }

    private static async Task<IResult> GetTenantBillingStatementAsync(
        Guid propertyId,
        string billingMonth,
        Guid tenantId,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ViewApprovedBills, cancellationToken))
        {
            return Results.Forbid();
        }

        if (!DateOnly.TryParseExact(billingMonth, "yyyy-MM", out var month))
        {
            return BadRequest("Billing month must use the yyyy-MM format.");
        }

        var loaded = await LoadApprovedReportCycleAsync(db, actor, propertyId, month, cancellationToken);
        if (loaded.Property is null || loaded.Cycle is null)
        {
            return Results.NotFound();
        }

        var metadataError = ApprovedReportMetadataError(loaded.Cycle);
        if (metadataError is not null)
        {
            return Results.Conflict(new { message = metadataError });
        }

        var bills = await db.BillingUnitBills.AsNoTracking()
            .Where(item => item.OrganizationId == actor.OrganizationId && item.BillingCycleId == loaded.Cycle.Id && item.TenantId == tenantId && item.OccupancyAssignmentId != null)
            .OrderBy(item => item.UnitNameSnapshot)
            .ToListAsync(cancellationToken);
        if (bills.Count == 0)
        {
            return Results.NotFound();
        }

        var billIds = bills.Select(item => item.Id).ToArray();
        var lines = await db.BillingChargeLines.AsNoTracking()
            .Where(item => billIds.Contains(item.BillingUnitBillId))
            .OrderBy(item => item.ChargeType)
            .ToListAsync(cancellationToken);
        return Results.Ok(ToBillingStatementResponse(loaded.Cycle, "Tenant", tenantId, bills.Select(bill => ToBillingStatementUnitResponse(bill, lines.Where(line => line.BillingUnitBillId == bill.Id))).ToList()));
    }

    private static async Task<IResult> GetPropertyBillingSummaryAsync(
        Guid propertyId,
        string billingMonth,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ViewApprovedBills, cancellationToken))
        {
            return Results.Forbid();
        }

        if (!DateOnly.TryParseExact(billingMonth, "yyyy-MM", out var month))
        {
            return BadRequest("Billing month must use the yyyy-MM format.");
        }

        var loaded = await LoadApprovedReportCycleAsync(db, actor, propertyId, month, cancellationToken);
        if (loaded.Property is null || loaded.Cycle is null)
        {
            return Results.NotFound();
        }

        var metadataError = ApprovedReportMetadataError(loaded.Cycle);
        if (metadataError is not null)
        {
            return Results.Conflict(new { message = metadataError });
        }

        var bills = await db.BillingUnitBills.AsNoTracking()
            .Where(item => item.OrganizationId == actor.OrganizationId && item.BillingCycleId == loaded.Cycle.Id)
            .OrderBy(item => item.FloorNameSnapshot)
            .ThenBy(item => item.UnitNameSnapshot)
            .ToListAsync(cancellationToken);
        if (bills.Any(item => string.IsNullOrWhiteSpace(item.FloorNameSnapshot)))
        {
            return Results.Conflict(new { message = "This approved cycle is missing historical floor snapshots; the property summary cannot safely substitute current floor data." });
        }

        var billIds = bills.Select(item => item.Id).ToArray();
        var lines = await db.BillingChargeLines.AsNoTracking()
            .Where(item => billIds.Contains(item.BillingUnitBillId))
            .OrderBy(item => item.ChargeType)
            .ToListAsync(cancellationToken);
        var floors = bills.GroupBy(item => item.FloorNameSnapshot!, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new BillingFloorSummaryResponse(
                group.Key,
                RoundMoney(group.Sum(bill => bill.Total)),
                group.Select(bill => ToBillingStatementUnitResponse(bill, lines.Where(line => line.BillingUnitBillId == bill.Id))).ToList()))
            .ToList();
        return Results.Ok(new BillingPropertySummaryResponse(
            loaded.Cycle.PropertyId,
            loaded.Cycle.Id,
            loaded.Cycle.PropertyNameSnapshot!,
            loaded.Cycle.PropertyAddressSnapshot!,
            loaded.Cycle.BillingMonth,
            loaded.Cycle.ApprovedAtUtc!.Value,
            loaded.Cycle.ApprovedTotal!.Value,
            floors));
    }

    private static async Task<IResult> GetMonthlyBillingComparisonAsync(
        Guid propertyId,
        string billingMonth,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ViewApprovedBills, cancellationToken))
        {
            return Results.Forbid();
        }

        if (!DateOnly.TryParseExact(billingMonth, "yyyy-MM", out var month))
        {
            return BadRequest("Billing month must use the yyyy-MM format.");
        }

        var loaded = await LoadApprovedReportCycleAsync(db, actor, propertyId, month, cancellationToken);
        if (loaded.Property is null || loaded.Cycle is null)
        {
            return Results.NotFound();
        }

        var metadataError = ApprovedReportMetadataError(loaded.Cycle);
        if (metadataError is not null)
        {
            return Results.Conflict(new { message = metadataError });
        }

        var current = await LoadComparisonSourceAsync(db, actor.OrganizationId, loaded.Cycle.Id, cancellationToken);
        var previousMonth = month.AddMonths(-1);
        var previousCycle = await db.BillingCycles.AsNoTracking().FirstOrDefaultAsync(item =>
            item.OrganizationId == actor.OrganizationId && item.PropertyId == propertyId && item.BillingMonth == previousMonth, cancellationToken);
        var previous = previousCycle?.Status == BillingCycleStatus.Approved
            ? await LoadComparisonSourceAsync(db, actor.OrganizationId, previousCycle.Id, cancellationToken)
            : null;
        var incompleteReason = previousCycle is null
            ? "The immediately preceding billing period is missing; comparison values are unavailable, not zero."
            : previous is null
                ? $"The immediately preceding billing period is {previousCycle.Status.ToString().ToLowerInvariant()}, not approved; comparison values are unavailable, not zero."
                : null;
        var currentCharges = BuildChargeBuckets(current);
        var previousCharges = previous is null ? new Dictionary<ChargeMetricKey, ChargeMetricBucket>() : BuildChargeBuckets(previous);
        var chargeKeys = currentCharges.Keys.Union(previousCharges.Keys).OrderBy(key => key.ChargeType).ThenBy(key => key.Label, StringComparer.Ordinal).ToList();
        var chargeMetrics = chargeKeys.Select(key => ToComparisonMetric(key, currentCharges.GetValueOrDefault(key), previousCharges.GetValueOrDefault(key))).ToList();
        var electricity = ToConsumptionComparison(current, previous);
        var coverageNotes = BuildCoverageNotes(current, previous, incompleteReason);
        var totalComparison = ToComparisonValues(loaded.Cycle.ApprovedTotal, previousCycle?.ApprovedTotal, previous is not null);

        return Results.Ok(new BillingComparisonResponse(
            loaded.Cycle.PropertyId,
            loaded.Cycle.PropertyNameSnapshot!,
            loaded.Cycle.PropertyAddressSnapshot!,
            loaded.Cycle.BillingMonth,
            loaded.Cycle.Id,
            loaded.Cycle.ApprovedTotal!.Value,
            previousCycle?.ApprovedTotal is null || previous is null ? null : previousCycle.ApprovedTotal,
            totalComparison.Difference,
            totalComparison.PercentageChange,
            totalComparison.PercentageChangeAvailable,
            previousMonth,
            previousCycle?.Status.ToString() ?? "Missing",
            previous is not null,
            incompleteReason,
            electricity,
            chargeMetrics,
            coverageNotes));
    }

    private static async Task<IResult> SaveBillingReadingsAsync(
        Guid propertyId,
        Guid billingCycleId,
        BillingReadingsRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.EditMeterReadings, cancellationToken))
        {
            return Results.Forbid();
        }

        var cycle = await AccessibleBillingCycleAsync(db, actor, propertyId, billingCycleId, cancellationToken);
        if (cycle is null)
        {
            return Results.NotFound();
        }

        if (cycle.Status is not (BillingCycleStatus.Draft or BillingCycleStatus.Returned))
        {
            return Results.Conflict(new { message = "Submitted and approved billing cycles cannot be edited. Return the cycle before changing readings." });
        }

        if (cycle.Version != request.ExpectedVersion)
        {
            return Results.Conflict(new { message = "This billing cycle changed since it was loaded. Refresh before saving." });
        }

        var bills = await db.BillingUnitBills.Where(bill => bill.BillingCycleId == cycle.Id && bill.OrganizationId == actor.OrganizationId).ToListAsync(cancellationToken);
        var billById = bills.ToDictionary(bill => bill.Id);
        var lines = await db.BillingChargeLines.Where(line => line.OrganizationId == actor.OrganizationId && billById.Keys.Contains(line.BillingUnitBillId) && line.ChargeType == ChargeType.Electricity).ToListAsync(cancellationToken);
        var lineByBillId = lines.ToDictionary(line => line.BillingUnitBillId);
        var before = new List<object>();

        foreach (var entry in request.Entries ?? [])
        {
            if (!billById.ContainsKey(entry.UnitBillId) || !lineByBillId.TryGetValue(entry.UnitBillId, out var line))
            {
                return BadRequest("Every meter entry must reference an electricity line in this billing cycle.");
            }

            if (line.PreviousReadingSource == MeterReadingSource.ApprovedPeriod && entry.PreviousReading is not null && entry.PreviousReading != line.PreviousReading)
            {
                return BadRequest("The previous approved-period reading is a snapshot and cannot be changed.");
            }

            if (line.PreviousReadingSource == MeterReadingSource.ApprovedPeriod && entry.PreviousReadingDate is not null && entry.PreviousReadingDate != line.PreviousReadingDate)
            {
                return BadRequest("The previous approved-period reading date is a snapshot and cannot be changed.");
            }

            var previous = entry.PreviousReading ?? line.PreviousReading;
            if (previous is not null && entry.CurrentReading is not null && entry.CurrentReading < previous)
            {
                return BadRequest("Negative electricity consumption is not allowed. Meter reset or replacement needs a separate documented workflow.");
            }

            before.Add(new { entry.UnitBillId, line.PreviousReading, line.PreviousReadingDate, line.CurrentReading, line.CurrentReadingDate, line.ConsumptionUnits, line.Amount, line.PreviousReadingSource });
        }

        if (request.Entries is null || request.Entries.Count == 0)
        {
            return BadRequest("At least one meter entry is required.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var entry in request.Entries)
            {
                var line = lineByBillId[entry.UnitBillId];
                var previous = entry.PreviousReading ?? line.PreviousReading;
                var source = line.PreviousReadingSource == MeterReadingSource.ApprovedPeriod
                    ? MeterReadingSource.ApprovedPeriod
                    : previous is null ? MeterReadingSource.Missing : MeterReadingSource.OpeningEntry;
                decimal? units = previous is not null && entry.CurrentReading is not null ? entry.CurrentReading.Value - previous.Value : null;
                var amount = units is null ? 0m : RoundMoney(units.Value * (line.RateSnapshot ?? 0m));
                var previousDate = line.PreviousReadingSource == MeterReadingSource.ApprovedPeriod
                    ? line.PreviousReadingDate
                    : entry.PreviousReadingDate;
                line.SetMeterReadings(previous, previousDate, entry.CurrentReading, entry.CurrentReadingDate, source, units, amount);
                RecalculateBillState(billById[entry.UnitBillId], lines.Where(item => item.BillingUnitBillId == entry.UnitBillId));
            }

            var after = request.Entries.Select(entry =>
            {
                var line = lineByBillId[entry.UnitBillId];
                return new { entry.UnitBillId, line.PreviousReading, line.PreviousReadingDate, line.CurrentReading, line.CurrentReadingDate, line.ConsumptionUnits, line.Amount, line.PreviousReadingSource };
            }).ToList();
            cycle.MarkDataChanged(actor.UserId);
            db.BillingAuditEntries.Add(new BillingAuditEntry(actor.OrganizationId, cycle.Id, actor.UserId, BillingAuditAction.Saved, request.Reason, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after)));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(await ToBillingCycleResponseAsync(db, cycle.Id, cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { message = "This billing cycle changed concurrently. Refresh before saving." });
        }
    }

    private static async Task<IResult> SubmitBillingCycleAsync(
        Guid propertyId,
        Guid billingCycleId,
        BillingActionRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.SubmitMeterReadings, cancellationToken))
        {
            return Results.Forbid();
        }

        var cycle = await AccessibleBillingCycleAsync(db, actor, propertyId, billingCycleId, cancellationToken);
        if (cycle is null)
        {
            return Results.NotFound();
        }

        if (cycle.Version != request.ExpectedVersion)
        {
            return Results.Conflict(new { message = "This billing cycle changed since it was loaded. Refresh before submitting." });
        }

        var bills = await db.BillingUnitBills.AsNoTracking().Where(bill => bill.BillingCycleId == cycle.Id).ToListAsync(cancellationToken);
        var blocked = bills.Where(bill => bill.BlockingReason is not null).Select(bill => bill.BlockingReason).Distinct().ToList();
        if (blocked.Count > 0)
        {
            return BadRequest($"This billing cycle has unresolved items: {string.Join(" ", blocked)}");
        }

        var before = new { status = cycle.Status.ToString(), version = cycle.Version };
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            cycle.Submit(actor.UserId);
            var after = new { status = cycle.Status.ToString(), version = cycle.Version };
            db.BillingAuditEntries.Add(new BillingAuditEntry(actor.OrganizationId, cycle.Id, actor.UserId, BillingAuditAction.Submitted, request.Reason, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after)));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(await ToBillingCycleResponseAsync(db, cycle.Id, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(exception.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { message = "This billing cycle changed concurrently. Refresh before submitting." });
        }
    }

    private static async Task<IResult> ReturnBillingCycleAsync(
        Guid propertyId,
        Guid billingCycleId,
        BillingActionRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReviewBills, cancellationToken))
        {
            return Results.Forbid();
        }

        var cycle = await AccessibleBillingCycleAsync(db, actor, propertyId, billingCycleId, cancellationToken);
        if (cycle is null)
        {
            return Results.NotFound();
        }

        if (cycle.Version != request.ExpectedVersion)
        {
            return Results.Conflict(new { message = "This billing cycle changed since it was loaded. Refresh before returning it." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var before = new { status = cycle.Status.ToString(), version = cycle.Version };
            cycle.Return(actor.UserId, request.Reason ?? string.Empty);
            var after = new { status = cycle.Status.ToString(), version = cycle.Version, reason = cycle.ReturnReason };
            db.BillingAuditEntries.Add(new BillingAuditEntry(actor.OrganizationId, cycle.Id, actor.UserId, BillingAuditAction.Returned, request.Reason, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after)));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(await ToBillingCycleResponseAsync(db, cycle.Id, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(exception.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { message = "This billing cycle changed concurrently. Refresh before returning it." });
        }
    }

    private static async Task<IResult> ApproveBillingCycleAsync(
        Guid propertyId,
        Guid billingCycleId,
        BillingActionRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReviewBills, cancellationToken))
        {
            return Results.Forbid();
        }

        var cycle = await AccessibleBillingCycleAsync(db, actor, propertyId, billingCycleId, cancellationToken);
        if (cycle is null)
        {
            return Results.NotFound();
        }

        if (cycle.Version != request.ExpectedVersion)
        {
            return Results.Conflict(new { message = "This billing cycle changed since it was loaded. Refresh before approving." });
        }

        if (cycle.Status != BillingCycleStatus.Submitted)
        {
            return BadRequest("Only submitted billing cycles can be approved.");
        }

        var bills = await db.BillingUnitBills.AsNoTracking().Where(bill => bill.BillingCycleId == cycle.Id).ToListAsync(cancellationToken);
        var blocked = bills.Where(bill => bill.BlockingReason is not null).Select(bill => bill.BlockingReason).Distinct().ToList();
        if (blocked.Count > 0)
        {
            return BadRequest($"This billing cycle has unresolved items: {string.Join(" ", blocked)}");
        }

        if (actor.UserId == cycle.LastChangedByUserId)
        {
            return Results.Problem(detail: "The person who last changed billing data cannot approve that version.", statusCode: StatusCodes.Status403Forbidden);
        }

        var total = RoundMoney(bills.Sum(bill => bill.Total));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var before = new { status = cycle.Status.ToString(), version = cycle.Version, total };
            cycle.Approve(actor.UserId, total);
            var after = new { status = cycle.Status.ToString(), version = cycle.Version, total = cycle.ApprovedTotal };
            db.BillingAuditEntries.Add(new BillingAuditEntry(actor.OrganizationId, cycle.Id, actor.UserId, BillingAuditAction.Approved, request.Reason, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after)));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(await ToBillingCycleResponseAsync(db, cycle.Id, cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { message = "This billing cycle changed concurrently. Refresh before approving." });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(detail: "The approval could not be persisted atomically. No approval was recorded.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> GetCollectionReportsAsync(
        Guid propertyId,
        DateOnly billingMonth,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ViewApprovedBills, cancellationToken))
        {
            return Results.Forbid();
        }

        var property = await AccessibleProperties(db, actor, includeArchived: false)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return Results.NotFound();
        }

        var rows = await (
            from report in db.CollectionReports.AsNoTracking()
            join bill in db.BillingUnitBills.AsNoTracking() on report.BillingUnitBillId equals bill.Id
            join cycle in db.BillingCycles.AsNoTracking() on bill.BillingCycleId equals cycle.Id
            where report.OrganizationId == actor.OrganizationId
                && cycle.OrganizationId == actor.OrganizationId
                && cycle.PropertyId == propertyId
                && cycle.BillingMonth == billingMonth
                && cycle.Status == BillingCycleStatus.Approved
                && (actor.IsAdmin || actor.IsFinanceManager || report.ReporterUserId == actor.UserId)
             select new { report, bill, cycle })
            .OrderByDescending(item => item.report.SubmittedAtUtc)
            .Select(item => new CollectionQueryRow(item.report, item.bill, item.cycle))
            .ToListAsync(cancellationToken);

        var approvedBills = await (
            from bill in db.BillingUnitBills.AsNoTracking()
            join cycle in db.BillingCycles.AsNoTracking() on bill.BillingCycleId equals cycle.Id
            where bill.OrganizationId == actor.OrganizationId
                && cycle.OrganizationId == actor.OrganizationId
                && cycle.PropertyId == propertyId
                && cycle.BillingMonth == billingMonth
                && cycle.Status == BillingCycleStatus.Approved
            select bill)
            .ToListAsync(cancellationToken);

        return Results.Ok(await ToCollectionQueueResponseAsync(db, rows, approvedBills, cancellationToken));
    }

    private static async Task<IResult> GetMeterOverviewAsync(
        Guid propertyId,
        DateOnly billingMonth,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ViewMeterReadings, cancellationToken))
        {
            return Results.Forbid();
        }

        var property = await AccessibleProperties(db, actor, includeArchived: false)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return Results.NotFound();
        }

        var cycle = await db.BillingCycles.AsNoTracking().FirstOrDefaultAsync(item =>
            item.OrganizationId == actor.OrganizationId
            && item.PropertyId == propertyId
            && item.BillingMonth == billingMonth, cancellationToken);
        var units = await (
            from unit in db.Units.AsNoTracking()
            join floor in db.Floors.AsNoTracking() on unit.FloorId equals floor.Id
            where unit.OrganizationId == actor.OrganizationId
                && unit.PropertyId == propertyId
                && unit.IsEnabled
                && !unit.IsArchived
            orderby floor.SortOrder, unit.Name
            select new { unit.Id, unit.Name, FloorName = floor.Name, FloorSortOrder = floor.SortOrder })
            .ToListAsync(cancellationToken);

        var configurations = cycle is null
            ? await db.ChargeConfigurations.AsNoTracking()
                .Where(item => item.OrganizationId == actor.OrganizationId && item.PropertyId == propertyId)
                .ToListAsync(cancellationToken)
            : [];
        var bills = cycle is null
            ? []
            : await db.BillingUnitBills.AsNoTracking().Where(item => item.BillingCycleId == cycle.Id).ToListAsync(cancellationToken);
        var billIds = bills.Select(item => item.Id).ToArray();
        var lines = billIds.Length == 0
            ? []
            : await db.BillingChargeLines.AsNoTracking().Where(item => billIds.Contains(item.BillingUnitBillId)).ToListAsync(cancellationToken);
        Dictionary<Guid, RoomMeterIssueCount> issueCounts;
        try
        {
            issueCounts = (await db.RoomMeterIssues.AsNoTracking()
                .Where(item => item.OrganizationId == actor.OrganizationId && item.PropertyId == propertyId && item.UnitId.HasValue)
                .GroupBy(item => item.UnitId!.Value)
                .Select(group => new RoomMeterIssueCount(group.Key, group.Count(), group.Count(item => item.Status != RoomMeterIssueStatus.Resolved)))
                .ToListAsync(cancellationToken))
                .ToDictionary(item => item.UnitId);
        }
        catch (Exception exception) when (IsMissingRoomMeterIssueStorage(exception))
        {
            return RoomMeterIssueStorageUnavailable();
        }

        var billByUnit = bills.ToDictionary(item => item.UnitId);
        var lineByBill = lines.GroupBy(item => item.BillingUnitBillId).ToDictionary(group => group.Key, group => group.ToList());
        var response = units.Select(unit =>
        {
            billByUnit.TryGetValue(unit.Id, out var bill);
            var meter = bill is null
                ? null
                : lineByBill.GetValueOrDefault(bill.Id)?.FirstOrDefault(item => item.IsMetered && item.ChargeType == ChargeType.Electricity);
            var configuredElectricity = bill is null
                ? ResolveEffectiveCharges(configurations, unit.Id, billingMonth).FirstOrDefault(item => item.ChargeType == ChargeType.Electricity)
                : null;
            var applicable = meter is not null || configuredElectricity is not null;
            var issue = issueCounts.GetValueOrDefault(unit.Id);
            return new MeterRoomOverviewResponse(
                unit.Id,
                unit.Name,
                unit.FloorName,
                applicable,
                meter?.PreviousReading,
                meter?.PreviousReadingDate,
                meter?.CurrentReading,
                meter?.CurrentReadingDate,
                meter?.ConsumptionUnits,
                meter?.Amount,
                meter?.RateSnapshot ?? configuredElectricity?.Amount,
                MeterReadingStatus(cycle?.Status, applicable, meter?.CurrentReading),
                bill?.BlockingReason,
                issue?.Total ?? 0,
                issue?.Open ?? 0);
        }).ToList();

        return Results.Ok(new MeterOverviewResponse(propertyId, billingMonth, cycle?.Id, cycle?.Status.ToString(), response));
    }

    private static async Task<IResult> GetCollectionOverviewAsync(
        Guid propertyId,
        DateOnly billingMonth,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ViewApprovedBills, cancellationToken))
        {
            return Results.Forbid();
        }

        var property = await AccessibleProperties(db, actor, includeArchived: false)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return Results.NotFound();
        }

        var approvedCycle = await db.BillingCycles.AsNoTracking().FirstOrDefaultAsync(item =>
            item.OrganizationId == actor.OrganizationId
            && item.PropertyId == propertyId
            && item.BillingMonth == billingMonth
            && item.Status == BillingCycleStatus.Approved, cancellationToken);
        var anyCycle = approvedCycle is not null || await db.BillingCycles.AsNoTracking().AnyAsync(item =>
            item.OrganizationId == actor.OrganizationId && item.PropertyId == propertyId && item.BillingMonth == billingMonth, cancellationToken);
        var units = await (
            from unit in db.Units.AsNoTracking()
            join floor in db.Floors.AsNoTracking() on unit.FloorId equals floor.Id
            where unit.OrganizationId == actor.OrganizationId
                && unit.PropertyId == propertyId
                && unit.IsEnabled
                && !unit.IsArchived
            orderby floor.SortOrder, unit.Name
            select new { unit.Id, unit.Name, FloorName = floor.Name, FloorSortOrder = floor.SortOrder })
            .ToListAsync(cancellationToken);

        var bills = approvedCycle is null
            ? []
            : await db.BillingUnitBills.AsNoTracking().Where(item => item.BillingCycleId == approvedCycle.Id).ToListAsync(cancellationToken);
        var billIds = bills.Select(item => item.Id).ToArray();
        var reports = billIds.Length == 0
            ? []
            : await db.CollectionReports.AsNoTracking().Where(item => billIds.Contains(item.BillingUnitBillId)).ToListAsync(cancellationToken);
        var reportIds = reports.Select(item => item.Id).ToArray();
        var reversals = reportIds.Length == 0
            ? []
            : await db.CollectionReversals.AsNoTracking().Where(item => reportIds.Contains(item.CollectionReportId)).ToListAsync(cancellationToken);
        var approvedReversalIds = reversals.Where(item => item.Status == CollectionReversalStatus.Approved).Select(item => item.CollectionReportId).ToHashSet();
        var pendingReversalIds = reversals.Where(item => item.Status == CollectionReversalStatus.Pending).Select(item => item.CollectionReportId).ToHashSet();
        var billByUnit = bills.ToDictionary(item => item.UnitId);
        var reportsByBill = reports.GroupBy(item => item.BillingUnitBillId).ToDictionary(group => group.Key, group => group.ToList());
        var rows = units.Select(unit =>
        {
            billByUnit.TryGetValue(unit.Id, out var bill);
            var billReports = bill is null ? [] : reportsByBill.GetValueOrDefault(bill.Id) ?? [];
            var confirmed = billReports.Where(item => item.Status == CollectionReportStatus.Approved && !approvedReversalIds.Contains(item.Id)).Sum(item => item.AmountReceived);
            var pending = billReports.Where(item => item.Status is CollectionReportStatus.Pending or CollectionReportStatus.Returned).Sum(item => item.AmountReceived);
            var pendingReversal = billReports.Where(item => pendingReversalIds.Contains(item.Id)).Sum(item => item.AmountReceived);
            var remaining = bill is null ? (decimal?)null : Math.Max(0m, bill.Total - confirmed);
            return new CollectionUnitOverviewResponse(
                unit.Id,
                unit.Name,
                unit.FloorName,
                bill?.Id,
                bill?.OccupancyAssignmentId,
                bill?.TenantId,
                bill?.TenantNameSnapshot,
                bill?.Total,
                confirmed,
                pending,
                pendingReversal,
                remaining,
                CollectionStatus(bill, confirmed, pending));
        }).ToList();

        return Results.Ok(new CollectionOverviewResponse(
            propertyId,
            billingMonth,
            approvedCycle?.Id,
            approvedCycle is not null ? "Approved" : anyCycle ? "Unapproved" : "Missing",
            rows,
            bills.Sum(item => item.Total),
            rows.Sum(item => item.PendingReportedAmount),
            rows.Sum(item => item.ConfirmedCollectedAmount),
            rows.Where(item => item.RemainingBalance is not null).Sum(item => item.RemainingBalance!.Value),
            rows.Sum(item => item.PendingReversalAmount)));
    }

    private static async Task<IResult> GetRoomMeterIssuesAsync(
        Guid propertyId,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        var canView = await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ViewMeterReadings, cancellationToken);
        var canReview = actor.IsAdmin || await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReviewRoomMeterIssues, cancellationToken);
        var canReport = await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReportRoomMeterIssues, cancellationToken);
        if (!canView && !canReview && !canReport)
        {
            return Results.Forbid();
        }

        if (await AccessibleProperties(db, actor, includeArchived: false).AsNoTracking().FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken) is null)
        {
            return Results.NotFound();
        }

        try
        {
            var issues = await db.RoomMeterIssues.AsNoTracking()
                .Where(item => item.OrganizationId == actor.OrganizationId && item.PropertyId == propertyId && (canReview || canView && item.ReporterUserId == actor.UserId || canReport && item.ReporterUserId == actor.UserId))
                .OrderByDescending(item => item.CreatedAtUtc)
                .ToListAsync(cancellationToken);
            var responses = new List<RoomMeterIssueResponse>(issues.Count);
            foreach (var issue in issues)
            {
                responses.Add(await ToRoomMeterIssueResponseAsync(db, issue, cancellationToken));
            }

            return Results.Ok(responses);
        }
        catch (Exception exception) when (IsMissingRoomMeterIssueStorage(exception))
        {
            return RoomMeterIssueStorageUnavailable();
        }
    }

    private static async Task<IResult> CreateRoomMeterIssueAsync(
        Guid propertyId,
        Guid unitId,
        RoomMeterIssueCreateRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReportRoomMeterIssues, cancellationToken))
        {
            return Results.Forbid();
        }

        var property = await AccessibleProperties(db, actor, includeArchived: false).FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        var unit = property is null ? null : await AccessibleUnitAsync(db, actor, propertyId, unitId, cancellationToken);
        if (property is null || unit is null)
        {
            return Results.NotFound();
        }

        if (!Enum.IsDefined(request.IssueType))
        {
            return BadRequest("A supported room or meter issue type is required.");
        }

        if (request.ObservedDate > PropertyLocalDate(property.TimeZoneId))
        {
            return BadRequest("The observed date cannot be in the future for this property.");
        }

        RoomMeterIssue issue;
        try
        {
            issue = new RoomMeterIssue(actor.OrganizationId, propertyId, unitId, actor.UserId, request.IssueType, request.Description, request.ObservedDate);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.RoomMeterIssues.Add(issue);
            db.RoomMeterIssueAuditEntries.Add(new RoomMeterIssueAuditEntry(
                actor.OrganizationId,
                issue.Id,
                actor.UserId,
                RoomMeterIssueAuditAction.Reported,
                null,
                null,
                JsonSerializer.Serialize(RoomMeterIssueSnapshot(issue))));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Created($"/api/room-meter-issues/{issue.Id}", await ToRoomMeterIssueResponseAsync(db, issue, cancellationToken));
        }
        catch (Exception exception) when (IsMissingRoomMeterIssueStorage(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return RoomMeterIssueStorageUnavailable();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(detail: "The issue report could not be saved. No meter or billing data was changed.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CreateFeedbackReportAsync(
        Guid propertyId,
        FeedbackReportCreateRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReportRoomMeterIssues, cancellationToken))
        {
            return Results.Forbid();
        }

        var property = await AccessibleProperties(db, actor, includeArchived: false)
            .FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return Results.NotFound();
        }

        Unit? unit = null;
        if (request.UnitId.HasValue)
        {
            unit = await AccessibleUnitAsync(db, actor, propertyId, request.UnitId.Value, cancellationToken);
            if (unit is null)
            {
                return Results.NotFound();
            }
        }

        if (request.SubmissionKey == Guid.Empty)
        {
            return BadRequest("A feedback submission key is required. Retry from the report form.");
        }

        var existing = await db.RoomMeterIssues.FirstOrDefaultAsync(item =>
            item.OrganizationId == actor.OrganizationId
            && item.ReporterUserId == actor.UserId
            && item.SubmissionKey == request.SubmissionKey,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.PropertyId != propertyId
                || existing.UnitId != request.UnitId
                || !string.Equals(existing.Description, request.Note?.Trim(), StringComparison.Ordinal)
                || existing.IssueType is not null)
            {
                return Results.Conflict(new { message = "This feedback retry key is already linked to a different report." });
            }

            try
            {
                return Results.Ok(await ToRoomMeterIssueResponseAsync(db, existing, cancellationToken));
            }
            catch (Exception exception) when (IsMissingRoomMeterIssueStorage(exception))
            {
                return RoomMeterIssueStorageUnavailable();
            }
        }

        RoomMeterIssue issue;
        try
        {
            issue = RoomMeterIssue.CreateFeedback(actor.OrganizationId, propertyId, unit?.Id, actor.UserId, request.Note, request.SubmissionKey);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.RoomMeterIssues.Add(issue);
            db.RoomMeterIssueAuditEntries.Add(new RoomMeterIssueAuditEntry(
                actor.OrganizationId,
                issue.Id,
                actor.UserId,
                RoomMeterIssueAuditAction.Reported,
                null,
                null,
                JsonSerializer.Serialize(RoomMeterIssueSnapshot(issue))));

            await AddFeedbackNotificationsAsync(db, actor, propertyId, issue, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Created($"/api/room-meter-issues/{issue.Id}", await ToRoomMeterIssueResponseAsync(db, issue, cancellationToken));
        }
        catch (Exception exception) when (IsMissingRoomMeterIssueStorage(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return RoomMeterIssueStorageUnavailable();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(detail: "The feedback report could not be saved. No billing data was changed.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task AddFeedbackNotificationsAsync(
        PropertyManagementDbContext db,
        RequestUser actor,
        Guid propertyId,
        RoomMeterIssue issue,
        CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking()
            .Where(user => user.OrganizationId == actor.OrganizationId && user.IsActive && user.Id != actor.UserId)
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            var recipient = new RequestUser(user.Id, user.OrganizationId, user.Role);
            if (!recipient.IsAdmin
                && !await OrganizationPermissionAuthorization.HasAsync(db, recipient, OrganizationPermission.ReviewRoomMeterIssues, cancellationToken))
            {
                continue;
            }

            if (!await AccessibleProperties(db, recipient, includeArchived: false)
                .AnyAsync(property => property.Id == propertyId, cancellationToken))
            {
                continue;
            }

            db.UserNotifications.Add(new UserNotification(actor.OrganizationId, user.Id, propertyId, issue.Id));
        }
    }

    private static async Task<IResult> GetNotificationsAsync(
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!actor.IsAdmin && !await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReviewRoomMeterIssues, cancellationToken))
        {
            return Results.Forbid();
        }

        try
        {
            var notifications = await db.UserNotifications.AsNoTracking()
                .Where(notification => notification.OrganizationId == actor.OrganizationId && notification.RecipientUserId == actor.UserId)
                .OrderByDescending(notification => notification.CreatedAtUtc)
                .Take(50)
                .ToListAsync(cancellationToken);
            var responses = new List<NotificationResponse>(notifications.Count);

            foreach (var notification in notifications)
            {
                var property = await AccessibleProperties(db, actor, includeArchived: false)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == notification.PropertyId, cancellationToken);
                if (property is null)
                {
                    continue;
                }

                var issue = await db.RoomMeterIssues.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == notification.RoomMeterIssueId && item.OrganizationId == actor.OrganizationId && item.PropertyId == property.Id, cancellationToken);
                if (issue is null)
                {
                    continue;
                }

                var unit = issue.UnitId.HasValue
                    ? await db.Units.AsNoTracking().FirstOrDefaultAsync(item => item.Id == issue.UnitId.Value && item.PropertyId == property.Id, cancellationToken)
                    : null;
                var unitLabel = unit?.Name ?? "Building/Common Area";
                responses.Add(new NotificationResponse(
                    notification.Id,
                    notification.RoomMeterIssueId,
                    notification.PropertyId,
                    issue.UnitId,
                    $"New report · {(unit is null ? "Building/Common Area" : $"Room {unit.Name}")}",
                    property.Name,
                    unitLabel,
                    property.TimeZoneId,
                    notification.CreatedAtUtc,
                    notification.ReadAtUtc,
                    notification.ReadAtUtc.HasValue));
            }

            return Results.Ok(responses);
        }
        catch (Exception exception) when (IsMissingRoomMeterIssueStorage(exception))
        {
            return RoomMeterIssueStorageUnavailable();
        }
    }

    private static async Task<IResult> MarkNotificationReadAsync(
        Guid notificationId,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!actor.IsAdmin && !await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReviewRoomMeterIssues, cancellationToken))
        {
            return Results.Forbid();
        }

        UserNotification? notification;
        try
        {
            notification = await db.UserNotifications.FirstOrDefaultAsync(item =>
                item.Id == notificationId && item.OrganizationId == actor.OrganizationId && item.RecipientUserId == actor.UserId, cancellationToken);
        }
        catch (Exception exception) when (IsMissingRoomMeterIssueStorage(exception))
        {
            return RoomMeterIssueStorageUnavailable();
        }

        if (notification is null
            || await AccessibleProperties(db, actor, includeArchived: false).AllAsync(item => item.Id != notification.PropertyId, cancellationToken))
        {
            return Results.NotFound();
        }

        notification.MarkRead();
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static Task<IResult> AcknowledgeRoomMeterIssueAsync(
        Guid issueId,
        RoomMeterIssueActionRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken) => ChangeRoomMeterIssueAsync(issueId, request, false, httpContext, db, cancellationToken);

    private static Task<IResult> ResolveRoomMeterIssueAsync(
        Guid issueId,
        RoomMeterIssueActionRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken) => ChangeRoomMeterIssueAsync(issueId, request, true, httpContext, db, cancellationToken);

    private static async Task<IResult> ChangeRoomMeterIssueAsync(
        Guid issueId,
        RoomMeterIssueActionRequest request,
        bool resolve,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReviewRoomMeterIssues, cancellationToken))
        {
            return Results.Forbid();
        }

        RoomMeterIssue? issue;
        try
        {
            issue = await db.RoomMeterIssues.FirstOrDefaultAsync(item => item.Id == issueId && item.OrganizationId == actor.OrganizationId, cancellationToken);
        }
        catch (Exception exception) when (IsMissingRoomMeterIssueStorage(exception))
        {
            return RoomMeterIssueStorageUnavailable();
        }
        if (issue is null || await AccessibleProperties(db, actor, includeArchived: false).AllAsync(item => item.Id != issue.PropertyId, cancellationToken))
        {
            return Results.NotFound();
        }

        if (issue.Version != request.ExpectedVersion)
        {
            return Results.Conflict(new { message = "This issue changed since it was loaded. Refresh before trying again." });
        }

        var before = RoomMeterIssueSnapshot(issue);
        try
        {
            if (resolve)
            {
                issue.Resolve(actor.UserId, request.Reason ?? string.Empty);
            }
            else
            {
                issue.Acknowledge(actor.UserId, request.Reason);
            }
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.RoomMeterIssueAuditEntries.Add(new RoomMeterIssueAuditEntry(
                actor.OrganizationId,
                issue.Id,
                actor.UserId,
                resolve ? RoomMeterIssueAuditAction.Resolved : RoomMeterIssueAuditAction.Acknowledged,
                request.Reason,
                JsonSerializer.Serialize(before),
                JsonSerializer.Serialize(RoomMeterIssueSnapshot(issue))));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(await ToRoomMeterIssueResponseAsync(db, issue, cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { message = "This issue changed since it was loaded. Refresh before trying again." });
        }
        catch (Exception exception) when (IsMissingRoomMeterIssueStorage(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return RoomMeterIssueStorageUnavailable();
        }
    }

    private static async Task<IResult> CreateCollectionReportAsync(
        Guid propertyId,
        CollectionReportCreateRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReportCollections, cancellationToken))
        {
            return Results.Forbid();
        }

        if (request.SubmissionKey == Guid.Empty || request.AmountReceived <= 0m)
        {
            return BadRequest("A positive amount and a unique submission key are required.");
        }

        var existing = await db.CollectionReports.FirstOrDefaultAsync(item =>
            item.OrganizationId == actor.OrganizationId && item.SubmissionKey == request.SubmissionKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.ReporterUserId != actor.UserId)
            {
                return Results.Conflict(new { message = "This submission key has already been used." });
            }

            var existingContext = await LoadCollectionContextAsync(db, actor, existing.Id, cancellationToken);
            return existingContext is null
                ? Results.Conflict(new { message = "This collection submission already exists but is no longer accessible." })
                : Results.Ok(await ToCollectionReportResponseAsync(db, existingContext, cancellationToken));
        }

        var context = await LoadApprovedBillContextAsync(db, actor, propertyId, request.BillingUnitBillId, cancellationToken);
        if (context is null)
        {
            return Results.NotFound();
        }

        var confirmed = await ConfirmedCollectionAmountAsync(db, context.Bill.Id, cancellationToken);
        if (request.AmountReceived > context.Bill.Total - confirmed)
        {
            return BadRequest("The reported amount exceeds the bill's remaining balance.");
        }

        CollectionReport report;
        try
        {
            report = new CollectionReport(
                actor.OrganizationId,
                context.Bill.Id,
                context.Bill.OccupancyAssignmentId,
                context.Bill.UnitId,
                context.Bill.UnitNameSnapshot,
                context.Bill.FloorNameSnapshot,
                context.Bill.TenantId,
                context.Bill.TenantNameSnapshot,
                context.Bill.TenantPhoneSnapshot,
                context.Cycle.BillingMonth,
                context.Bill.Total,
                actor.UserId,
                request.AmountReceived,
                request.ReceivedDate,
                request.PaymentMethod,
                request.ReferenceNote,
                request.SubmissionKey);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.CollectionReports.Add(report);
            db.CollectionAuditEntries.Add(new CollectionAuditEntry(
                actor.OrganizationId,
                report.Id,
                null,
                actor.UserId,
                CollectionAuditAction.Submitted,
                request.ReferenceNote,
                null,
                JsonSerializer.Serialize(CollectionSnapshot(report))));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var saved = await LoadCollectionContextAsync(db, actor, report.Id, cancellationToken);
            return Results.Created($"/api/collection-reports/{report.Id}", await ToCollectionReportResponseAsync(db, saved!, cancellationToken));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { message = "This collection report was already submitted. Refresh before retrying." });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(detail: "The collection report could not be persisted atomically.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> CorrectCollectionReportAsync(
        Guid reportId,
        CollectionReportCorrectionRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReportCollections, cancellationToken))
        {
            return Results.Forbid();
        }

        var context = await LoadCollectionContextAsync(db, actor, reportId, cancellationToken);
        if (context is null)
        {
            return Results.NotFound();
        }

        if (!actor.IsAdmin && context.Report.ReporterUserId != actor.UserId)
        {
            return Results.Forbid();
        }

        if (context.Report.Version != request.ExpectedVersion)
        {
            return Results.Conflict(new { message = "This collection report changed since it was loaded. Refresh before correcting it." });
        }

        var confirmed = await ConfirmedCollectionAmountAsync(db, context.Bill.Id, cancellationToken);
        if (request.AmountReceived > context.Bill.Total - confirmed)
        {
            return BadRequest("The corrected amount exceeds the bill's remaining balance.");
        }

        var before = CollectionSnapshot(context.Report);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            context.Report.Correct(actor.UserId, request.AmountReceived, request.ReceivedDate, request.PaymentMethod, request.ReferenceNote);
            db.CollectionAuditEntries.Add(new CollectionAuditEntry(
                actor.OrganizationId,
                context.Report.Id,
                null,
                actor.UserId,
                CollectionAuditAction.Corrected,
                request.Reason,
                JsonSerializer.Serialize(before),
                JsonSerializer.Serialize(CollectionSnapshot(context.Report))));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var corrected = await LoadCollectionContextAsync(db, actor, reportId, cancellationToken);
            return Results.Ok(await ToCollectionReportResponseAsync(db, corrected!, cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { message = "This collection report changed concurrently. Refresh before correcting it." });
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(exception.Message);
        }
    }

    private static async Task<IResult> ReturnCollectionReportAsync(
        Guid reportId,
        CollectionActionRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReviewCollections, cancellationToken))
        {
            return Results.Forbid();
        }

        var context = await LoadCollectionContextAsync(db, actor, reportId, cancellationToken);
        if (context is null)
        {
            return Results.NotFound();
        }

        if (context.Report.Version != request.ExpectedVersion)
        {
            return Results.Conflict(new { message = "This collection report changed since it was loaded. Refresh before returning it." });
        }

        var before = CollectionSnapshot(context.Report);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            context.Report.Return(actor.UserId, request.Reason ?? string.Empty);
            db.CollectionAuditEntries.Add(new CollectionAuditEntry(actor.OrganizationId, context.Report.Id, null, actor.UserId, CollectionAuditAction.Returned, request.Reason, JsonSerializer.Serialize(before), JsonSerializer.Serialize(CollectionSnapshot(context.Report))));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var returned = await LoadCollectionContextAsync(db, actor, reportId, cancellationToken);
            return Results.Ok(await ToCollectionReportResponseAsync(db, returned!, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(exception.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { message = "This collection report changed concurrently. Refresh before returning it." });
        }
    }

    private static async Task<IResult> ApproveCollectionReportAsync(
        Guid reportId,
        CollectionActionRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReviewCollections, cancellationToken))
        {
            return Results.Forbid();
        }

        var context = await LoadCollectionContextAsync(db, actor, reportId, cancellationToken);
        if (context is null)
        {
            return Results.NotFound();
        }

        if (context.Report.Version != request.ExpectedVersion)
        {
            return Results.Conflict(new { message = "This collection report changed since it was loaded. Refresh before approving it." });
        }

        if (context.Report.Status != CollectionReportStatus.Pending)
        {
            return BadRequest("Only pending collection reports can be approved.");
        }

        if (context.Report.LastChangedByUserId == actor.UserId)
        {
            return SelfApprovalForbidden();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var lockedBill = await LockBillingUnitBillAsync(db, context.Bill.Id, cancellationToken);
            if (lockedBill is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Results.NotFound();
            }

            var report = await db.CollectionReports.FirstAsync(item => item.Id == reportId, cancellationToken);
            if (report.Version != request.ExpectedVersion)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Results.Conflict(new { message = "This collection report changed concurrently. Refresh before approving it." });
            }

            if (report.Status != CollectionReportStatus.Pending)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BadRequest("Only pending collection reports can be approved.");
            }

            if (report.LastChangedByUserId == actor.UserId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return SelfApprovalForbidden();
            }

            var confirmed = await ConfirmedCollectionAmountAsync(db, lockedBill.Id, cancellationToken);
            if (report.AmountReceived > lockedBill.Total - confirmed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BadRequest("The reported amount exceeds the bill's remaining balance.");
            }

            var before = CollectionSnapshot(report);
            report.Approve(actor.UserId);
            db.CollectionAuditEntries.Add(new CollectionAuditEntry(actor.OrganizationId, report.Id, null, actor.UserId, CollectionAuditAction.Approved, request.Reason, JsonSerializer.Serialize(before), JsonSerializer.Serialize(CollectionSnapshot(report))));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var approved = await LoadCollectionContextAsync(db, actor, reportId, cancellationToken);
            return Results.Ok(await ToCollectionReportResponseAsync(db, approved!, cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { message = "This collection report changed concurrently. Refresh before approving it." });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(detail: "The collection approval could not be persisted atomically. No balance change was recorded.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> RequestCollectionReversalAsync(
        Guid reportId,
        CollectionActionRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.RequestCollectionReversals, cancellationToken))
        {
            return Results.Forbid();
        }

        var context = await LoadCollectionContextAsync(db, actor, reportId, cancellationToken);
        if (context is null)
        {
            return Results.NotFound();
        }

        if (!actor.IsAdmin && context.Report.ReporterUserId != actor.UserId)
        {
            return Results.Forbid();
        }

        if (context.Report.Status != CollectionReportStatus.Approved)
        {
            return BadRequest("Only approved collection reports can be reversed.");
        }

        if (context.Report.Version != request.ExpectedVersion)
        {
            return Results.Conflict(new { message = "This collection report changed since it was loaded. Refresh before requesting a reversal." });
        }

        var existing = await db.CollectionReversals.FirstOrDefaultAsync(item =>
            item.OrganizationId == actor.OrganizationId && item.CollectionReportId == reportId, cancellationToken);
        if (existing is not null)
        {
            return Results.Conflict(new { message = "This approved collection already has a reversal workflow." });
        }

        CollectionReversal reversal;
        try
        {
            reversal = new CollectionReversal(actor.OrganizationId, reportId, actor.UserId, request.Reason ?? string.Empty);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.CollectionReversals.Add(reversal);
            db.CollectionAuditEntries.Add(new CollectionAuditEntry(actor.OrganizationId, reportId, reversal.Id, actor.UserId, CollectionAuditAction.ReversalRequested, request.Reason, null, JsonSerializer.Serialize(new { reversal.Id, reversal.Status, reversal.Reason })));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var requested = await LoadCollectionContextAsync(db, actor, reportId, cancellationToken);
            return Results.Ok(await ToCollectionReportResponseAsync(db, requested!, cancellationToken));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { message = "This approved collection already has a reversal workflow." });
        }
    }

    private static async Task<IResult> ResubmitCollectionReversalAsync(
        Guid reversalId,
        CollectionActionRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.RequestCollectionReversals, cancellationToken))
        {
            return Results.Forbid();
        }

        var reversal = await db.CollectionReversals.FirstOrDefaultAsync(item => item.Id == reversalId && item.OrganizationId == actor.OrganizationId, cancellationToken);
        if (reversal is null)
        {
            return Results.NotFound();
        }

        var context = await LoadCollectionContextAsync(db, actor, reversal.CollectionReportId, cancellationToken);
        if (context is null)
        {
            return Results.NotFound();
        }

        if (!actor.IsAdmin && (reversal.RequestedByUserId != actor.UserId || context.Report.ReporterUserId != actor.UserId))
        {
            return Results.Forbid();
        }

        if (reversal.Version != request.ExpectedVersion)
        {
            return Results.Conflict(new { message = "This reversal changed since it was loaded. Refresh before resubmitting it." });
        }

        if (reversal.Status != CollectionReversalStatus.Returned)
        {
            return BadRequest("Only returned reversals can be resubmitted.");
        }

        var before = new { reversal.Status, reversal.Version, reversal.ReturnReason };
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            reversal.Resubmit(actor.UserId, request.Reason);
            db.CollectionAuditEntries.Add(new CollectionAuditEntry(actor.OrganizationId, context.Report.Id, reversal.Id, actor.UserId, CollectionAuditAction.ReversalRequested, request.Reason, JsonSerializer.Serialize(before), JsonSerializer.Serialize(new { reversal.Status, reversal.Version, reversal.Reason })));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var resubmitted = await LoadCollectionContextAsync(db, actor, context.Report.Id, cancellationToken);
            return Results.Ok(await ToCollectionReportResponseAsync(db, resubmitted!, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(exception.Message);
        }
    }

    private static async Task<IResult> ReturnCollectionReversalAsync(
        Guid reversalId,
        CollectionActionRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReviewCollectionReversals, cancellationToken))
        {
            return Results.Forbid();
        }

        var reversal = await db.CollectionReversals.FirstOrDefaultAsync(item => item.Id == reversalId && item.OrganizationId == actor.OrganizationId, cancellationToken);
        if (reversal is null)
        {
            return Results.NotFound();
        }

        var context = await LoadCollectionContextAsync(db, actor, reversal.CollectionReportId, cancellationToken);
        if (context is null)
        {
            return Results.NotFound();
        }

        if (reversal.Version != request.ExpectedVersion)
        {
            return Results.Conflict(new { message = "This reversal changed since it was loaded. Refresh before returning it." });
        }

        var before = new { reversal.Status, reversal.Version, reversal.ReturnReason };
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            reversal.Return(actor.UserId, request.Reason ?? string.Empty);
            db.CollectionAuditEntries.Add(new CollectionAuditEntry(actor.OrganizationId, context.Report.Id, reversal.Id, actor.UserId, CollectionAuditAction.ReversalReturned, request.Reason, JsonSerializer.Serialize(before), JsonSerializer.Serialize(new { reversal.Status, reversal.Version, reversal.ReturnReason })));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var returned = await LoadCollectionContextAsync(db, actor, context.Report.Id, cancellationToken);
            return Results.Ok(await ToCollectionReportResponseAsync(db, returned!, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(exception.Message);
        }
    }

    private static async Task<IResult> ApproveCollectionReversalAsync(
        Guid reversalId,
        CollectionActionRequest request,
        HttpContext httpContext,
        PropertyManagementDbContext db,
        CancellationToken cancellationToken)
    {
        var actor = httpContext.User.GetRequestUser();
        if (actor is null)
        {
            return Results.Unauthorized();
        }

        if (!await OrganizationPermissionAuthorization.HasAsync(db, actor, OrganizationPermission.ReviewCollectionReversals, cancellationToken))
        {
            return Results.Forbid();
        }

        var reversal = await db.CollectionReversals.FirstOrDefaultAsync(item => item.Id == reversalId && item.OrganizationId == actor.OrganizationId, cancellationToken);
        if (reversal is null)
        {
            return Results.NotFound();
        }

        var context = await LoadCollectionContextAsync(db, actor, reversal.CollectionReportId, cancellationToken);
        if (context is null)
        {
            return Results.NotFound();
        }

        if (reversal.Version != request.ExpectedVersion)
        {
            return Results.Conflict(new { message = "This reversal changed since it was loaded. Refresh before approving it." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var lockedBill = await LockBillingUnitBillAsync(db, context.Bill.Id, cancellationToken);
            if (lockedBill is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Results.NotFound();
            }

            var currentReversal = await db.CollectionReversals.FirstAsync(item => item.Id == reversalId, cancellationToken);
            if (currentReversal.Version != request.ExpectedVersion)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Results.Conflict(new { message = "This reversal changed concurrently. Refresh before approving it." });
            }

            if (await db.CollectionReversals.AnyAsync(item => item.CollectionReportId == context.Report.Id && item.Status == CollectionReversalStatus.Approved, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Results.Conflict(new { message = "This collection has already been reversed." });
            }

            var before = new { currentReversal.Status, currentReversal.Version };
            currentReversal.Approve(actor.UserId);
            db.CollectionAuditEntries.Add(new CollectionAuditEntry(actor.OrganizationId, context.Report.Id, currentReversal.Id, actor.UserId, CollectionAuditAction.ReversalApproved, request.Reason, JsonSerializer.Serialize(before), JsonSerializer.Serialize(new { currentReversal.Status, currentReversal.Version })));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var approved = await LoadCollectionContextAsync(db, actor, context.Report.Id, cancellationToken);
            return Results.Ok(await ToCollectionReportResponseAsync(db, approved!, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(detail: exception.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { message = "This reversal changed concurrently. Refresh before approving it." });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(detail: "The reversal approval could not be persisted atomically. No balance change was recorded.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<ApprovedBillContext?> LoadApprovedBillContextAsync(
        PropertyManagementDbContext db,
        RequestUser actor,
        Guid propertyId,
        Guid billingUnitBillId,
        CancellationToken cancellationToken)
    {
        var property = await AccessibleProperties(db, actor, includeArchived: false)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return null;
        }

        var result = await (
            from bill in db.BillingUnitBills
            join cycle in db.BillingCycles on bill.BillingCycleId equals cycle.Id
            where bill.Id == billingUnitBillId
                && bill.OrganizationId == actor.OrganizationId
                && cycle.OrganizationId == actor.OrganizationId
                && cycle.PropertyId == propertyId
                && cycle.Status == BillingCycleStatus.Approved
            select new ApprovedBillContext(bill, cycle))
            .FirstOrDefaultAsync(cancellationToken);
        return result;
    }

    private static async Task<CollectionContext?> LoadCollectionContextAsync(
        PropertyManagementDbContext db,
        RequestUser actor,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var report = await db.CollectionReports.FirstOrDefaultAsync(item => item.Id == reportId && item.OrganizationId == actor.OrganizationId, cancellationToken);
        if (report is null)
        {
            return null;
        }

        var bill = await db.BillingUnitBills.FirstOrDefaultAsync(item => item.Id == report.BillingUnitBillId && item.OrganizationId == actor.OrganizationId, cancellationToken);
        var cycle = bill is null
            ? null
            : await db.BillingCycles.FirstOrDefaultAsync(item => item.Id == bill.BillingCycleId && item.OrganizationId == actor.OrganizationId && item.Status == BillingCycleStatus.Approved, cancellationToken);
        var property = cycle is null
            ? null
            : await AccessibleProperties(db, actor, includeArchived: false).FirstOrDefaultAsync(item => item.Id == cycle.PropertyId, cancellationToken);
        return bill is null || cycle is null || property is null
            ? null
            : new CollectionContext(report, bill, cycle);
    }

    private static async Task<CollectionQueueResponse> ToCollectionQueueResponseAsync(
        PropertyManagementDbContext db,
        IReadOnlyList<CollectionQueryRow> rows,
        IReadOnlyList<BillingUnitBill> approvedBills,
        CancellationToken cancellationToken)
    {
        var billIds = approvedBills.Select(item => item.Id).ToArray();
        var allReports = billIds.Length == 0
            ? []
            : await db.CollectionReports.AsNoTracking().Where(item => billIds.Contains(item.BillingUnitBillId)).ToListAsync(cancellationToken);
        var reversedReportIds = await db.CollectionReversals.AsNoTracking()
            .Where(item => allReports.Select(report => report.Id).Contains(item.CollectionReportId) && item.Status == CollectionReversalStatus.Approved)
            .Select(item => item.CollectionReportId)
            .ToListAsync(cancellationToken);
        var confirmedByBill = allReports
            .Where(item => item.Status == CollectionReportStatus.Approved && !reversedReportIds.Contains(item.Id))
            .GroupBy(item => item.BillingUnitBillId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.AmountReceived));
        var pendingByBill = allReports
            .Where(item => item.Status is CollectionReportStatus.Pending or CollectionReportStatus.Returned)
            .GroupBy(item => item.BillingUnitBillId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.AmountReceived));

        var items = new List<CollectionReportResponse>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(await ToCollectionReportResponseAsync(db, new CollectionContext(row.Report, row.Bill, row.Cycle), cancellationToken, confirmedByBill.GetValueOrDefault(row.Bill.Id), reversedReportIds.Contains(row.Report.Id)));
        }

        var billedTotal = approvedBills.Sum(item => item.Total);
        var confirmedTotal = confirmedByBill.Values.Sum();
        var pendingTotal = pendingByBill.Values.Sum();
        return new CollectionQueueResponse(items, billedTotal, pendingTotal, confirmedTotal, Math.Max(0m, billedTotal - confirmedTotal));
    }

    private static async Task<CollectionReportResponse> ToCollectionReportResponseAsync(
        PropertyManagementDbContext db,
        CollectionContext context,
        CancellationToken cancellationToken,
        decimal? confirmedOverride = null,
        bool? reversedOverride = null)
    {
        var confirmed = confirmedOverride ?? await ConfirmedCollectionAmountAsync(db, context.Bill.Id, cancellationToken);
        var reversal = await db.CollectionReversals.AsNoTracking()
            .Where(item => item.OrganizationId == context.Report.OrganizationId && item.CollectionReportId == context.Report.Id)
            .OrderByDescending(item => item.LastChangedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var reporterName = await db.Users.AsNoTracking()
            .Where(user => user.Id == context.Report.ReporterUserId && user.OrganizationId == context.Report.OrganizationId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Unknown reporter";
        var audit = await db.CollectionAuditEntries.AsNoTracking()
            .Where(item => item.CollectionReportId == context.Report.Id)
            .OrderBy(item => item.OccurredAtUtc)
            .Select(item => new CollectionAuditResponse(item.Id, item.CollectionReversalId, item.ActorUserId, item.Action, item.OccurredAtUtc, item.Reason, item.BeforeJson, item.AfterJson))
            .ToListAsync(cancellationToken);
        var isReversed = reversedOverride ?? reversal?.Status == CollectionReversalStatus.Approved;
        return new CollectionReportResponse(
            context.Report.Id,
            context.Report.BillingUnitBillId,
            context.Cycle.Id,
            context.Cycle.PropertyId,
            context.Cycle.BillingMonth,
            context.Report.UnitId,
            context.Report.UnitNameSnapshot,
            context.Report.FloorNameSnapshot,
            context.Report.OccupancyAssignmentIdSnapshot,
            context.Report.TenantIdSnapshot,
            context.Report.TenantNameSnapshot,
            context.Report.TenantPhoneSnapshot,
            context.Report.BilledAmountSnapshot,
            confirmed,
            Math.Max(0m, context.Report.BilledAmountSnapshot - confirmed),
            context.Report.AmountReceived,
            context.Report.ReceivedDate,
            context.Report.PaymentMethod,
            context.Report.ReferenceNote,
            context.Report.ReporterUserId,
            reporterName,
            context.Report.SubmittedAtUtc,
            context.Report.Status,
            context.Report.Version,
            context.Report.ReturnReason,
            context.Report.ApprovedByUserId,
            context.Report.ApprovedAtUtc,
            isReversed,
            reversal?.Status,
            reversal?.Id,
             reversal?.Version,
            reversal?.Reason,
            audit);
    }

    private static async Task<decimal> ConfirmedCollectionAmountAsync(PropertyManagementDbContext db, Guid billingUnitBillId, CancellationToken cancellationToken)
    {
        var reversedReportIds = db.CollectionReversals
            .Where(item => item.CollectionReportId != Guid.Empty && item.Status == CollectionReversalStatus.Approved)
            .Select(item => item.CollectionReportId);
        return await db.CollectionReports
            .Where(item => item.BillingUnitBillId == billingUnitBillId && item.Status == CollectionReportStatus.Approved && !reversedReportIds.Contains(item.Id))
            .Select(item => (decimal?)item.AmountReceived)
            .SumAsync(cancellationToken) ?? 0m;
    }

    private static string MeterReadingStatus(BillingCycleStatus? cycleStatus, bool electricityApplicable, decimal? currentReading)
    {
        if (!electricityApplicable)
        {
            return "NotApplicable";
        }

        return cycleStatus switch
        {
            BillingCycleStatus.Submitted => "AwaitingReview",
            BillingCycleStatus.Approved => "Approved",
            BillingCycleStatus.Returned => currentReading is null ? "Missing" : "Returned",
            BillingCycleStatus.Draft => currentReading is null ? "Missing" : "SavedDraft",
            _ => "NoDraft",
        };
    }

    private static string CollectionStatus(BillingUnitBill? bill, decimal confirmed, decimal pending)
    {
        if (bill is null)
        {
            return "NoApprovedBill";
        }

        if (pending > 0m)
        {
            return "PendingApproval";
        }

        if (confirmed <= 0m)
        {
            return "Outstanding";
        }

        return confirmed >= bill.Total ? "FullyCollected" : "PartiallyCollected";
    }

    private static DateOnly PropertyLocalDate(string timeZoneId)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }
        catch (InvalidTimeZoneException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }
    }

    private static async Task<RoomMeterIssueResponse> ToRoomMeterIssueResponseAsync(
        PropertyManagementDbContext db,
        RoomMeterIssue issue,
        CancellationToken cancellationToken)
    {
        var unit = issue.UnitId.HasValue
            ? await db.Units.AsNoTracking().FirstOrDefaultAsync(item => item.Id == issue.UnitId.Value, cancellationToken)
            : null;
        var floorName = unit is null
            ? null
            : await db.Floors.AsNoTracking().Where(item => item.Id == unit.FloorId).Select(item => item.Name).FirstOrDefaultAsync(cancellationToken);
        var reporterName = await db.Users.AsNoTracking()
            .Where(item => item.Id == issue.ReporterUserId && item.OrganizationId == issue.OrganizationId)
            .Select(item => item.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Unknown reporter";
        var audit = await db.RoomMeterIssueAuditEntries.AsNoTracking()
            .Where(item => item.RoomMeterIssueId == issue.Id)
            .OrderBy(item => item.OccurredAtUtc)
            .Select(item => new RoomMeterIssueAuditResponse(item.Id, item.ActorUserId, item.Action, item.OccurredAtUtc, item.Reason, item.BeforeJson, item.AfterJson))
            .ToListAsync(cancellationToken);
        return new RoomMeterIssueResponse(
            issue.Id,
            issue.PropertyId,
            issue.UnitId,
            unit?.Name ?? "Building/Common Area",
            floorName,
            issue.ReporterUserId,
            reporterName,
            issue.IssueType,
            issue.Description,
            issue.ObservedDate,
            issue.CreatedAtUtc,
            issue.Status,
            issue.Version,
            issue.ResolutionReason,
            audit,
            issue.Target,
            issue.IssueType is null);
    }

    private static object RoomMeterIssueSnapshot(RoomMeterIssue issue) => new
    {
        issue.Id,
        issue.PropertyId,
        issue.UnitId,
        issue.Target,
        issue.ReporterUserId,
        issue.IssueType,
        issue.Description,
        issue.ObservedDate,
        issue.Status,
        issue.Version,
        issue.ResolutionReason,
    };

    private static async Task<BillingUnitBill?> LockBillingUnitBillAsync(PropertyManagementDbContext db, Guid billingUnitBillId, CancellationToken cancellationToken)
    {
        if (db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            return await db.BillingUnitBills
                .FromSqlInterpolated($"SELECT * FROM \"billing_unit_bills\" WHERE \"Id\" = {billingUnitBillId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        }

        return await db.BillingUnitBills.SingleOrDefaultAsync(item => item.Id == billingUnitBillId, cancellationToken);
    }

    private static object CollectionSnapshot(CollectionReport report) => new
    {
        report.Id,
        report.BillingUnitBillId,
        report.AmountReceived,
        report.ReceivedDate,
        report.PaymentMethod,
        report.ReferenceNote,
        report.Status,
        report.Version,
        report.LastChangedByUserId,
    };

    private sealed record ApprovedBillContext(BillingUnitBill Bill, BillingCycle Cycle);

    private sealed record CollectionContext(CollectionReport Report, BillingUnitBill Bill, BillingCycle Cycle);

    private sealed record CollectionQueryRow(CollectionReport Report, BillingUnitBill Bill, BillingCycle Cycle);

    private static async Task<(Property? Property, BillingCycle? Cycle)> LoadApprovedReportCycleAsync(
        PropertyManagementDbContext db,
        RequestUser actor,
        Guid propertyId,
        DateOnly billingMonth,
        CancellationToken cancellationToken)
    {
        var property = await AccessibleProperties(db, actor, includeArchived: false)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return (null, null);
        }

        var cycle = await db.BillingCycles.AsNoTracking().FirstOrDefaultAsync(item =>
            item.OrganizationId == actor.OrganizationId
            && item.PropertyId == propertyId
            && item.BillingMonth == billingMonth
            && item.Status == BillingCycleStatus.Approved, cancellationToken);
        return (property, cycle);
    }

    private static string? ApprovedReportMetadataError(BillingCycle cycle)
    {
        if (cycle.ApprovedAtUtc is null || cycle.ApprovedTotal is null)
        {
            return "This approved cycle is missing approval metadata and cannot be printed as an official statement.";
        }

        if (string.IsNullOrWhiteSpace(cycle.PropertyNameSnapshot) || string.IsNullOrWhiteSpace(cycle.PropertyAddressSnapshot))
        {
            return "This approved cycle is missing historical property snapshots; the report cannot safely substitute current property data.";
        }

        return null;
    }

    private static BillingStatementResponse ToBillingStatementResponse(
        BillingCycle cycle,
        string statementType,
        Guid? tenantId,
        IReadOnlyList<BillingStatementUnitResponse> bills) =>
        new(
            statementType,
            cycle.PropertyId,
            cycle.Id,
            cycle.PropertyNameSnapshot!,
            cycle.PropertyAddressSnapshot!,
            cycle.BillingMonth,
            cycle.ApprovedAtUtc!.Value,
            cycle.ApprovedTotal!.Value,
            tenantId,
            bills);

    private static BillingStatementUnitResponse ToBillingStatementUnitResponse(
        BillingUnitBill bill,
        IEnumerable<BillingChargeLine> lines) =>
        new(
            $"PT-{bill.Id:N}",
            bill.Id,
            bill.UnitId,
            bill.UnitNameSnapshot,
            bill.FloorNameSnapshot,
            bill.OccupancyAssignmentId,
            bill.TenantId,
            bill.TenantNameSnapshot,
            bill.TenantPhoneSnapshot,
            bill.OccupancyStartDate,
            bill.OccupancyEndDate,
            bill.Total,
            lines.Select(line => new BillingStatementChargeResponse(
                line.Id,
                line.ChargeType,
                line.ChargeNameSnapshot,
                line.SettingModeSnapshot,
                line.IsMetered,
                line.RateSnapshot,
                line.PreviousReading,
                line.PreviousReadingDate,
                line.CurrentReading,
                line.CurrentReadingDate,
                line.ConsumptionUnits,
                line.WarningEnabled,
                line.WarningThreshold,
                line.Amount)).ToList());

    private static async Task<ComparisonSource> LoadComparisonSourceAsync(
        PropertyManagementDbContext db,
        Guid organizationId,
        Guid billingCycleId,
        CancellationToken cancellationToken)
    {
        var bills = await db.BillingUnitBills.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && item.BillingCycleId == billingCycleId)
            .ToListAsync(cancellationToken);
        var billIds = bills.Select(item => item.Id).ToArray();
        var lines = await db.BillingChargeLines.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId && billIds.Contains(item.BillingUnitBillId))
            .ToListAsync(cancellationToken);
        return new ComparisonSource(bills, lines);
    }

    private static Dictionary<ChargeMetricKey, ChargeMetricBucket> BuildChargeBuckets(ComparisonSource source)
    {
        var unitByBill = source.Bills.ToDictionary(item => item.Id, item => item.UnitId);
        return source.Lines
            .GroupBy(line => new ChargeMetricKey(line.ChargeType, line.ChargeNameSnapshot))
            .ToDictionary(
                group => group.Key,
                group => new ChargeMetricBucket(
                    RoundMoney(group.Sum(line => line.Amount)),
                    group.Where(line => unitByBill.ContainsKey(line.BillingUnitBillId)).Select(line => unitByBill[line.BillingUnitBillId]).ToHashSet()));
    }

    private static BillingComparisonMetricResponse ToComparisonMetric(
        ChargeMetricKey key,
        ChargeMetricBucket? current,
        ChargeMetricBucket? previous)
    {
        var values = ToComparisonValues(current?.Amount, previous?.Amount, current is not null && previous is not null);
        return new BillingComparisonMetricResponse(
            $"{key.ChargeType}:{key.Label}",
            key.ChargeType,
            key.Label,
            current?.Amount,
            previous?.Amount,
            values.Difference,
            values.PercentageChange,
            values.PercentageChangeAvailable,
            current?.UnitIds.Count ?? 0,
            previous?.UnitIds.Count ?? 0,
            current is null || previous is null || !current.UnitIds.SetEquals(previous.UnitIds));
    }

    private static BillingComparisonConsumptionResponse ToConsumptionComparison(ComparisonSource current, ComparisonSource? previous)
    {
        var currentLines = current.Lines.Where(line => line.ChargeType == ChargeType.Electricity).ToList();
        var previousLines = previous?.Lines.Where(line => line.ChargeType == ChargeType.Electricity).ToList() ?? [];
        var currentUnitsByBill = current.Bills.ToDictionary(item => item.Id, item => item.UnitId);
        var previousUnitsByBill = previous?.Bills.ToDictionary(item => item.Id, item => item.UnitId) ?? [];
        var currentUnitIds = currentLines.Where(line => currentUnitsByBill.ContainsKey(line.BillingUnitBillId)).Select(line => currentUnitsByBill[line.BillingUnitBillId]).ToHashSet();
        var previousUnitIds = previousLines.Where(line => previousUnitsByBill.ContainsKey(line.BillingUnitBillId)).Select(line => previousUnitsByBill[line.BillingUnitBillId]).ToHashSet();
        decimal? currentValue = currentLines.Count > 0 && currentLines.All(line => line.ConsumptionUnits is not null)
            ? RoundMoney(currentLines.Sum(line => line.ConsumptionUnits!.Value))
            : null;
        decimal? previousValue = previous is not null && previousLines.Count > 0 && previousLines.All(line => line.ConsumptionUnits is not null)
            ? RoundMoney(previousLines.Sum(line => line.ConsumptionUnits!.Value))
            : null;
        var values = ToComparisonValues(currentValue, previousValue, previous is not null);
        return new BillingComparisonConsumptionResponse(
            currentValue,
            previousValue,
            values.Difference,
            values.PercentageChange,
            values.PercentageChangeAvailable,
            currentUnitIds.Count,
            previousUnitIds.Count,
            previous is null || !currentUnitIds.SetEquals(previousUnitIds));
    }

    private static IReadOnlyList<string> BuildCoverageNotes(ComparisonSource current, ComparisonSource? previous, string? incompleteReason)
    {
        var notes = new List<string>();
        if (incompleteReason is not null)
        {
            notes.Add(incompleteReason);
            return notes;
        }

        var currentUnits = current.Bills.Select(item => item.UnitId).ToHashSet();
        var previousUnits = previous!.Bills.Select(item => item.UnitId).ToHashSet();
        var added = currentUnits.Except(previousUnits).Count();
        var removed = previousUnits.Except(currentUnits).Count();
        if (added > 0 || removed > 0)
        {
            notes.Add($"Unit coverage changed: {added} added and {removed} removed from the approved snapshot.");
        }

        var currentKeys = BuildChargeBuckets(current).Keys.ToHashSet();
        var previousKeys = BuildChargeBuckets(previous).Keys.ToHashSet();
        var addedCharges = currentKeys.Except(previousKeys).Count();
        var removedCharges = previousKeys.Except(currentKeys).Count();
        if (addedCharges > 0 || removedCharges > 0)
        {
            notes.Add($"Applicable charge coverage changed: {addedCharges} categories added and {removedCharges} removed from the approved snapshot.");
        }

        if (notes.Count == 0)
        {
            notes.Add("Unit and applicable-charge coverage matches the immediately preceding approved snapshot.");
        }

        return notes;
    }

    private static ComparisonValues ToComparisonValues(decimal? current, decimal? previous, bool previousAvailable)
    {
        if (!previousAvailable || current is null || previous is null)
        {
            return new ComparisonValues(null, null, false);
        }

        if (previous.Value == 0m)
        {
            return new ComparisonValues(RoundMoney(current.Value - previous.Value), null, false);
        }

        return new ComparisonValues(
            RoundMoney(current.Value - previous.Value),
            decimal.Round((current.Value - previous.Value) / previous.Value * 100m, 2, MidpointRounding.AwayFromZero),
            true);
    }

    private static async Task<BillingCycle?> AccessibleBillingCycleAsync(
        PropertyManagementDbContext db,
        RequestUser actor,
        Guid propertyId,
        Guid billingCycleId,
        CancellationToken cancellationToken) =>
        await db.BillingCycles.FirstOrDefaultAsync(cycle => cycle.Id == billingCycleId && cycle.PropertyId == propertyId && cycle.OrganizationId == actor.OrganizationId
            && db.Properties.Any(property => property.Id == propertyId && property.OrganizationId == actor.OrganizationId && !property.IsArchived)
            && (actor.IsAdmin || db.StaffPropertyAssignments.Any(assignment => assignment.OrganizationId == actor.OrganizationId && assignment.UserId == actor.UserId && assignment.PropertyId == propertyId)), cancellationToken);

    private static async Task<BillingCycleResponse> ToBillingCycleResponseAsync(PropertyManagementDbContext db, Guid billingCycleId, CancellationToken cancellationToken)
    {
        var cycle = await db.BillingCycles.AsNoTracking().FirstAsync(item => item.Id == billingCycleId, cancellationToken);
        var bills = await db.BillingUnitBills.AsNoTracking().Where(item => item.BillingCycleId == billingCycleId).OrderBy(item => item.UnitNameSnapshot).ToListAsync(cancellationToken);
        var billIds = bills.Select(item => item.Id).ToArray();
        var lines = await db.BillingChargeLines.AsNoTracking().Where(item => billIds.Contains(item.BillingUnitBillId)).OrderBy(item => item.ChargeType).ToListAsync(cancellationToken);
        var audit = await db.BillingAuditEntries.AsNoTracking().Where(item => item.BillingCycleId == billingCycleId).OrderBy(item => item.OccurredAtUtc).ToListAsync(cancellationToken);
        var billResponses = bills.Select(bill => new BillingUnitBillResponse(
            bill.Id,
            bill.UnitId,
            bill.UnitNameSnapshot,
            bill.OccupancyAssignmentId,
            bill.TenantId,
            bill.TenantNameSnapshot,
            bill.TenantPhoneSnapshot,
            bill.OccupancyStartDate,
            bill.OccupancyEndDate,
            bill.BlockingReason,
            bill.Total,
            bill.ElectricityWarning,
            lines.Where(line => line.BillingUnitBillId == bill.Id).Select(line => new BillingChargeLineResponse(
                line.Id,
                line.ChargeType,
                line.ChargeNameSnapshot,
                line.SettingModeSnapshot,
                line.IsMetered,
                line.RateSnapshot,
                line.PreviousReading,
                line.PreviousReadingDate,
                line.CurrentReading,
                line.CurrentReadingDate,
                line.ConsumptionUnits,
                line.PreviousReadingSource,
                line.WarningEnabled,
                line.WarningThreshold,
                line.Amount)).ToList())).ToList();
        return new BillingCycleResponse(
            cycle.Id,
            cycle.PropertyId,
            cycle.BillingMonth,
            cycle.Status,
            cycle.Version,
            cycle.LastChangedByUserId,
            cycle.LastChangedAtUtc,
            cycle.ReturnReason,
            RoundMoney(billResponses.Sum(bill => bill.Total)),
            cycle.ApprovedTotal,
            billResponses.Count(bill => bill.BlockingReason is not null),
            billResponses,
            audit.Select(item => new BillingAuditResponse(item.Id, item.ActorUserId, item.Action, item.OccurredAtUtc, item.Reason, item.BeforeJson, item.AfterJson)).ToList());
    }

    private static IReadOnlyList<EffectiveCharge> ResolveEffectiveCharges(IReadOnlyList<ChargeConfiguration> configurations, Guid unitId, DateOnly billingMonth)
    {
        var result = new List<EffectiveCharge>();
        foreach (var chargeType in Enum.GetValues<ChargeType>())
        {
            var propertySetting = configurations.Where(item => item.UnitId is null && item.ChargeType == chargeType && item.BillingMonth <= billingMonth).OrderByDescending(item => item.BillingMonth).FirstOrDefault();
            var unitSetting = configurations.Where(item => item.UnitId == unitId && item.ChargeType == chargeType && item.BillingMonth <= billingMonth).OrderByDescending(item => item.BillingMonth).FirstOrDefault();
            var selected = unitSetting?.Mode switch
            {
                ChargeSettingMode.Disabled => null,
                ChargeSettingMode.Custom => unitSetting,
                ChargeSettingMode.Inherited => propertySetting,
                _ => propertySetting,
            };
            if (selected is null || selected.Mode == ChargeSettingMode.Disabled || selected.Amount is null)
            {
                continue;
            }

            var mode = unitSetting?.Mode == ChargeSettingMode.Custom ? ChargeSettingMode.Custom : selected.Mode;
            var warningSetting = unitSetting?.WarningMode switch
            {
                ChargeSettingMode.Disabled => null,
                ChargeSettingMode.Custom => unitSetting,
                ChargeSettingMode.Inherited => propertySetting,
                _ => propertySetting,
            };
            var warningMode = chargeType == ChargeType.Electricity
                ? unitSetting?.WarningMode switch
                {
                    ChargeSettingMode.Disabled => ChargeSettingMode.Disabled,
                    ChargeSettingMode.Custom => ChargeSettingMode.Custom,
                    ChargeSettingMode.Inherited => propertySetting?.WarningMode ?? ChargeSettingMode.Custom,
                    _ => propertySetting?.WarningMode ?? ChargeSettingMode.Custom,
                }
                : ChargeSettingMode.Disabled;
            var warningThreshold = warningMode == ChargeSettingMode.Custom
                ? warningSetting?.WarningThreshold ?? 200m
                : (decimal?)null;
            result.Add(new EffectiveCharge(chargeType, ChargeName(chargeType, selected.CustomLabel), mode, selected.Amount.Value, warningMode, warningThreshold));
        }

        return result;
    }

    private static string ChargeName(ChargeType chargeType, string? customLabel) =>
        chargeType == ChargeType.Custom && !string.IsNullOrWhiteSpace(customLabel)
            ? customLabel.Trim()
            : chargeType switch
            {
                ChargeType.Electricity => "Electricity",
                ChargeType.Water => "Water",
                ChargeType.Gas => "Gas",
                ChargeType.ServiceCharge => "Service charge",
                ChargeType.Rent => "Rent",
                _ => "Custom charge",
            };

    private static void RecalculateBillState(BillingUnitBill bill, IEnumerable<BillingChargeLine> chargeLines)
    {
        var lines = chargeLines.ToList();
        var electricity = lines.FirstOrDefault(line => line.ChargeType == ChargeType.Electricity);
        var blockingReason = bill.BlockingReason;
        if (bill.OccupancyAssignmentId is not null)
        {
            blockingReason = electricity is null
                ? null
                : electricity.CurrentReading is null
                    ? electricity.PreviousReadingSource == MeterReadingSource.Missing
                        ? "Opening electricity reading and current reading are required; no immediately preceding approved period is available."
                        : "Current electricity reading is required."
                    : electricity.PreviousReading is null
                        ? "Opening electricity reading is required."
                        : null;
        }

        var total = RoundMoney(lines.Sum(line => line.Amount));
        bill.SetCalculation(
            total,
            electricity is not null
            && electricity.CurrentReading is not null
            && electricity.WarningEnabled
            && electricity.WarningThreshold is not null
            && electricity.Amount < electricity.WarningThreshold.Value);
        bill.SetBlockingReason(blockingReason);
    }

    private static decimal RoundMoney(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    private sealed record OccupancySnapshot(OccupancyAssignment Assignment, string? TenantName, string? TenantPhone);

    private sealed record ComparisonSource(IReadOnlyList<BillingUnitBill> Bills, IReadOnlyList<BillingChargeLine> Lines);

    private sealed record ChargeMetricKey(ChargeType ChargeType, string Label);

    private sealed record ChargeMetricBucket(decimal Amount, HashSet<Guid> UnitIds);

    private sealed record RoomMeterIssueCount(Guid UnitId, int Total, int Open);

    private sealed record ComparisonValues(decimal? Difference, decimal? PercentageChange, bool PercentageChangeAvailable);

    private sealed record EffectiveCharge(
        ChargeType ChargeType,
        string ChargeName,
        ChargeSettingMode Mode,
        decimal Amount,
        ChargeSettingMode WarningMode,
        decimal? WarningThreshold);

    private static IQueryable<Property> AccessibleProperties(PropertyManagementDbContext db, RequestUser actor, bool includeArchived) =>
        db.Properties.Where(property => property.OrganizationId == actor.OrganizationId
            && (includeArchived || !property.IsArchived)
            && (actor.IsAdmin || db.StaffPropertyAssignments.Any(assignment => assignment.OrganizationId == actor.OrganizationId && assignment.UserId == actor.UserId && assignment.PropertyId == property.Id)));

    private static IQueryable<Tenant> AccessibleTenants(PropertyManagementDbContext db, RequestUser actor) =>
        db.Tenants.Where(tenant => tenant.OrganizationId == actor.OrganizationId
            && (actor.IsAdmin || db.OccupancyAssignments.Any(assignment => assignment.TenantId == tenant.Id && db.Units.Any(unit => unit.Id == assignment.UnitId && db.StaffPropertyAssignments.Any(staff => staff.UserId == actor.UserId && staff.PropertyId == unit.PropertyId)))));

    private static bool IsMissingRoomMeterIssueStorage(Exception exception) =>
        exception switch
        {
            DbException databaseException when databaseException.Message.Contains("room_meter_issues", StringComparison.OrdinalIgnoreCase)
                || databaseException.Message.Contains("room_meter_issue_audit_entries", StringComparison.OrdinalIgnoreCase)
                || databaseException.Message.Contains("user_notifications", StringComparison.OrdinalIgnoreCase) => true,
            _ when exception.InnerException is not null => IsMissingRoomMeterIssueStorage(exception.InnerException),
            _ => false,
        };

    private static IResult RoomMeterIssueStorageUnavailable() =>
        Results.Problem(
            title: "Room/meter issue storage unavailable",
            detail: "Room/meter issue reporting is unavailable until migration 20261010091817_RoomMeterIssues is applied to this database.",
            statusCode: StatusCodes.Status503ServiceUnavailable);

    private static async Task<Unit?> AccessibleUnitAsync(PropertyManagementDbContext db, RequestUser actor, Guid propertyId, Guid unitId, CancellationToken cancellationToken) =>
        await db.Units.FirstOrDefaultAsync(unit => unit.Id == unitId && unit.PropertyId == propertyId && unit.OrganizationId == actor.OrganizationId
            && db.Properties.Any(property => property.Id == propertyId && property.OrganizationId == actor.OrganizationId && !property.IsArchived)
            && (actor.IsAdmin || db.StaffPropertyAssignments.Any(assignment => assignment.OrganizationId == actor.OrganizationId && assignment.UserId == actor.UserId && assignment.PropertyId == propertyId)), cancellationToken);

    private static async Task<PropertyResponse> ToPropertyResponseAsync(PropertyManagementDbContext db, Property property, bool includeArchived, CancellationToken cancellationToken)
    {
        var floors = await db.Floors.AsNoTracking().Where(floor => floor.PropertyId == property.Id && (includeArchived || !floor.IsArchived)).OrderBy(floor => floor.SortOrder).ToListAsync(cancellationToken);
        var units = await db.Units.AsNoTracking().Where(unit => unit.PropertyId == property.Id && (includeArchived || (!unit.IsArchived && unit.IsEnabled))).OrderBy(unit => unit.Name).ToListAsync(cancellationToken);
        var floorResponses = floors.Select(floor => new FloorResponse(
            floor.Id,
            floor.Name,
            floor.SortOrder,
            floor.IsArchived,
            units.Where(unit => unit.FloorId == floor.Id).Select(unit => new UnitResponse(unit.Id, unit.Name, unit.IsEnabled, unit.IsArchived, unit.OccupancyStatus)).ToList())).ToList();
        return new PropertyResponse(property.Id, property.Name, property.Address, property.PropertyType, property.IsArchived, floorResponses, property.TimeZoneId);
    }

    private static AppUser NewUser(Guid organizationId, string email, string fullName, UserRole role) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        UserName = email.Trim().ToLowerInvariant(),
        Email = email.Trim().ToLowerInvariant(),
        FullName = fullName.Trim(),
        Role = role,
        IsActive = true,
    };

    private static async Task<IdentityResult> AddUserClaimsAsync(UserManager<AppUser> userManager, AppUser user) =>
        await userManager.AddClaimsAsync(user, new[]
        {
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("organization_id", user.OrganizationId.ToString()),
        });

    private static ApiUserResponse ToUserResponse(AppUser user) => new(user.Id, user.Email ?? string.Empty, user.FullName, user.Role, user.OrganizationId);

    private static ChargeConfigurationResponse ToChargeResponse(ChargeConfiguration configuration) =>
        new(configuration.Id, configuration.UnitId, configuration.ChargeType, configuration.Mode, configuration.BillingMonth, configuration.Amount, configuration.CustomLabel, configuration.WarningMode, configuration.WarningThreshold);

    private static string? ValidatePropertyDraft(PropertySetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Address))
        {
            return "Property name and address are required.";
        }

        if (request.Floors is null || request.Floors.Count == 0)
        {
            return "At least one floor is required.";
        }

        var names = request.Floors.Select(floor => floor.Name.Trim()).ToList();
        return names.Any(string.IsNullOrWhiteSpace) || names.Count != names.Distinct(StringComparer.OrdinalIgnoreCase).Count()
            ? "Floor names must be present and unique."
            : request.Floors.Select(ValidateFloorDraft).FirstOrDefault(error => error is not null);
    }

    private static string? ValidateFloorDraft(FloorDraft floor)
    {
        if (string.IsNullOrWhiteSpace(floor.Name) || floor.UnitCount < 0)
        {
            return "Each floor needs a name and a non-negative unit count.";
        }

        var names = CleanNames(floor.UnitNames);
        if (floor.UnitNames is { Count: > 0 } && floor.UnitCount != names.Count)
        {
            return $"Floor '{floor.Name}' unit count must match its unit names.";
        }

        return names.Count != names.Distinct(StringComparer.OrdinalIgnoreCase).Count()
            ? $"Unit names on floor '{floor.Name}' must be unique."
            : null;
    }

    private static IReadOnlyList<string> GetUnitNames(FloorDraft floor)
    {
        var provided = CleanNames(floor.UnitNames);
        return provided.Count > 0
            ? provided
            : Enumerable.Range(1, floor.UnitCount).Select(index => $"{floor.Name.Trim()}-{index}").ToList();
    }

    private static List<string> CleanNames(IEnumerable<string>? names) =>
        names?.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToList() ?? [];

    private static string? ValidateChargeSettings(IEnumerable<ChargeSettingRequest> settings, bool propertyDefaults)
    {
        var list = settings.ToList();
        if (list.GroupBy(item => new { item.ChargeType, item.BillingMonth }).Any(group => group.Count() > 1))
        {
            return "Only one setting per charge type and billing month is allowed.";
        }

        foreach (var setting in list)
        {
            if (setting.BillingMonth.Day != 1 || setting.Amount < 0)
            {
                return "Billing month must be the first day of a month and amounts cannot be negative.";
            }

            if (propertyDefaults && setting.Mode is not (ChargeSettingMode.Custom or ChargeSettingMode.Disabled))
            {
                return "Property defaults must use custom or disabled settings.";
            }

            if (setting.Mode is ChargeSettingMode.Inherited or ChargeSettingMode.Disabled && setting.Amount is not null)
            {
                return "Inherited and disabled settings must not include an amount.";
            }

            if (setting.Mode == ChargeSettingMode.Custom && setting.Amount is null)
            {
                return "Custom charge settings require a decimal amount or rate.";
            }

            if (setting.ChargeType == ChargeType.Custom && setting.Mode == ChargeSettingMode.Custom && string.IsNullOrWhiteSpace(setting.CustomLabel))
            {
                return "Custom charges require a label.";
            }

            if (setting.ChargeType == ChargeType.Electricity)
            {
                var warningMode = setting.WarningMode ?? (propertyDefaults ? ChargeSettingMode.Custom : ChargeSettingMode.Inherited);
                if (propertyDefaults && warningMode == ChargeSettingMode.Inherited)
                {
                    return "Property electricity warning settings cannot inherit from a unit.";
                }

                if (warningMode is ChargeSettingMode.Inherited or ChargeSettingMode.Disabled && setting.WarningThreshold is not null)
                {
                    return "Inherited and disabled electricity warnings must not include a threshold.";
                }

                if (warningMode == ChargeSettingMode.Custom && setting.WarningThreshold is < 0)
                {
                    return "Electricity warning thresholds cannot be negative.";
                }
            }
        }

        return null;
    }

    private static IResult BadRequest(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });

    private static IResult SelfApprovalForbidden() =>
        Results.Json(
            new { message = "Another authorized person must approve this collection report." },
            statusCode: StatusCodes.Status403Forbidden);

    private static Dictionary<string, string[]> IdentityErrors(IdentityResult result) =>
        result.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
}
