using CarbonBill.Modules.PlatformAdmin.Endpoints;
using CarbonBill.Modules.PlatformAdmin.Persistence;
using CarbonBill.SharedKernel.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonBill.Modules.PlatformAdmin;

public static class PlatformAdminModuleExtensions
{
    public static IServiceCollection AddPlatformAdminModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=carbonbill.db;Cache=Shared";

        services.AddDbContext<PlatformAdminDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(sp.GetRequiredService<SqlitePragmaInterceptor>());
        });

        return services;
    }

    public static IEndpointRouteBuilder MapPlatformAdminModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPlatformAdminEndpoints();
        return endpoints;
    }
}
