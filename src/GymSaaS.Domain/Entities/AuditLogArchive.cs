using GymSaaS.Domain.Enums;

namespace GymSaaS.Domain.Entities;

/// <summary>
/// Archive copy of AuditLogEntry records older than 3 months.
/// Moved by Hangfire recurring job. Searchable but not the primary load target.
/// </summary>
public class AuditLogArchive
{
    public long Id { get; set; }

    public long OriginalEntryId { get; set; }

    public string ActorId { get; set; } = default!;

    public ActorType ActorType { get; set; }

    public string? OnBehalfOfRole { get; set; }

    public string ActionType { get; set; } = default!;

    public string EntityType { get; set; } = default!;

    public string EntityId { get; set; } = default!;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime Timestamp { get; set; }

    public int? FacilityId { get; set; }

    public int? BranchId { get; set; }

    public string? CorrelationId { get; set; }

    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
}
