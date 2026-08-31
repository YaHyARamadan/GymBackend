using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GymSaaS.Infrastructure.Identity;

public class ImpersonationTokenService : IImpersonationTokenService
{
    private readonly IConfiguration _configuration;

    public ImpersonationTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateImpersonationToken(string supervisorId, int facilityId, ActorType onBehalfOfRole, int? branchId, TimeSpan ttl)
    {
        var secretKey = _configuration["JwtSettings:Secret"] 
            ?? throw new InvalidOperationException("JwtSettings:Secret is not configured. The application cannot start without a valid JWT secret key.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, supervisorId),
            new("supervisor_id", supervisorId),
            new("actor_type", "SUPERVISOR"),
            new(ClaimTypes.Role, onBehalfOfRole.ToString()),
            new("on_behalf_of_role", onBehalfOfRole.ToString()),
            new("facility_id", facilityId.ToString()),
            new("is_impersonating", "true")
        };

        if (branchId.HasValue)
            claims.Add(new Claim("branch_id", branchId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"] ?? "GymSaaS",
            audience: _configuration["JwtSettings:Audience"] ?? "GymSaaSClient",
            claims: claims,
            expires: DateTime.UtcNow.Add(ttl),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (bool IsValid, string? SupervisorId, int? FacilityId, ActorType? OnBehalfOfRole, int? BranchId, bool IsExpired) ValidateToken(string token)
    {
        var secretKey = _configuration["JwtSettings:Secret"] 
            ?? throw new InvalidOperationException("JwtSettings:Secret is not configured. The application cannot start without a valid JWT secret key.");
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            var jwtToken = (JwtSecurityToken)validatedToken;

            var supervisorId = jwtToken.Claims.FirstOrDefault(c => c.Type == "supervisor_id")?.Value;
            var facilityVal = jwtToken.Claims.FirstOrDefault(c => c.Type == "facility_id")?.Value;
            var roleVal = jwtToken.Claims.FirstOrDefault(c => c.Type == "on_behalf_of_role")?.Value;
            var branchVal = jwtToken.Claims.FirstOrDefault(c => c.Type == "branch_id")?.Value;

            int? facilityId = int.TryParse(facilityVal, out var f) ? f : null;
            int? branchId = int.TryParse(branchVal, out var b) ? b : null;
            ActorType? role = Enum.TryParse<ActorType>(roleVal, out var r) ? r : null;

            return (true, supervisorId, facilityId, role, branchId, false);
        }
        catch (SecurityTokenExpiredException)
        {
            return (false, null, null, null, null, true);
        }
        catch
        {
            return (false, null, null, null, null, false);
        }
    }
}
