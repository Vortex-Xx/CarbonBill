using CarbonBill.Modules.Insights.Endpoints;
using CarbonBill.Modules.Insights.Persistence;
using CarbonBill.Modules.Insights.Seeds;
using CarbonBill.Modules.Insights.Services;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonBill.Modules.Insights;

public static class InsightsModuleExtensions
{
    public static IServiceCollection AddInsightsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=carbonbill.db;Cache=Shared";

        services.AddDbContext<InsightsDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(
                sp.GetRequiredService<SqlitePragmaInterceptor>(),
                sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddScoped<InsightsService>();
        services.AddScoped<IInsightsService>(sp => sp.GetRequiredService<InsightsService>());
        services.AddScoped<IProductionMetricReader>(sp => sp.GetRequiredService<InsightsService>());
        services.AddScoped<IBenchmarkReader>(sp => sp.GetRequiredService<InsightsService>());

        services.AddScoped<IInsightsSeedLoader, InsightsSeedLoader>();

        return services;
    }

    public static IEndpointRouteBuilder MapInsightsModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapInsightsEndpoints();
        return endpoints;
    }
}
