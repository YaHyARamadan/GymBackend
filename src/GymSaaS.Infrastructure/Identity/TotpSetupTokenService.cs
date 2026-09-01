using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GymSaaS.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GymSaaS.Infrastructure.Identity;

public class TotpSetupTokenService : ITotpSetupTokenService
{
    // This token is a bridge state BEFORE full authentication (issued after password check,
    // before TOTP verification) and must never be usable as a general Bearer token. Using the
    // real ValidAudience ("GymSaaSClient") here previously meant it passed the app-wide
    // [Authorize] pipeline like any full session token — protected only by the accident that no
    // handler currently trusts [Authorize] alone (see ActorType/FacilityId checks added to
    // ChangePasswordCommand, CreateBranchCommand, etc.). Giving it its own audience makes
    // AddJwtBearer's ValidateAudience reject it outright, so a caller who only has valid
    // credentials but hasn't passed 2FA can never reach a protected endpoint with it, regardless
    // of what any individual handler does or doesn't check.
    private const string SetupTokenAudience = "GymSaaS.TotpSetup";

    private readonly IConfiguration _configuration;

    public TotpSetupTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateSetupToken(int supervisorId, string? pendingSecret, TimeSpan ttl)
    {
        var secretKey = _configuration["JwtSettings:Secret"];
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("JwtSettings:Secret is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, supervisorId.ToString()),
            new("supervisor_id", supervisorId.ToString()),
            new("purpose", "totp_verification")
        };

        if (!string.IsNullOrEmpty(pendingSecret))
        {
            claims.Add(new Claim("pending_secret", pendingSecret));
        }

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"] ?? "GymSaaS",
            audience: SetupTokenAudience,
            claims: claims,
            expires: DateTime.UtcNow.Add(ttl),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (bool IsValid, int SupervisorId, string? PendingSecret) ValidateSetupToken(string token)
    {
        var secretKey = _configuration["JwtSettings:Secret"];
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("JwtSettings:Secret is not configured.");

        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = false,
            ValidateAudience = true,
            ValidAudience = SetupTokenAudience,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
            var jwtToken = (JwtSecurityToken)validatedToken;

            var purpose = jwtToken.Claims.FirstOrDefault(c => c.Type == "purpose")?.Value;
            if (purpose != "totp_verification") return (false, 0, null);

            var supervisorIdVal = jwtToken.Claims.FirstOrDefault(c => c.Type == "supervisor_id")?.Value;
            if (!int.TryParse(supervisorIdVal, out var supervisorId)) return (false, 0, null);

            var pendingSecret = jwtToken.Claims.FirstOrDefault(c => c.Type == "pending_secret")?.Value;

            return (true, supervisorId, pendingSecret);
        }
        catch
        {
            return (false, 0, null);
        }
    }
}
