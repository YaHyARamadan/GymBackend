using GymSaaS.Application.Common.Interfaces;
using GymSaaS.Domain.Enums;
using GymSaaS.Domain.Exceptions;
using MediatR;

namespace GymSaaS.Application.Features.Auth.Queries;

public record GetCurrentSessionQuery : IRequest<CurrentSessionDto>;

public record CurrentSessionDto(
    string UserId,
    string Email,
    ActorType ActorType,
    int? FacilityId,
    int? BranchId,
    bool IsImpersonating,
    string? OnBehalfOfRole,
    bool MustChangePassword
);

public class GetCurrentSessionQueryHandler : IRequestHandler<GetCurrentSessionQuery, CurrentSessionDto>
{
    private readonly ICurrentUserService _currentUserService;

    public GetCurrentSessionQueryHandler(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public Task<CurrentSessionDto> Handle(
        GetCurrentSessionQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var email = _currentUserService.Email;
        var actorType = _currentUserService.ActorType;

        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(email) ||
            actorType is null)
        {
            throw new ForbiddenAccessException("The current user session is incomplete.");
        }

        return Task.FromResult(new CurrentSessionDto(
            userId,
            email,
            actorType.Value,
            _currentUserService.FacilityId,
            _currentUserService.BranchId,
            _currentUserService.IsImpersonating,
            _currentUserService.OnBehalfOfRole,
            _currentUserService.MustChangePassword));
    }
}
