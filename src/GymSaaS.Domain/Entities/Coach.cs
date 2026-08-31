namespace GymSaaS.Domain.Entities;

public class Coach
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    public string? Phone { get; set; }

    public string? Specialization { get; set; }

    public int BranchId { get; set; }

    public int FacilityId { get; set; }

    public int FailedLoginAttempts { get; set; } = 0;

    public DateTime? LockoutUntil { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation
    public Branch Branch { get; set; } = default!;
    public Facility Facility { get; set; } = default!;
}
