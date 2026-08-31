using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GymSaaS.Infrastructure.Identity;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(string userId, string email, ActorType actorType, int? facilityId, int? branchId)
    {
        var secretKey = _configuration["JwtSettings:Secret"] ?? "SuperSecretKeyForGymSaaSWithMinimum32Characters!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, actorType.ToString()),
            new("actor_type", actorType.ToString())
        };

        if (facilityId.HasValue)
            claims.Add(new Claim("facility_id", facilityId.Value.ToString()));

        if (branchId.HasValue)
            claims.Add(new Claim("branch_id", branchId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"] ?? "GymSaaS",
            audience: _configuration["JwtSettings:Audience"] ?? "GymSaaSClient",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
