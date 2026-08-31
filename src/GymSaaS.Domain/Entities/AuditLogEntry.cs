using GymSaaS.Domain.Enums;

namespace GymSaaS.Domain.Entities;

/// <summary>
/// Immutable audit log entry. No delete or manual archiving allowed.
/// Entries older than 3 months are moved to AuditLogArchive by Hangfire job.
/// Sensitive fields (passwords, tokens): only "تم التعديل" is logged, not the value.
/// </summary>
public class AuditLogEntry
{
    public long Id { get; set; }

    public string ActorId { get; set; } = default!;

    public ActorType ActorType { get; set; }

    /// <summary>Used when supervisor is impersonating a role</summary>
    public string? OnBehalfOfRole { get; set; }

    /// <summary>create | update | delete</summary>
    public string ActionType { get; set; } = default!;

    public string EntityType { get; set; } = default!;

    public string EntityId { get; set; } = default!;

    /// <summary>JSON snapshot before change — sensitive fields omitted</summary>
    public string? OldValue { get; set; }

    /// <summary>JSON snapshot after change — sensitive fields omitted</summary>
    public string? NewValue { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public int? FacilityId { get; set; }

    public int? BranchId { get; set; }

    public string? CorrelationId { get; set; }
}
