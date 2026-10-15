using Hangfire.Dashboard;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CarbonBill.Api.Hangfire;

public class HangfireAdminDashboardFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Allow all in local Development mode for convenience
        var env = httpContext.RequestServices.GetService<IWebHostEnvironment>();
        if (env?.IsDevelopment() == true)
        {
            return true;
        }

        var user = httpContext.User;
        return user.Identity?.IsAuthenticated == true;
    }
}
