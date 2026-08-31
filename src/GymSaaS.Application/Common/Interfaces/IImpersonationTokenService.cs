using GymSaaS.Domain.Enums;

namespace GymSaaS.Application.Common.Interfaces;

public interface IImpersonationTokenService
{
    string GenerateImpersonationToken(string supervisorId, int facilityId, ActorType onBehalfOfRole, int? branchId, TimeSpan ttl);
    (bool IsValid, string? SupervisorId, int? FacilityId, ActorType? OnBehalfOfRole, int? BranchId, bool IsExpired) ValidateToken(string token);
}
