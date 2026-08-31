using GymSaaS.Domain.Enums;

namespace GymSaaS.Domain.Entities;

public class SupportTicketMessage
{
    public int Id { get; set; }

    public int SupportTicketId { get; set; }

    public string SenderId { get; set; } = default!;

    public ActorType SenderActorType { get; set; }

    public string Message { get; set; } = default!;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public SupportTicket SupportTicket { get; set; } = default!;
}
