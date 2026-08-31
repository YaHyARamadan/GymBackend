using GymSaaS.Domain.Enums;

namespace GymSaaS.Domain.Entities;

/// <summary>
/// Represents a gym facility (tenant). Every tenant-scoped entity references this via FacilityId.
/// RowVersion is required for optimistic concurrency (backend.md §0 rule 9).
/// </summary>
public class Facility
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public LicenseType LicenseType { get; set; }

    public FacilityStatus Status { get; set; } = FacilityStatus.Active;

    /// <summary>Null means Lifetime license (for Sold type)</summary>
    public DateTime? LicenseEndDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Required for optimistic concurrency control</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public ICollection<Branch> Branches { get; set; } = [];
    public ICollection<Owner> Owners { get; set; } = [];
    public PlatformSubscription? PlatformSubscription { get; set; }
    public ICollection<FacilityAddOnSubscription> AddOnSubscriptions { get; set; } = [];
}
