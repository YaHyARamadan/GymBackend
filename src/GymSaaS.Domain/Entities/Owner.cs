namespace GymSaaS.Domain.Entities;

/// <summary>
/// Owner of a single facility. 
/// OnboardingCompleted and ContractSigned control access gates.
/// </summary>
public class Owner
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    public string? Phone { get; set; }

    public int FacilityId { get; set; }

    /// <summary>Blocked from accessing dashboard until contract is signed</summary>
    public bool ContractSigned { get; set; } = false;

    /// <summary>Blocked from accessing dashboard until onboarding form is completed</summary>
    public bool OnboardingCompleted { get; set; } = false;

    /// <summary>Consecutive failed login attempts</summary>
    public int FailedLoginAttempts { get; set; } = 0;

    public DateTime? LockoutUntil { get; set; }

    /// <summary>Consecutive failed TOTP verification attempts</summary>
    public int FailedTotpAttempts { get; set; } = 0;

    public DateTime? TotpLockoutUntil { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Facility Facility { get; set; } = default!;
}
