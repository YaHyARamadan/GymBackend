using AspNetCoreRateLimit;
using FluentValidation;
using GymSaaS.API.Filters;
using GymSaaS.API.Middleware;
using GymSaaS.Application.Common.Behaviors;
using GymSaaS.Infrastructure;
using GymSaaS.Infrastructure.Jobs;
using Hangfire;
using MediatR;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Serilog Configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/gymsaas-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add Layers
builder.Services.AddInfrastructure(builder.Configuration);

// Add MediatR & FluentValidation
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(GymSaaS.Application.Common.Behaviors.ValidationBehavior<,>).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

// CRITICAL FIX: FluentValidation validators (LoginSupervisorCommandValidator, ChangePasswordCommandValidator, ...)
// were never registered in DI. ValidationBehavior<T> resolves IEnumerable<IValidator<T>>, which silently
// came back empty for every command in the app, so `if (!_validators.Any()) return await next();` always
// took the "no validators" path and skipped validation entirely — every command handler was receiving
// completely unvalidated input regardless of the FluentValidation rules written for it.
builder.Services.AddValidatorsFromAssembly(typeof(GymSaaS.Application.Common.Behaviors.ValidationBehavior<,>).Assembly);

// AspNetCoreRateLimit configuration (backend.md §3.9)
// SECURITY: Do NOT set IpRateLimitOptions.RealIpHeader to a client-suppliable header (e.g.
// "X-Real-IP"). AspNetCoreRateLimit trusts that header's value verbatim with no check that it
// actually came from a proxy — a caller can send a different value on every request and reset
// every rate-limit bucket (login, TOTP, change-password brute-force limits included) each time.
// The safe default below buckets by the real TCP connection IP instead.
//
// If this API sits behind a real reverse proxy/load balancer in production, RemoteIpAddress
// will otherwise be the proxy's own IP for every request, collapsing all clients into one
// shared bucket. The correct fix for that is ASP.NET Core's own ForwardedHeadersOptions scoped
// to the proxy's actual address — which only honors X-Forwarded-For from a known, trusted hop —
// not a bespoke header taken at face value. Configure "ReverseProxy:KnownProxies" (a comma
// separated list of trusted proxy IPs) to enable this; leave it unset for direct internet-facing
// deployments, where the TCP peer address is already the real client IP.
var knownProxies = builder.Configuration["ReverseProxy:KnownProxies"];
if (!string.IsNullOrWhiteSpace(knownProxies))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Clear();
        foreach (var proxy in knownProxies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPAddress.TryParse(proxy, out var ip))
                options.KnownProxies.Add(ip);
        }
    });
}

builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger Configuration
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Ensure database is created & EF Core migrations are applied automatically at startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<GymSaaS.Infrastructure.Persistence.GymSaaSDbContext>();
    dbContext.Database.Migrate();
    await GymSaaS.Infrastructure.Persistence.DbInitializer.SeedAsync(dbContext);
}

// Configure Middleware Pipeline
app.UseMiddleware<CorrelationIdMiddleware>();

// Must run before anything that reads Connection.RemoteIpAddress (rate limiting included) — see
// the ReverseProxy:KnownProxies note above. No-ops (does nothing) if that setting is unset.
if (!string.IsNullOrWhiteSpace(knownProxies))
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<ImpersonationGuardMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseIpRateLimiting();

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MustChangePasswordMiddleware>();

app.MapControllers();

// Hangfire Dashboard & Jobs (backend.md §3.2)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthFilter() }
});

using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<ArchiveOldAuditLogsJob>("archive-audit-logs", j => j.ExecuteAsync(), Cron.Monthly);
    recurringJobManager.AddOrUpdate<CheckExpiringSubscriptionsJob>("check-expiring-subscriptions", j => j.ExecuteAsync(), Cron.Daily);
}

app.Run();
