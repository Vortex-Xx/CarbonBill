using CarbonBill.Modules.ActivityUnits.Endpoints;
using CarbonBill.Modules.ActivityUnits.Persistence;
using CarbonBill.Modules.ActivityUnits.Services;
using CarbonBill.SharedKernel.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonBill.Modules.ActivityUnits;

public static class ActivityUnitsModuleExtensions
{
    public static IServiceCollection AddActivityUnitsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=carbonbill.db;Cache=Shared";

        services.AddDbContext<ActivityUnitsDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(
                sp.GetRequiredService<SqlitePragmaInterceptor>(),
                sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddScoped<IUnitConverter, UnitConverter>();

        return services;
    }

    public static IEndpointRouteBuilder MapActivityUnitsModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapActivityUnitsEndpoints();
        endpoints.MapUtilityConnectorsEndpoints();
        return endpoints;
    }
}
