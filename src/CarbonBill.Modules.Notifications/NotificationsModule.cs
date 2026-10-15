using CarbonBill.Modules.Notifications.Endpoints;
using CarbonBill.Modules.Notifications.Handlers;
using CarbonBill.Modules.Notifications.Jobs;
using CarbonBill.Modules.Notifications.Persistence;
using CarbonBill.Modules.Notifications.Services;
using CarbonBill.SharedKernel.Events;
using CarbonBill.SharedKernel.Persistence;
using CarbonBill.SharedKernel.Providers;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarbonBill.Modules.Notifications;

public static class NotificationsModuleExtensions
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=carbonbill.db;Cache=Shared";

        services.AddDbContext<NotificationsDbContext>((sp, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(
                sp.GetRequiredService<SqlitePragmaInterceptor>(),
                sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        // Email Providers
        services.AddHttpClient<BrevoEmailProvider>();
        services.AddScoped<IEmailProvider, BrevoEmailProvider>();

        services.AddHttpClient<ResendEmailProvider>();
        services.AddScoped<IEmailProvider, ResendEmailProvider>();

        services.AddScoped<IEmailProvider, LoggingEmailProvider>();

        // Web Push Providers
        services.AddHttpClient<VapidWebPushProvider>();
        services.AddScoped<IWebPushProvider, VapidWebPushProvider>();

        services.AddScoped<IWebPushProvider, LoggingWebPushProvider>();

        // Abstract INotifier provider implementation
        services.AddScoped<INotifier, NotifierService>();

        // Template Renderer (bilingual bn/en with Bangla numerals)
        services.AddSingleton<INotificationTemplateRenderer, NotificationTemplateRenderer>();

        // Core Notification & Fatigue Management Service
        services.AddScoped<INotificationService, NotificationService>();

        // Hangfire Weekly Digest Job
        services.AddScoped<IWeeklyDigestJob, WeeklyDigestJob>();

        // Domain Event Handlers
        services.AddScoped<IDomainEventHandler<MissingAlertRaisedEvent>, MissingAlertRaisedEventHandler>();
        services.AddScoped<IDomainEventHandler<FlagRaisedEvent>, FlagRaisedEventHandler>();
        services.AddScoped<IDomainEventHandler<DocumentRetakeRequestedEvent>, DocumentRetakeRequestedEventHandler>();
        services.AddScoped<IDomainEventHandler<DocumentFailedEvent>, DocumentFailedEventHandler>();

        return services;
    }

    public static IEndpointRouteBuilder MapNotificationsModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapNotificationEndpoints();
        return endpoints;
    }
}
