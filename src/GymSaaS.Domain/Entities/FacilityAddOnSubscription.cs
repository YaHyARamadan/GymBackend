using GymSaaS.Domain.Enums;

namespace GymSaaS.Domain.Entities;

/// <summary>
/// Links a facility to an activated add-on feature.
/// Independent from the main platform subscription status.
/// Does NOT exist for Sold license facilities.
/// </summary>
public class FacilityAddOnSubscription
{
    public int Id { get; set; }

    public int FacilityId { get; set; }

    public int AddOnFeatureId { get; set; }

    public AddOnFeatureStatus Status { get; set; } = AddOnFeatureStatus.Active;

    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; }

    // Navigation
    public Facility Facility { get; set; } = default!;
    public AddOnFeature AddOnFeature { get; set; } = default!;
}
