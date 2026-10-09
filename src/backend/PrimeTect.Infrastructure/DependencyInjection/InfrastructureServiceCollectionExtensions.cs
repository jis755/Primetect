using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PrimeTect.Infrastructure.Persistence;

namespace PrimeTect.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "PostgreSQL";

        if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            var databaseName = configuration["Database:Name"] ?? "PrimeTectTests";
            services.AddDbContext<PropertyManagementDbContext>((serviceProvider, options) => options
                .UseInMemoryDatabase(databaseName)
                .AddInterceptors(serviceProvider.GetServices<ISaveChangesInterceptor>())
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        }
        else
        {
            var connectionString = configuration.GetConnectionString("PrimeTectDatabase");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:PrimeTectDatabase must be configured for PostgreSQL.");
            }

            services.AddDbContext<PropertyManagementDbContext>((serviceProvider, options) => options
                .UseNpgsql(connectionString)
                .AddInterceptors(serviceProvider.GetServices<ISaveChangesInterceptor>()));
        }

        return services;
    }
}
