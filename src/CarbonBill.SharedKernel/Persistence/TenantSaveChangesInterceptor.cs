using CarbonBill.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CarbonBill.SharedKernel.Persistence;

/// <summary>
/// Intercepts SaveChanges to enforce tenant isolation:
/// 1. Stamping the current OrgId on new ITenantScopedEntity instances if not already set.
/// 2. Rejecting cross-tenant modifications or deletions if the entity's OrgId does not match current tenant context.
/// </summary>
public class TenantSaveChangesInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ValidateAndStampTenancy(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ValidateAndStampTenancy(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ValidateAndStampTenancy(DbContext? context)
    {
        if (context == null) return;

        var entries = context.ChangeTracker.Entries<ITenantScopedEntity>().ToList();
        if (entries.Count == 0) return;

        var currentOrgId = _tenantContext.CurrentOrgId;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.OrgId == Guid.Empty)
                {
                    if (!currentOrgId.HasValue)
                    {
                        throw new InvalidOperationException(
                            $"Cannot insert tenant entity of type '{entry.Entity.GetType().Name}' without an active tenant context.");
                    }
                    entry.Entity.OrgId = currentOrgId.Value;
                }
                else if (currentOrgId.HasValue && entry.Entity.OrgId != currentOrgId.Value)
                {
                    throw new CrossTenantAccessException(entry.Entity.OrgId, currentOrgId, "Insert");
                }
            }
            else if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                if (currentOrgId.HasValue && entry.Entity.OrgId != currentOrgId.Value)
                {
                    throw new CrossTenantAccessException(
                        entry.Entity.OrgId,
                        currentOrgId,
                        entry.State == EntityState.Modified ? "Update" : "Delete");
                }
            }
        }
    }
}
