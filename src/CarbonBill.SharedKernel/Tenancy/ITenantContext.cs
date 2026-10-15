namespace CarbonBill.SharedKernel.Tenancy;

public interface ITenantScopedEntity
{
    Guid OrgId { get; set; }
}

public interface ITenantContext
{
    Guid? CurrentOrgId { get; }
    Guid? CurrentUserId { get; }
    bool IsAuthenticated { get; }

    void SetContext(Guid orgId, Guid userId);
    void Clear();
}

public class TenantContext : ITenantContext
{
    public Guid? CurrentOrgId { get; private set; }
    public Guid? CurrentUserId { get; private set; }
    public bool IsAuthenticated => CurrentUserId.HasValue;

    public void SetContext(Guid orgId, Guid userId)
    {
        CurrentOrgId = orgId;
        CurrentUserId = userId;
    }

    public void Clear()
    {
        CurrentOrgId = null;
        CurrentUserId = null;
    }
}

public class CrossTenantAccessException : Exception
{
    public Guid AttemptedOrgId { get; }
    public Guid? CurrentOrgId { get; }

    public CrossTenantAccessException(Guid attemptedOrgId, Guid? currentOrgId, string operation)
        : base($"Cross-tenant write operation '{operation}' rejected: Entity belongs to Org '{attemptedOrgId}', but current context is Org '{currentOrgId}'.")
    {
        AttemptedOrgId = attemptedOrgId;
        CurrentOrgId = currentOrgId;
    }
}
