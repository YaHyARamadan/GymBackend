using GymSaaS.Domain.Enums;

namespace GymSaaS.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(string userId, string email, ActorType actorType, int? facilityId, int? branchId, bool mustChangePassword = false);
}
