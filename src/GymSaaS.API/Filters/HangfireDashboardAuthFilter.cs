using System.Security.Claims;
using Hangfire.Dashboard;

namespace GymSaaS.API.Filters;

/// <summary>
/// فلتر حماية داشبورد Hangfire للتحقق من مصادقة المستخدم بصفته Supervisor فقط.
/// ينطبق هذا الفلتر حتى خلف الـ Reverse Proxies وداخل حاويات Docker.
/// </summary>
public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext?.User?.Identity == null || !httpContext.User.Identity.IsAuthenticated)
        {
            return false;
        }

        var actorType = httpContext.User.FindFirstValue("actor_type") ?? 
                         httpContext.User.FindFirstValue(ClaimTypes.Role);

        return actorType is "Supervisor" or "SUPERVISOR";
    }
}
