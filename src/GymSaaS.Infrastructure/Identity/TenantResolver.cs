using System.Security.Claims;
using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GymSaaS.Infrastructure.Identity;

public class TenantResolver : ITenantResolver, ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantResolver(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? ActorId => UserId;

    public ActorType? ActorType
    {
        get
        {
            var val = _httpContextAccessor.HttpContext?.User.FindFirstValue("actor_type") ??
                      _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<ActorType>(val, true, out var result) ? result : null;
        }
    }

    public int? FacilityId
    {
        get
        {
            var val = _httpContextAccessor.HttpContext?.User.FindFirstValue("facility_id");
            return int.TryParse(val, out int id) ? id : null;
        }
    }

    public int? BranchId
    {
        get
        {
            var val = _httpContextAccessor.HttpContext?.User.FindFirstValue("branch_id");
            return int.TryParse(val, out int id) ? id : null;
        }
    }

    public bool IsSupervisor
    {
        get
        {
            var actor = ActorType;
            if (actor == Domain.Enums.ActorType.Supervisor) return true;

            var actorTypeClaim = _httpContextAccessor.HttpContext?.User.FindFirstValue("actor_type");
            return actorTypeClaim == "SUPERVISOR" || actorTypeClaim == "Supervisor";
        }
    }

    public string? OnBehalfOfRole => _httpContextAccessor.HttpContext?.User.FindFirstValue("on_behalf_of_role");
}
