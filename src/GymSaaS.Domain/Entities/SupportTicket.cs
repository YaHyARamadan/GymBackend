namespace GymSaaS.Domain.Entities;

/// <summary>
/// Support ticket opened by an Owner to communicate with the Supervisor.
/// No external channels (no WhatsApp/email) — everything inside the dashboard.
/// </summary>
public class SupportTicket
{
    public int Id { get; set; }

    public int FacilityId { get; set; }

    public int OwnerId { get; set; }

    public string Subject { get; set; } = default!;

    /// <summary>Open | InProgress | Closed</summary>
    public string Status { get; set; } = "Open";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ClosedAt { get; set; }

    // Navigation
    public Facility Facility { get; set; } = default!;
    public Owner Owner { get; set; } = default!;
    public ICollection<SupportTicketMessage> Messages { get; set; } = [];
}
