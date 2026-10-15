using CarbonBill.Modules.FactorRegistry.Domain;
using CarbonBill.Modules.FactorRegistry.Endpoints;
using CarbonBill.Modules.FactorRegistry.Persistence;
using CarbonBill.Modules.FactorRegistry.Services;
using CarbonBill.SharedKernel.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonBill.Modules.FactorRegistry;

public static class FactorRegistryModuleExtensions
{
    public static IServiceCollection AddFactorRegistryModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=carbonbill.db;Cache=Shared";

        services.AddDbContext<FactorRegistryDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(
                sp.GetRequiredService<SqlitePragmaInterceptor>(),
                sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddScoped<IFactorLookup, FactorLookupService>();

        return services;
    }

    public static IEndpointRouteBuilder MapFactorRegistryModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapFactorEndpoints();
        return endpoints;
    }
}
