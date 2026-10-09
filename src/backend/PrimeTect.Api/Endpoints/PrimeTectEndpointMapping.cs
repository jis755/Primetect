using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
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
        secure.MapGet("/properties/{propertyId:guid}/units/{unitId:guid}/charges", (Delegate)GetUnitChargesAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
        secure.MapPut("/properties/{propertyId:guid}/units/{unitId:guid}/charges", (Delegate)PutUnitChargesAsync).RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Manager)));
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
                db.ChargeConfigurations.Add(new ChargeConfiguration(actor.OrganizationId, propertyId, null, setting.ChargeType, setting.Mode, setting.BillingMonth, setting.Amount, setting.CustomLabel));
            }
            else
            {
                existing.Update(ChargeSettingMode.Custom, setting.BillingMonth, setting.Amount, setting.CustomLabel);
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
                db.ChargeConfigurations.Add(new ChargeConfiguration(actor.OrganizationId, propertyId, unitId, setting.ChargeType, setting.Mode, setting.BillingMonth, setting.Amount, setting.CustomLabel));
            }
            else
            {
                existing.Update(setting.Mode, setting.BillingMonth, setting.Amount, setting.CustomLabel);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await db.ChargeConfigurations.AsNoTracking().Where(item => item.OrganizationId == actor.OrganizationId && item.PropertyId == propertyId && item.UnitId == unitId).Select(item => ToChargeResponse(item)).ToListAsync(cancellationToken));
    }

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
        new(configuration.Id, configuration.UnitId, configuration.ChargeType, configuration.Mode, configuration.BillingMonth, configuration.Amount, configuration.CustomLabel);

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
        }

        return null;
    }

    private static IResult BadRequest(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = [message] });

    private static Dictionary<string, string[]> IdentityErrors(IdentityResult result) =>
        result.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
}
