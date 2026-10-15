using CarbonBill.Modules.Onboarding.Domain;
using CarbonBill.Modules.Onboarding.Endpoints;
using CarbonBill.Modules.Onboarding.Persistence;
using CarbonBill.Modules.Onboarding.Services;
using CarbonBill.SharedKernel.Contracts;
using CarbonBill.SharedKernel.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonBill.Modules.Onboarding;

public static class OnboardingModuleExtensions
{
    public static IServiceCollection AddOnboardingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=carbonbill.db;Cache=Shared";

        services.AddDbContext<OnboardingDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(
                sp.GetRequiredService<SqlitePragmaInterceptor>(),
                sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddScoped<IExpectedDocRuleReader, ExpectedDocRuleReader>();

        return services;
    }

    public static IEndpointRouteBuilder MapOnboardingModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOnboardingEndpoints();
        return endpoints;
    }
}
