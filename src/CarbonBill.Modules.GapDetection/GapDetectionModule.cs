using CarbonBill.Modules.GapDetection.Endpoints;
using CarbonBill.Modules.GapDetection.Fakes;
using CarbonBill.Modules.GapDetection.Handlers;
using CarbonBill.Modules.GapDetection.Jobs;
using CarbonBill.Modules.GapDetection.Persistence;
using CarbonBill.Modules.GapDetection.Services;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Events;
using CarbonBill.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarbonBill.Modules.GapDetection;

public static class GapDetectionModuleExtensions
{
    public static IServiceCollection AddGapDetectionModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=carbonbill.db;Cache=Shared";

        services.AddDbContext<GapDetectionDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(
                sp.GetRequiredService<SqlitePragmaInterceptor>(),
                sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        // Register default system TimeProvider if not already in container
        services.TryAddSingleton(TimeProvider.System);

        // Fallback fakes for dependencies from Track A & B until integrated
        services.TryAddScoped<IExpectedDocRuleReader, FakeExpectedDocRuleReader>();
        services.TryAddScoped<IDocumentReadModel, FakeDocumentReadModel>();

        // Gap detection services
        services.AddScoped<IGapDetector, GapDetectionService>();
        services.AddScoped<INightlyGapDetectionJob, NightlyGapDetectionJob>();

        // Event handlers
        services.AddScoped<IDomainEventHandler<DocumentUploadedEvent>, DocumentUploadedEventHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapGapDetectionModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGapEndpoints();
        return endpoints;
    }
}
