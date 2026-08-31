using System.Text.Json;
using GymSaaS.Application.Common.Interfaces;

namespace GymSaaS.API.Middleware;

public class ImpersonationGuardMiddleware
{
    private readonly RequestDelegate _next;

    public ImpersonationGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IImpersonationTokenService impersonationTokenService)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            var (isValid, _, _, _, _, isExpired) = impersonationTokenService.ValidateToken(token);

            if (isExpired)
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(new
                {
                    success = false,
                    statusCode = 401,
                    errorCode = "IMPERSONATION_EXPIRED",
                    message = "انتهت جلسة الدخول المؤقتة كـ Role. يرجى تجديد التوكن للمتابعة."
                });
                await context.Response.WriteAsync(json);
                return;
            }
        }

        await _next(context);
    }
}
