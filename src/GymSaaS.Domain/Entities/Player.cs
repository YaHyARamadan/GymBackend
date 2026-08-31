namespace GymSaaS.Domain.Entities;

public class Player
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string? Phone { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public int FacilityId { get; set; }

    public int BranchId { get; set; }

    public int? SubscriptionId { get; set; }

    public DateTime? SubscriptionStartDate { get; set; }

    public DateTime? SubscriptionEndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation
    public Facility Facility { get; set; } = default!;
    public Branch Branch { get; set; } = default!;
    public Subscription? Subscription { get; set; }
}
