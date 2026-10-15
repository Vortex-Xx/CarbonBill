using System.Security.Cryptography;
using System.Text;
using CarbonBill.Modules.Calculation.Persistence;
using CarbonBill.Modules.Documents.Persistence;
using CarbonBill.Modules.IdentityTenancy.Persistence;
using CarbonBill.Modules.PlatformAdmin.Domain;
using CarbonBill.Modules.PlatformAdmin.Persistence;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CarbonBill.Modules.PlatformAdmin.Endpoints;

public record CreateDatasetVersionRequest(string Kind, string Version, string Description, int RecordCount, string? Content = null);

public static class PlatformAdminEndpoints
{
    public static IEndpointRouteBuilder MapPlatformAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin").WithTags("Admin");

        // Datasets
        group.MapPost("/datasets", async (
            [FromBody] CreateDatasetVersionRequest request,
            ITenantContext tenantContext,
            PlatformAdminDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            var checksum = !string.IsNullOrEmpty(request.Content)
                ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Content)))
                : Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

            var datasetVersion = new DatasetVersion
            {
                Kind = request.Kind,
                Version = request.Version,
                Description = request.Description,
                RecordCount = request.RecordCount,
                ChecksumSha256 = checksum,
                UploadedByUserId = tenantContext.CurrentUserId ?? Guid.Empty,
                UploadedAtUtc = DateTime.UtcNow,
                IsActive = true
            };

            dbContext.DatasetVersions.Add(datasetVersion);
            await dbContext.SaveChangesAsync(ct);

            return Results.Created($"/api/v1/admin/datasets/{datasetVersion.Id}", new
            {
                datasetVersionId = datasetVersion.Id,
                recordsLoaded = datasetVersion.RecordCount,
                version = datasetVersion.Version,
                checksum = datasetVersion.ChecksumSha256
            });
        });

        group.MapGet("/datasets", async (
            ITenantContext tenantContext,
            PlatformAdminDbContext dbContext,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            var datasets = await dbContext.DatasetVersions
                .AsNoTracking()
                .OrderByDescending(d => d.UploadedAtUtc)
                .ToListAsync(ct);

            return Results.Ok(datasets);
        });

        // Tenants overview
        group.MapGet("/tenants", async (
            ITenantContext tenantContext,
            IdentityTenancyDbContext identityDb,
            DocumentsDbContext docsDb,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            var orgs = await identityDb.Organizations.AsNoTracking().ToListAsync(ct);
            var memberships = await identityDb.Memberships.AsNoTracking().ToListAsync(ct);
            var docs = await docsDb.Documents.AsNoTracking().IgnoreQueryFilters().ToListAsync(ct);

            var tenantOverviews = orgs.Select(org => new
            {
                id = org.Id,
                name = org.Name,
                sector = org.Sector,
                isActive = org.IsActive,
                usersCount = memberships.Count(m => m.OrgId == org.Id && m.IsActive),
                documentsCount = docs.Count(d => d.OrgId == org.Id),
                createdAtUtc = org.CreatedAtUtc
            }).ToList();

            return Results.Ok(tenantOverviews);
        });

        // Global stats
        group.MapGet("/stats", async (
            ITenantContext tenantContext,
            IdentityTenancyDbContext identityDb,
            DocumentsDbContext docsDb,
            CalculationDbContext calcDb,
            CancellationToken ct) =>
        {
            if (!tenantContext.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            var orgCount = await identityDb.Organizations.CountAsync(ct);
            var userCount = await identityDb.Users.CountAsync(ct);
            var docCount = await docsDb.Documents.IgnoreQueryFilters().CountAsync(ct);
            var totalKgCo2e = await calcDb.EmissionResults.IgnoreQueryFilters().SumAsync(e => (double)e.KgCo2e, ct);

            return Results.Ok(new
            {
                totalOrganizations = orgCount,
                totalUsers = userCount,
                totalDocumentsCaptured = docCount,
                totalTonnesCo2eTracked = Math.Round((decimal)totalKgCo2e / 1000m, 2),
                serverTimeUtc = DateTime.UtcNow
            });
        });

        return endpoints;
    }
}
