using System.Text.RegularExpressions;
using Serilog.Context;

namespace GymSaaS.API.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    // Client-supplied and echoed straight into every log line and the response header for the
    // request — without this check, a value containing CR/LF could forge fake log entries (log
    // injection), and an unbounded length could bloat every log line. Only allow a conservative,
    // opaque-token shape; anything else is treated as absent and a fresh ID is generated instead.
    private static readonly Regex ValidCorrelationId = new("^[a-zA-Z0-9\\-]{1,64}$", RegexOptions.Compiled);

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requested = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        var correlationId = requested != null && ValidCorrelationId.IsMatch(requested)
            ? requested
            : Guid.NewGuid().ToString("N");

        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
