using CarbonBill.Modules.Reporting.Endpoints;
using CarbonBill.Modules.Reporting.Persistence;
using CarbonBill.Modules.Reporting.Renderers;
using CarbonBill.Modules.Reporting.Services;
using CarbonBill.SharedKernel.Persistence;
using CarbonBill.SharedKernel.Providers;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonBill.Modules.Reporting;

public static class ReportingModuleExtensions
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=carbonbill.db;Cache=Shared";

        services.AddDbContext<ReportingDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(
                sp.GetRequiredService<SqlitePragmaInterceptor>(),
                sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        // Time provider fallback
        services.AddSingleton(TimeProvider.System);

        // Report Renderers (QuestPDF and ClosedXML)
        services.AddScoped<QuestPdfReportRenderer>();
        services.AddScoped<ClosedXmlReportRenderer>();
        services.AddScoped<IReportRenderer>(sp => sp.GetRequiredService<QuestPdfReportRenderer>());

        // Reporting Business Service
        services.AddScoped<ReportingService>();

        return services;
    }

    public static IEndpointRouteBuilder MapReportingModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapReportingEndpoints();
        return endpoints;
    }
}
