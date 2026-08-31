using System.Security.Claims;
using System.Text.Json;

namespace GymSaaS.API.Middleware;

public class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var mustChangeClaim = context.User.FindFirstValue("must_change_password");
            if (mustChangeClaim == "true")
            {
                var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
                // Allow change-password and logout endpoints
                if (!path.Contains("/change-password") && !path.Contains("/logout"))
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";
                    var json = JsonSerializer.Serialize(new
                    {
                        success = false,
                        statusCode = 403,
                        message = "يجب تغيير كلمة السر الافتراضية أولاً قبل استخدام خدمات المنصة."
                    });
                    await context.Response.WriteAsync(json);
                    return;
                }
            }
        }

        await _next(context);
    }
}
