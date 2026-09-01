namespace GymSaaS.Domain.Entities;

/// <summary>
/// The single supervisor account for the entire platform.
/// 2FA (TOTP) is mandatory — secret is stored encrypted.
/// MustChangePassword is set on the seeded bootstrap account.
/// </summary>
public class Supervisor
{
    public int Id { get; set; }

    public string Email { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    /// <summary>AES-encrypted TOTP secret — never stored or logged in plaintext</summary>
    public string? TotpSecretEncrypted { get; set; }

    /// <summary>Whether 2FA setup has been completed</summary>
    public bool TotpEnabled { get; set; } = false;

    /// <summary>Force password change on first login</summary>
    public bool MustChangePassword { get; set; } = true;

    /// <summary>
    /// Bumped on every password change. Embedded in issued JWTs as the "token_version" claim
    /// and checked against this column on every request (see JwtBearer OnTokenValidated in
    /// Infrastructure/DependencyInjection.cs) so that changing the password immediately
    /// invalidates every previously issued token, not just the one the client happens to swap in.
    /// </summary>
    public int TokenVersion { get; set; } = 0;

    /// <summary>Consecutive failed login attempts for brute-force protection</summary>
    public int FailedLoginAttempts { get; set; } = 0;

    /// <summary>Account locked until this time (null = not locked)</summary>
    public DateTime? LockoutUntil { get; set; }

    /// <summary>Consecutive failed TOTP verification attempts for brute-force protection</summary>
    public int FailedTotpAttempts { get; set; } = 0;

    /// <summary>TOTP verification locked until this time (null = not locked)</summary>
    public DateTime? TotpLockoutUntil { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
