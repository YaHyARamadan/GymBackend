using GymSaaS.Domain.Enums;

namespace GymSaaS.Domain.Entities;

/// <summary>
/// Platform-level subscription for a facility (paid to supervisor).
/// RowVersion required for optimistic concurrency.
/// </summary>
public class PlatformSubscription
{
    public int Id { get; set; }

    public int FacilityId { get; set; }

    public FacilityStatus Status { get; set; } = FacilityStatus.Active;

    public DateTime StartDate { get; set; }

    /// <summary>Null = Lifetime (for Sold license)</summary>
    public DateTime? EndDate { get; set; }

    public decimal AmountPaid { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Required for optimistic concurrency control</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public Facility Facility { get; set; } = default!;
}
