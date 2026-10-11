using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PrimeTect.Api.Tests;

public sealed class PrimeTectApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"PrimeTectTests_{Guid.NewGuid():N}";

    public bool FailNextSave { get; set; }

    /// <summary>Private, test-owned meter photo storage root cleaned up by the owning test.</summary>
    public string PhotoRootPath { get; } = Path.Combine(Path.GetTempPath(), "primetect-tests-photos", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "InMemory");
        builder.UseSetting("Database:Name", databaseName);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "InMemory",
            ["Database:Name"] = databaseName,
            ["AllowedOrigins:0"] = "http://localhost:3000",
            ["MeterPhotos:RootPath"] = PhotoRootPath,
        }));
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddSingleton<ISaveChangesInterceptor>(new FailNextSaveChangesInterceptor(() =>
            {
                if (!FailNextSave)
                {
                    return false;
                }

                FailNextSave = false;
                return true;
            }));
        });
    }

    private sealed class FailNextSaveChangesInterceptor(Func<bool> shouldFail) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (shouldFail())
            {
                throw new InvalidOperationException("Intentional test persistence failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
