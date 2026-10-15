using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using CarbonBill.Api;
using CarbonBill.Api.Hangfire;
using CarbonBill.Api.Jobs;
using CarbonBill.Api.Middleware;
using CarbonBill.Modules.ActivityUnits;
using CarbonBill.Modules.Audit;
using CarbonBill.Modules.Calculation;
using CarbonBill.Modules.Documents;
using CarbonBill.Modules.Extraction;
using CarbonBill.Modules.FactorRegistry;
using CarbonBill.Modules.Flags;
using CarbonBill.Modules.Flags.Jobs;
using CarbonBill.Modules.GapDetection;
using CarbonBill.Modules.GapDetection.Jobs;
using CarbonBill.Modules.IdentityTenancy;
using CarbonBill.Modules.IdentityTenancy.Endpoints;
using CarbonBill.Modules.Insights;
using CarbonBill.Modules.Notifications;
using CarbonBill.Modules.Notifications.Jobs;
using CarbonBill.Modules.Onboarding;
using CarbonBill.Modules.PlatformAdmin;
using CarbonBill.Modules.Recommendations;
using CarbonBill.Modules.Reporting;
using CarbonBill.Modules.Review;
using CarbonBill.SharedKernel.Events;
using CarbonBill.SharedKernel.Providers;
using CarbonBill.SharedKernel.Providers.Stubs;
using CarbonBill.SharedKernel.Tenancy;
using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// Load .env if present in current directory or parents
LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Configure Serilog
builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
      .Enrich.FromLogContext()
      .WriteTo.Console(
          outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{TenantId}] {Message:lj}{NewLine}{Exception}",
          formatProvider: CultureInfo.InvariantCulture);
});

// Tenancy & Domain Events
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IDomainEventPublisher, InMemoryDomainEventPublisher>();

// Provider Configuration (UseFakes=false uses real module implementations by default)
var useFakes = builder.Configuration.GetValue<bool>("UseFakes", false);

if (useFakes)
{
    builder.Services.AddSingleton<IOcrProvider, FakeTesseractOcrProvider>();
    builder.Services.AddSingleton<IFileStore, LocalOrR2FileStoreStub>();
    builder.Services.AddSingleton<INotifier, LoggingNotifierStub>();
    builder.Services.AddSingleton<IReportRenderer, FakeQuestPdfReportRenderer>();
}
else
{
    builder.Services.AddSingleton<IOcrProvider, FakeTesseractOcrProvider>();
    builder.Services.AddSingleton<IFileStore, LocalOrR2FileStoreStub>();
    // Real INotifier and IReportRenderer are registered by AddNotificationsModule and AddReportingModule
}

// Register Domain Modules
builder.Services.AddIdentityTenancyModule(builder.Configuration);
builder.Services.AddAuditModule(builder.Configuration);
builder.Services.AddOnboardingModule(builder.Configuration);
builder.Services.AddDocumentsModule(builder.Configuration);
builder.Services.AddExtractionModule(builder.Configuration);
builder.Services.AddReviewModule(builder.Configuration);
builder.Services.AddActivityUnitsModule(builder.Configuration);
builder.Services.AddFactorRegistryModule(builder.Configuration);
builder.Services.AddCalculationModule(builder.Configuration);
builder.Services.AddGapDetectionModule(builder.Configuration);
builder.Services.AddFlagsModule(builder.Configuration);
builder.Services.AddRecommendationsModule(builder.Configuration);
builder.Services.AddReportingModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddPlatformAdminModule(builder.Configuration);
builder.Services.AddInsightsModule(builder.Configuration);

// Background Jobs (Hangfire + SQLite)
var hangfireConn = builder.Configuration.GetConnectionString("HangfireConnection")
    ?? "Data Source=hangfire.db;Cache=Shared";

builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UseSQLiteStorage(hangfireConn, new SQLiteStorageOptions
          {
              QueuePollInterval = TimeSpan.FromSeconds(5)
          });
});
builder.Services.AddHangfireServer();

