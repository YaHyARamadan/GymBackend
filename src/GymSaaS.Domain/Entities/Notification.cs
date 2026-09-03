using GymSaaS.Domain.Enums;

namespace GymSaaS.Domain.Entities;

public class Notification
{
    public int Id { get; set; }

    public string RecipientId { get; set; } = default!;

    public ActorType RecipientActorType { get; set; }

    public int? FacilityId { get; set; }

    public string Title { get; set; } = default!;

    public string Message { get; set; } = default!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
