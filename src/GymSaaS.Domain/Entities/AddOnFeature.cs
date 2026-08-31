namespace GymSaaS.Domain.Entities;

/// <summary>
/// An optional paid add-on feature (e.g. Online Store, AI Coach).
/// Defined and priced by the supervisor only.
/// Does NOT apply to Sold license facilities.
/// </summary>
public class AddOnFeature
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    /// <summary>Whether this add-on is available for sale to facilities</summary>
    public bool IsActiveForSale { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<FacilityAddOnSubscription> FacilitySubscriptions { get; set; } = [];
}