builder.Services.AddScoped<IDatabaseBackupJob, DatabaseBackupJob>();
builder.Services.AddScoped<ISampleRecurringJob, SampleRecurringJob>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "CarbonBill-SuperSecret-Development-Key-2026-VeryLongKeyNeeded-AtLeast32Bytes";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CarbonBill";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "CarbonBillClients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// Built-in Rate Limiter
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var partitionKey = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst("sub")?.Value ?? "auth_user"
            : httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon_ip";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 10,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });
});

// Problem Details & Health Checks
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CarbonBill API",
        Version = "v1",
        Description = "SME Carbon-Accounting Platform API (ASP.NET Core LTS & SQLite)"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS (PWA origin support)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Execute CLI seed command if --seed argument is passed
if (args.Contains("--seed"))
{
    Log.Information("CarbonBill CLI --seed flag detected. Loading versioned platform datasets...");
    await DataSeeder.SeedDevelopmentDataAsync(app.Services);
    Log.Information("Versioned platform datasets successfully loaded. Exiting process.");
    return;
}

// Seed Database in Development
if (app.Environment.IsDevelopment())
{
    await DataSeeder.SeedDevelopmentDataAsync(app.Services);
}

// Middleware Pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseMiddleware<SqliteLockRetryMiddleware>();

app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CarbonBill API v1");
        c.RoutePrefix = "swagger";
    });
}

// Hangfire Dashboard (Admin Only)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAdminDashboardFilter()],
    DashboardTitle = "CarbonBill Background Jobs"
});

// Schedule Recurring Jobs
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    
    // Nightly SQLite atomic backup to R2
    recurringJobManager.AddOrUpdate<IDatabaseBackupJob>(
        "nightly-sqlite-vacuum-backup",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily(2)); // 02:00 AM UTC

    // Nightly gap detection alert evaluation
    recurringJobManager.AddOrUpdate<INightlyGapDetectionJob>(
        "gap-detection-nightly",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily());

    // Weekly consolidated notifications digest (Mondays 08:00 UTC)
    recurringJobManager.AddOrUpdate<IWeeklyDigestJob>(
        "notifications-weekly-digest",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Weekly(DayOfWeek.Monday, 8));

    // Nightly carbon flags evaluation and dismissal expiration
    recurringJobManager.AddOrUpdate<INightlyFlagEvaluationJob>(
        "flags-evaluation-nightly",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily(3)); // 03:00 AM UTC
}

// Health checks
app.MapHealthChecks("/healthz");

// Root endpoint
app.MapGet("/", () => Results.Ok(new
{
    service = "CarbonBill.Api",
    version = "1.0.0",
    status = "healthy",
    docs = "/swagger",
    jobs = "/hangfire",
    health = "/healthz"
}));

// Map Module Minimal APIs
app.MapAuthEndpoints();
app.MapOnboardingModuleEndpoints();
app.MapActivityUnitsModuleEndpoints();
app.MapFactorRegistryModuleEndpoints();
app.MapCalculationModuleEndpoints();
app.MapDocumentsModuleEndpoints();
app.MapExtractionModuleEndpoints();
app.MapReviewModuleEndpoints();
app.MapPlatformAdminModuleEndpoints();
app.MapGapDetectionModuleEndpoints();
app.MapNotificationsModuleEndpoints();
app.MapFlagsModuleEndpoints();
app.MapInsightsModuleEndpoints();
app.MapRecommendationsModuleEndpoints();
app.MapReportingModuleEndpoints();

app.Run();

static void LoadDotEnv()
{
    var dir = Directory.GetCurrentDirectory();
    while (!string.IsNullOrEmpty(dir))
    {
        var envFile = Path.Combine(dir, ".env");
        if (File.Exists(envFile))
        {
            foreach (var line in File.ReadAllLines(envFile))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
                var eqIdx = trimmed.IndexOf('=');
                if (eqIdx > 0)
                {
                    var k = trimmed[..eqIdx].Trim();
                    var v = trimmed[(eqIdx + 1)..].Trim();
                    if (!string.IsNullOrEmpty(k) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(k)))
                    {
                        Environment.SetEnvironmentVariable(k, v);
                    }
                }
            }
            break;
        }
        var parent = Directory.GetParent(dir);
        dir = parent?.FullName;
    }
}
