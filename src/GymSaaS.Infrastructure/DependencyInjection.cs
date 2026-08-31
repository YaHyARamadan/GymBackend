using System.Text;
using GymSaaS.Application.Common.Interfaces;
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

        var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                               "Server=localhost,1433;Database=GymSaaSDb;User Id=sa;Password=Your_password123!;TrustServerCertificate=True;";

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
        services.AddSingleton<ITotpService, TotpService>();
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddSingleton<IPdfExportService, PdfExportService>();

        // JWT Authentication
        var secretKey = configuration["JwtSettings:Secret"] ?? "SuperSecretKeyForGymSaaSWithMinimum32Characters!";
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
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
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
