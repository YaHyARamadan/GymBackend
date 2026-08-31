using GymSaaS.Domain.Enums;

namespace GymSaaS.Domain.Interfaces;

public interface ITenantResolver
{
    int? FacilityId { get; }
    int? BranchId { get; }
    bool IsSupervisor { get; }
    /// <summary>True when the supervisor is operating under an impersonation session scoped to a specific facility.</summary>
    bool IsImpersonating { get; }
    string? ActorId { get; }
    ActorType? ActorType { get; }
    string? OnBehalfOfRole { get; }
}
