using CarbonBill.Modules.Calculation.Endpoints;
using CarbonBill.Modules.Calculation.Persistence;
using CarbonBill.Modules.Calculation.Services;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonBill.Modules.Calculation;

public static class CalculationModuleExtensions
{
    public static IServiceCollection AddCalculationModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=carbonbill.db;Cache=Shared";

        services.AddDbContext<CalculationDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(
                sp.GetRequiredService<SqlitePragmaInterceptor>(),
                sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddScoped<IActivityWriter, ActivityWriter>();
        services.AddScoped<IEmissionReadModel, EmissionReadModel>();

        return services;
    }

    public static IEndpointRouteBuilder MapCalculationModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCalculationEndpoints();
        return endpoints;
    }
}
