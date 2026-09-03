using GymSaaS.Domain.Enums;

namespace GymSaaS.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    string? TokenId { get; }
    DateTime? TokenExpiresAt { get; }
    bool MustChangePassword { get; }
    ActorType? ActorType { get; }
    int? FacilityId { get; }
    int? BranchId { get; }
    bool IsSupervisor { get; }
    /// <summary>True when the supervisor is operating under an impersonation session scoped to a specific facility.</summary>
    bool IsImpersonating { get; }
    string? OnBehalfOfRole { get; }
}
