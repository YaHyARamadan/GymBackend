using System.Text.Json;

namespace GymSaaS.API.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Reject if facility_id is passed via query string or custom headers (backend.md §0 rule 3)
        if (context.Request.Query.ContainsKey("facility_id") || 
            context.Request.Query.ContainsKey("facilityId") ||
            context.Request.Headers.ContainsKey("X-Facility-Id") ||
            context.Request.Headers.ContainsKey("facility_id"))
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(new
            {
                success = false,
                statusCode = 400,
                message = "ممنوع إرسال معرّف المنشأة في الاستعلام أو الترُويسات (Query/Headers). المعرّف يُستخرج حصريًا من التوكن."
            });
            await context.Response.WriteAsync(json);
            return;
        }

        await _next(context);
    }
}
