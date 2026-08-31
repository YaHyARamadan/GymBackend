using GymSaaS.Domain.Enums;

namespace GymSaaS.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    ActorType? ActorType { get; }
    int? FacilityId { get; }
    int? BranchId { get; }
    bool IsSupervisor { get; }
    string? OnBehalfOfRole { get; }
}
