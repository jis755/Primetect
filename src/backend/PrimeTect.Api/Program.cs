using System.Text.Json.Serialization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using PrimeTect.Api.Endpoints;
using PrimeTect.Api.Security;
using PrimeTect.Application;
using PrimeTect.Infrastructure.Identity;
using PrimeTect.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.ClaimsIdentity.SecurityStampClaimType = SecurityClaims.SecurityStamp;
    })
    .AddEntityFrameworkStores<PrimeTect.Infrastructure.Persistence.PropertyManagementDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "primetect.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = async context =>
        {
            // Re-check the Identity security stamp on every request so a password reset or phone change
            // invalidates the target user's existing cookies immediately. The principal is rejected and the
            // application cookie cleared on mismatch; unregistered Identity schemes are never touched.
            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
            var user = await userManager.GetUserAsync(context.Principal!);
            var presentedStamp = context.Principal?.FindFirstValue(SecurityClaims.SecurityStamp);
            if (user is null || !user.IsActive || !string.Equals(user.SecurityStamp ?? string.Empty, presentedStamp ?? string.Empty, StringComparison.Ordinal))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            }
        };
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
        var origins = configuredOrigins is { Length: > 0 } ? configuredOrigins : ["http://localhost:3000"];

        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

if (builder.Environment.IsEnvironment("Demo"))
{
    builder.Services.AddSingleton<IDataProtectionProvider>(_ => new EphemeralDataProtectionProvider());
}

var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseMiddleware<RecoverySessionMiddleware>();
app.UseAuthorization();
app.UseAntiforgery();
app.MapHealthChecks("/health");
app.MapGet("/api/health", () => Results.Ok(new HealthResponse("PrimeTect API", "ok", DateTimeOffset.UtcNow)))
    .WithName("ApiHealth");
app.MapPrimeTectEndpoints();

app.Run();

public partial class Program;
