using System.ComponentModel.DataAnnotations;
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
        secure.MapPost("/properties/preview", (Delegate)PreviewProperty).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapPost("/properties", (Delegate)CreatePropertyAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapPut("/properties/{propertyId:guid}", (Delegate)UpdatePropertyAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapPost("/properties/{propertyId:guid}/floors", (Delegate)AddFloorAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapPut("/properties/{propertyId:guid}/floors/{floorId:guid}", (Delegate)UpdateFloorAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapPut("/properties/{propertyId:guid}/units/{unitId:guid}", (Delegate)UpdateUnitAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapPut("/properties/{propertyId:guid}/units/{unitId:guid}/occupancy-status", (Delegate)UpdateOccupancyStatusAsync);
        secure.MapGet("/staff", (Delegate)GetStaffAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapPost("/staff/invitations", (Delegate)CreateInvitationAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
        secure.MapPost("/properties/{propertyId:guid}/staff/{userId:guid}", (Delegate)AssignStaffAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
        secure.MapDelete("/properties/{propertyId:guid}/staff/{userId:guid}", (Delegate)RemoveStaffAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)));
        secure.MapGet("/tenants", (Delegate)GetTenantsAsync);
        secure.MapPost("/tenants", (Delegate)CreateTenantAsync);
        secure.MapPut("/tenants/{tenantId:guid}", (Delegate)UpdateTenantAsync);
        secure.MapGet("/properties/{propertyId:guid}/units/{unitId:guid}/occupancy", (Delegate)GetOccupancyHistoryAsync);
        secure.MapPost("/properties/{propertyId:guid}/units/{unitId:guid}/occupancy", (Delegate)AddOccupancyAsync);
        secure.MapPut("/properties/{propertyId:guid}/units/{unitId:guid}/occupancy/{assignmentId:guid}/close", (Delegate)CloseOccupancyAsync);
        secure.MapGet("/properties/{propertyId:guid}/charges", (Delegate)GetPropertyChargesAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapPut("/properties/{propertyId:guid}/charges", (Delegate)PutPropertyChargesAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapPost("/properties/{propertyId:guid}/charges/{chargeType}/end", (Delegate)EndPropertyChargeAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapGet("/properties/{propertyId:guid}/units/{unitId:guid}/charges", (Delegate)GetUnitChargesAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapPut("/properties/{propertyId:guid}/units/{unitId:guid}/charges", (Delegate)PutUnitChargesAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapPost("/properties/{propertyId:guid}/units/{unitId:guid}/charges/{chargeType}/end", (Delegate)EndUnitChargeAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapPost("/properties/{propertyId:guid}/billing-cycles", (Delegate)GenerateBillingCycleAsync);
        secure.MapGet("/properties/{propertyId:guid}/billing-cycles/{billingMonth}", (Delegate)GetBillingCycleAsync);
        secure.MapGet("/properties/{propertyId:guid}/billing-reports/{billingMonth}/units/{unitId:guid}", (Delegate)GetUnitBillingStatementAsync);
        secure.MapGet("/properties/{propertyId:guid}/billing-reports/{billingMonth}/tenants/{tenantId:guid}", (Delegate)GetTenantBillingStatementAsync);
        secure.MapGet("/properties/{propertyId:guid}/billing-reports/{billingMonth}/property-summary", (Delegate)GetPropertyBillingSummaryAsync);
        secure.MapGet("/properties/{propertyId:guid}/billing-reports/{billingMonth}/comparison", (Delegate)GetMonthlyBillingComparisonAsync);
        secure.MapPut("/properties/{propertyId:guid}/billing-cycles/{billingCycleId:guid}/meter-readings", (Delegate)SaveBillingReadingsAsync);
        secure.MapPost("/properties/{propertyId:guid}/billing-cycles/{billingCycleId:guid}/submit", (Delegate)SubmitBillingCycleAsync);
        secure.MapPost("/properties/{propertyId:guid}/billing-cycles/{billingCycleId:guid}/return", (Delegate)ReturnBillingCycleAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapPost("/properties/{propertyId:guid}/billing-cycles/{billingCycleId:guid}/approve", (Delegate)ApproveBillingCycleAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
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

        if (invitation.Role is not (UserRole.Manager or UserRole.BuildingStaff))
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
            : Results.Ok(new { user = ToUserResponse(user), organization = new { organization.Id, organization.Name } });
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

        var include = includeArchived == true && actor.IsManagerOrAdmin;
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

        var include = includeArchived == true && actor.IsManagerOrAdmin;
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

        var error = ValidatePropertyDraft(request);
        if (error is not null)
        {
            return BadRequest(error);
        }

        var property = new Property(actor.OrganizationId, request.Name, request.Address, request.PropertyType);
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

        var property = await AccessibleProperties(db, actor, includeArchived: true).FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return Results.NotFound();
        }

        try
        {
            property.Update(request.Name, request.Address, request.PropertyType);
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

        if (request.Role is not (UserRole.Manager or UserRole.BuildingStaff))
        {
            return BadRequest("Only Manager and Building Staff invitations can be created here.");
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
        var staff = await db.Users.FirstOrDefaultAsync(user => user.Id == userId && user.OrganizationId == actor.OrganizationId && user.Role == UserRole.BuildingStaff && user.IsActive, cancellationToken);
        if (property is null || staff is null)
        {
            return Results.NotFound();
        }

        var assignmentExists = await db.StaffPropertyAssignments.AnyAsync(item => item.UserId == userId && item.PropertyId == propertyId, cancellationToken);
        if (!assignmentExists)
        {
            db.StaffPropertyAssignments.Add(new StaffPropertyAssignment(actor.OrganizationId, userId, propertyId));
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

        db.StaffPropertyAssignments.Remove(assignment);
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

        var query = db.Tenants.AsNoTracking().Where(tenant => tenant.OrganizationId == actor.OrganizationId);
        if (actor.IsStaff)
        {
            query = query.Where(tenant => db.OccupancyAssignments.Any(assignment => assignment.TenantId == tenant.Id && db.Units.Any(unit => unit.Id == assignment.UnitId && db.StaffPropertyAssignments.Any(staff => staff.UserId == actor.UserId && staff.PropertyId == unit.PropertyId))));
        }

        var tenants = await query.OrderBy(tenant => tenant.Name).ToListAsync(cancellationToken);
        return Results.Ok(tenants.Select(tenant => new TenantResponse(tenant.Id, tenant.Name, tenant.Phone, tenant.OnboardingStatus)));
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

        if (!actor.IsManagerOrAdmin)
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
            && (!actor.IsStaff || db.StaffPropertyAssignments.Any(assignment => assignment.OrganizationId == actor.OrganizationId && assignment.UserId == actor.UserId && assignment.PropertyId == propertyId)), cancellationToken);

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
            && (!actor.IsStaff || db.StaffPropertyAssignments.Any(assignment => assignment.OrganizationId == actor.OrganizationId && assignment.UserId == actor.UserId && assignment.PropertyId == property.Id)));

    private static IQueryable<Tenant> AccessibleTenants(PropertyManagementDbContext db, RequestUser actor) =>
        db.Tenants.Where(tenant => tenant.OrganizationId == actor.OrganizationId
            && (!actor.IsStaff || db.OccupancyAssignments.Any(assignment => assignment.TenantId == tenant.Id && db.Units.Any(unit => unit.Id == assignment.UnitId && db.StaffPropertyAssignments.Any(staff => staff.UserId == actor.UserId && staff.PropertyId == unit.PropertyId)))));

    private static async Task<Unit?> AccessibleUnitAsync(PropertyManagementDbContext db, RequestUser actor, Guid propertyId, Guid unitId, CancellationToken cancellationToken) =>
        await db.Units.FirstOrDefaultAsync(unit => unit.Id == unitId && unit.PropertyId == propertyId && unit.OrganizationId == actor.OrganizationId
            && db.Properties.Any(property => property.Id == propertyId && property.OrganizationId == actor.OrganizationId && !property.IsArchived)
            && (!actor.IsStaff || db.StaffPropertyAssignments.Any(assignment => assignment.OrganizationId == actor.OrganizationId && assignment.UserId == actor.UserId && assignment.PropertyId == propertyId)), cancellationToken);

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
        return new PropertyResponse(property.Id, property.Name, property.Address, property.PropertyType, property.IsArchived, floorResponses);
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

    private static Dictionary<string, string[]> IdentityErrors(IdentityResult result) =>
        result.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
}
