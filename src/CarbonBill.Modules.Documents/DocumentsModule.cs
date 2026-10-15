using CarbonBill.Modules.Documents.Endpoints;
using CarbonBill.Modules.Documents.Persistence;
using CarbonBill.Modules.Documents.Services;
using CarbonBill.SharedKernel.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonBill.Modules.Documents;

public static class DocumentsModuleExtensions
{
    public static IServiceCollection AddDocumentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Data Source=carbonbill.db;Cache=Shared";

        services.AddDbContext<DocumentsDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(
                sp.GetRequiredService<SqlitePragmaInterceptor>(),
                sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<CarbonBill.SharedKernel.Events.IDomainEventHandler<CarbonBill.SharedKernel.Events.DocumentExtractedEvent>, DocumentExtractedHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapDocumentsModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDocumentsEndpoints();
        return endpoints;
    }
}
