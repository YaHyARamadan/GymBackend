using GymSaaS.Domain.Enums;

namespace GymSaaS.Domain.Interfaces;

public interface ITenantResolver
{
    int? FacilityId { get; }
    int? BranchId { get; }
    bool IsSupervisor { get; }
    string? ActorId { get; }
    ActorType? ActorType { get; }
    string? OnBehalfOfRole { get; }
}
