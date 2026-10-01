using Hangfire.Dashboard;

namespace BatteryTestingSystem.Utils;

/// <summary>
/// Restricts Hangfire dashboard to authenticated users only.
/// In production you can further restrict to Administrator role.
/// </summary>
public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated ?? false;
    }
}
