using GymSaaS.Domain.Enums;

namespace GymSaaS.Domain.Entities;

/// <summary>
/// Internal payment record logged when supervisor receives payment and unlocks/activates.
/// Offline payments only — no payment gateway.
/// </summary>
public class PaymentRecord
{
    public int Id { get; set; }

    public int FacilityId { get; set; }

    public decimal Amount { get; set; }

    public PaymentType PaymentType { get; set; }

    /// <summary>Optional: which AddOn this payment covers</summary>
    public int? AddOnFeatureId { get; set; }

    public string? Notes { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Idempotency key to prevent double recording</summary>
    public string IdempotencyKey { get; set; } = default!;

    // Navigation
    public Facility Facility { get; set; } = default!;
    public AddOnFeature? AddOnFeature { get; set; }
}
