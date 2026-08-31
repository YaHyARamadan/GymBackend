namespace GymSaaS.Domain.Entities;

public class BranchManager
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    public string? Phone { get; set; }

    public int FacilityId { get; set; }

    /// <summary>Comma-separated branch IDs this manager is responsible for</summary>
    public string AssignedBranchIds { get; set; } = string.Empty;

    public int FailedLoginAttempts { get; set; } = 0;

    public DateTime? LockoutUntil { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation
    public Facility Facility { get; set; } = default!;
}
