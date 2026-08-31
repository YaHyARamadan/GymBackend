namespace GymSaaS.Domain.Entities;

/// <summary>Player subscription plan (inside a facility)</summary>
public class Subscription
{
    public int Id { get; set; }

    public string PlanName { get; set; } = default!;

    public decimal Price { get; set; }

    public int DurationInDays { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int FacilityId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Required for optimistic concurrency control</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public Facility Facility { get; set; } = default!;
}
