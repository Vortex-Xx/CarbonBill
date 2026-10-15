using CarbonBill.Modules.Recommendations.Domain;
using CarbonBill.Modules.Recommendations.Endpoints;
using CarbonBill.Modules.Recommendations.Loader;
using CarbonBill.Modules.Recommendations.Persistence;
using CarbonBill.Modules.Recommendations.Pipeline;
using CarbonBill.Modules.Recommendations.Pipeline.Steps;
using CarbonBill.Modules.Recommendations.Seeds;
using CarbonBill.Modules.Recommendations.Services;
using CarbonBill.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonBill.Modules.Recommendations;

public static class RecommendationsModuleExtensions
{
    public static IServiceCollection AddRecommendationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=carbonbill.db;Cache=Shared";

        services.AddDbContext<RecommendationsDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(
                sp.GetRequiredService<SqlitePragmaInterceptor>(),
                sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        // Pipeline Step 1 to 8 classes
        services.AddScoped<ProfileIngestionStep>();
        services.AddScoped<EligibilityFilterStep>();
        services.AddScoped<CarbonImpactStep>();
        services.AddScoped<FinancialModelingStep>();

        // Realism filter with spend threshold (config or default 1M BDT)
        decimal auditThreshold = configuration.GetValue<decimal?>("Recommendations:AuditThresholdBdt") ?? 1000000m;
        services.AddScoped(sp => new RealismFilterStep(auditThreshold));

        // Ranking step with configurable weights
        decimal wPayback = configuration.GetValue<decimal?>("Recommendations:Weights:Payback") ?? 0.35m;
        decimal wImpact = configuration.GetValue<decimal?>("Recommendations:Weights:Impact") ?? 0.35m;
        decimal wGrade = configuration.GetValue<decimal?>("Recommendations:Weights:Grade") ?? 0.20m;
        decimal wCapex = configuration.GetValue<decimal?>("Recommendations:Weights:Capex") ?? 0.10m;
        services.AddScoped(sp => new RankingStep(wPayback, wImpact, wGrade, wCapex));

        services.AddScoped<ExplanationStep>();
        services.AddScoped<CloseTheLoopStep>();

        // Pipeline Orchestrator
        services.AddScoped<IRecommendationPipeline, RecommendationPipeline>();

        // Dataset Loader
        services.AddScoped<MeasureDatasetLoader>();
        services.AddScoped<IDatasetLoader<Measure>>(sp => sp.GetRequiredService<MeasureDatasetLoader>());

        // Seed Loader
        services.AddScoped<IRecommendationsSeedLoader, RecommendationsSeedLoader>();

        // Main Module Service
        services.AddScoped<RecommendationsService>();

        return services;
    }

    public static IEndpointRouteBuilder MapRecommendationsModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRecommendationsEndpoints();
        return endpoints;
    }
}
