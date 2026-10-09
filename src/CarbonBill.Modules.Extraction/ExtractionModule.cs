using CarbonBill.Modules.Extraction.Persistence;
using CarbonBill.Modules.Extraction.Services;
using CarbonBill.Modules.Extraction.Services.Groq;
using CarbonBill.Modules.Extraction.Services.Providers;
using CarbonBill.Modules.Extraction.Services.Tracing;
using CarbonBill.SharedKernel.Domain;
using CarbonBill.SharedKernel.Persistence;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonBill.Modules.Extraction;

public class ExtractionRun : BaseEntity, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid DocumentId { get; set; }
    public int TierUsed { get; set; } // 1 = Tesseract, 2 = Azure DI, 3 = LLM
    public string Status { get; set; } = "Completed";
    public float OverallConfidence { get; set; }
    public string? RawOutputJson { get; set; }
    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
    public List<ExtractedField> Fields { get; set; } = [];
}

public class ExtractedField : BaseEntity, ITenantScopedEntity
{
    public Guid OrgId { get; set; }
    public Guid ExtractionRunId { get; set; }
    public ExtractionRun ExtractionRun { get; set; } = null!;
    public Guid DocumentId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string RawValue { get; set; } = string.Empty;
    public string? NormalizedValue { get; set; }
    public string? CorrectedValue { get; set; }
    public float Confidence { get; set; }
    public int SourceTier { get; set; }
    public string? BoundingBoxJson { get; set; }
}

public static class ExtractionModuleExtensions
{
    public static IServiceCollection AddExtractionModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Data Source=carbonbill.db;Cache=Shared";

        services.AddDbContext<ExtractionDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(
                sp.GetRequiredService<SqlitePragmaInterceptor>(),
                sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddHttpClient<ILangSmithTracer, CarbonBill.Modules.Extraction.Services.Tracing.LangSmithTracer>();
        services.AddHttpClient<IGroqLlmExtractor, GroqLlmExtractor>();
        services.AddSingleton<IDualOcrEngine, DualOcrEngine>();
        services.AddScoped<IExtractionService, ExtractionService>();

        return services;
    }

    public static Microsoft.AspNetCore.Routing.IEndpointRouteBuilder MapExtractionModuleEndpoints(this Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)
    {
        CarbonBill.Modules.Extraction.Endpoints.ExtractionEndpoints.MapExtractionEndpoints(endpoints);
        return endpoints;
    }
}
