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
