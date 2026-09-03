using System.IdentityModel.Tokens.Jwt;
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
        // Support cookie-first architecture: read from gymsaas_token cookie when there is
        // no Authorization header (the frontend now uses httpOnly cookies exclusively).
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        string? token = null;

        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader["Bearer ".Length..].Trim();
        }
        else
        {
            token = context.Request.Cookies["gymsaas_token"];
        }

        if (!string.IsNullOrEmpty(token))
        {
            // Only inspect expiry for tokens that identify themselves as impersonation
            // tokens. A normal expired JWT must be handled by JwtBearer as a regular 401.
            var isImpersonationToken = false;
            try
            {
                var untrustedToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
                isImpersonationToken = untrustedToken.Claims.Any(c =>
                    c.Type == "is_impersonating" && c.Value == "true");
            }
            catch
            {
                // JwtBearer remains responsible for malformed token responses.
            }

            var (_, _, _, _, _, isExpired) = isImpersonationToken
                ? impersonationTokenService.ValidateToken(token)
                : (false, null, null, null, null, false);

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
