using System.Security.Claims;
using CarbonBill.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace CarbonBill.Api.Middleware;

public class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
            var orgIdClaim = user.FindFirst("org_id");
            var hasUserId = Guid.TryParse(userIdClaim?.Value, out var userId);
            
            // Allow tenant switching header for multi-org access
            Guid orgId = Guid.Empty;
            if (context.Request.Headers.TryGetValue("X-Org-Id", out var customOrgId) &&
                Guid.TryParse(customOrgId, out var parsedOrgId))
            {
                orgId = parsedOrgId;
            }
            else if (Guid.TryParse(orgIdClaim?.Value, out var tokenOrgId))
            {
                orgId = tokenOrgId;
            }

            if (hasUserId && orgId != Guid.Empty)
            {
                tenantContext.SetContext(orgId, userId);
            }
        }

        // Enrich Serilog context with current tenant and user IDs
        using (LogContext.PushProperty("TenantId", tenantContext.CurrentOrgId?.ToString() ?? "Anonymous"))
        using (LogContext.PushProperty("UserId", tenantContext.CurrentUserId?.ToString() ?? "Anonymous"))
        {
            await next(context);
        }
    }
}
