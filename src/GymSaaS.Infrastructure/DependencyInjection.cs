using System.Text;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Interfaces;
using GymSaaS.Infrastructure.Identity;
using GymSaaS.Infrastructure.Jobs;
using GymSaaS.Infrastructure.Persistence;
using GymSaaS.Infrastructure.Persistence.Interceptors;
using GymSaaS.Infrastructure.Services;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace GymSaaS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<TenantResolver>();
        services.AddScoped<ITenantResolver>(sp => sp.GetRequiredService<TenantResolver>());
        services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<TenantResolver>());

        services.AddScoped<AuditLogInterceptor>();

        // Startup Secret & Connection Validation (fail-fast security enforcement)
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured. The application cannot start without a valid database connection string.");

        var jwtSecret = configuration["JwtSettings:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
            throw new InvalidOperationException("JwtSettings:Secret is not configured. The application cannot start without a valid JWT secret key.");

        var encryptionSecret = configuration["Encryption:SecretKey"];
        if (string.IsNullOrWhiteSpace(encryptionSecret))
            throw new InvalidOperationException("Encryption:SecretKey is not configured. The application cannot start without a valid encryption secret key.");

        services.AddDbContext<GymSaaSDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditLogInterceptor>();
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
            });
            options.AddInterceptors(interceptor);
        });

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<GymSaaSDbContext>());

        // Identity & Security
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IImpersonationTokenService, ImpersonationTokenService>();
        services.AddSingleton<ITotpSetupTokenService, TotpSetupTokenService>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddSingleton<IPdfExportService, PdfExportService>();

        // JWT Authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["JwtSettings:Issuer"] ?? "GymSaaS",
                ValidAudience = configuration["JwtSettings:Audience"] ?? "GymSaaSClient",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            };

            // Revocation check: stateless JWTs otherwise stay valid for their full 7-day lifetime
            // no matter what happens to the account afterward — there is no logout/blacklist
            // mechanism anywhere in this app. Changing the Supervisor password now bumps
            // TokenVersion and stamps it into new tokens as "token_version"; here we reject any
            // token whose version doesn't match the current DB value, which is what actually
            // invalidates every older/stolen token the moment the password is changed.
            // Scoped to Supervisor only (single row, single indexed lookup, negligible per-request
            // cost) rather than every actor type, to avoid turning this into a DB hit on every
            // request platform-wide — see backend.md discussion referenced in ChangePasswordCommand.
            options.Events = new JwtBearerEvents
            {
                // Cookie-first extraction: if no Authorization header is present (frontend
                // now sends the JWT exclusively via httpOnly cookie), pull it from there.
                // This must run before OnTokenValidated, which is why it uses OnMessageReceived.
                OnMessageReceived = context =>
                {
                    if (string.IsNullOrEmpty(context.Token))
                    {
                        var cookieToken = context.HttpContext.Request.Cookies["gymsaas_token"];
                        if (!string.IsNullOrEmpty(cookieToken))
                            context.Token = cookieToken;
                    }
                    return Task.CompletedTask;
                },

                OnTokenValidated = async context =>
                {
                    var actorType = context.Principal?.FindFirstValue("actor_type");
                    if (actorType != nameof(ActorType.Supervisor))
                        return;

                    // Impersonation tokens are minted by ImpersonationTokenService, not
                    // JwtTokenGenerator, and never carry a token_version claim — skip them.
                    var isImpersonating = context.Principal?.FindFirstValue("is_impersonating");
                    if (isImpersonating == "true")
                        return;

                    var versionClaim = context.Principal?.FindFirstValue("token_version");
                    var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                    if (!int.TryParse(versionClaim, out var tokenVersion) || !int.TryParse(userId, out var supervisorId))
                    {
                        context.Fail("Invalid token version claim.");
                        return;
                    }

                    var dbContext = context.HttpContext.RequestServices.GetRequiredService<DbContext>();
                    var currentVersion = await dbContext.Set<GymSaaS.Domain.Entities.Supervisor>()
                        .Where(s => s.Id == supervisorId)
                        .Select(s => s.TokenVersion)
                        .FirstOrDefaultAsync();

                    if (currentVersion != tokenVersion)
                    {
                        context.Fail("Token has been revoked.");
                    }
                }
            };
        });

        // Hangfire
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString));

        services.AddHangfireServer();

        services.AddScoped<ArchiveOldAuditLogsJob>();
        services.AddScoped<CheckExpiringSubscriptionsJob>();

        return services;
    }
}
